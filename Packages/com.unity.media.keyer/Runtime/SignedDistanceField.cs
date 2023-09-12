using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    partial class SignedDistanceField
    {
        struct BlurKernelIds
        {
            public int Horizontal;
            public int Vertical;
        }

        // Faster then larger group sizes.
        // More reasonable given we may store the whole tile in group shared memory.
        const int k_GroupSize = 16;

        // SDF.
        RenderTexture m_InputBuffer;
        RenderTexture m_OutputBuffer;
        ComputeShader m_Shader;
        KernelIds.SignedDistanceFieldIds m_KernelIds;

        // Blur.
        BlurKernelIds m_BlurKernelIds;
        ComputeShader m_BlurShader;
        RenderTexture m_TempBlurBuffer;

        public void Initialize()
        {
            var resources = KeyerResources.GetInstance();
            m_Shader = resources.Shaders.SignedDistanceField;
            m_KernelIds = resources.KernelIds.SignedDistanceField;
            m_BlurShader = resources.Shaders.Blur;
            m_BlurKernelIds = new BlurKernelIds
            {
                Horizontal = resources.KernelIds.Rendering.BlurHorizontal,
                Vertical = resources.KernelIds.Rendering.BlurVertical
            };
        }

        public void Dispose()
        {
            Utilities.DeallocateIfNeeded(ref m_InputBuffer);
            Utilities.DeallocateIfNeeded(ref m_OutputBuffer);
            Utilities.DeallocateIfNeeded(ref m_TempBlurBuffer);
        }

        public void Execute(
            CommandBuffer cmd, Texture input, RenderTexture output, Settings settings)
        {
            var width = input.width;
            var height = input.height;

            var groupsX = Mathf.CeilToInt(width / (float)k_GroupSize);
            var groupsY = Mathf.CeilToInt(height / (float)k_GroupSize);

            Utilities.AllocateIfNeededForCompute(ref m_InputBuffer, width, height, GraphicsFormat.R32_SFloat);
            Utilities.AllocateIfNeededForCompute(ref m_OutputBuffer, width, height, GraphicsFormat.R32_SFloat);

            var texelSize = new Vector4(input.width, input.height, 1 / (float)input.width, 1 / (float)input.height);
            cmd.SetComputeVectorParam(m_Shader, ShaderIDs._TexelSize, texelSize);

            InitializeField(cmd, groupsX, groupsY, input);

            Propagate(cmd, groupsX, groupsY, settings);

            if (settings.UseGroupsShared && settings.StepsPerLod[0] > 0)
            {
                GroupSharedPropagation(cmd, groupsX, groupsY, settings.StepsPerLod[0], settings.GroupsSharedPasses);
            }

            FinalBlit(cmd, groupsX, groupsY, output, settings.Scale);

            if (settings.UseBlur)
            {
                Blur(cmd, groupsX, groupsY, output, settings.BlurSampleCount, settings.BlurRadius);
            }
        }

        void InitializeField(CommandBuffer cmd, int groupsX, int groupsY, Texture input)
        {
            var kernel = m_KernelIds.Init;
            cmd.SetComputeTextureParam(m_Shader, kernel, ShaderIDs._SourceInput, input);
            cmd.SetComputeTextureParam(m_Shader, kernel, ShaderIDs._Output, m_OutputBuffer);
            cmd.DispatchCompute(m_Shader, kernel, groupsX, groupsY, 1);
        }

        void FinalBlit(CommandBuffer cmd, int groupsX, int groupsY, RenderTexture output, float scale)
        {
            var kernel = m_KernelIds.Final;
            cmd.SetComputeFloatParam(m_Shader, ShaderIDs._Scale, 1 / scale);
            cmd.SetComputeTextureParam(m_Shader, kernel, ShaderIDs._Input, m_OutputBuffer);
            cmd.SetComputeTextureParam(m_Shader, kernel, ShaderIDs._FinalOutput, output);

            cmd.DispatchCompute(m_Shader, kernel, groupsX, groupsY, 1);
        }

        void Propagate(CommandBuffer cmd, int groupsX, int groupsY, Settings settings)
        {
            var lods = settings.MaxLods - settings.MinLods;

            for (var lodPass = 0; lodPass != lods; ++lodPass)
            {
                var lod = lods - lodPass - 1;
                var jump = (int)Mathf.Pow(2, settings.MinLods + lod);

                // If we enable group shared pass and have reached lod 0, exit now.
                // Lod 0 will be handled afterwards.
                if (jump == 1 && settings.UseGroupsShared)
                {
                    break;
                }

                cmd.SetComputeIntParams(m_Shader, ShaderIDs._Jump, jump);

                var passes = settings.StepsPerLod[lod];

                for (var pass = 0; pass != passes; ++pass)
                {
                    // Horizontal pass.
                    Utilities.Swap(ref m_InputBuffer, ref m_OutputBuffer);
                    {
                        var kernel = m_KernelIds.PropagateHorizontal;
                        cmd.SetComputeTextureParam(m_Shader, kernel, ShaderIDs._Input, m_InputBuffer);
                        cmd.SetComputeTextureParam(m_Shader, kernel, ShaderIDs._Output, m_OutputBuffer);
                        cmd.DispatchCompute(m_Shader, kernel, groupsX, groupsY, 1);
                    }

                    // Vertical pass.
                    Utilities.Swap(ref m_InputBuffer, ref m_OutputBuffer);
                    {
                        var kernel = m_KernelIds.PropagateVertical;
                        cmd.SetComputeTextureParam(m_Shader, kernel, ShaderIDs._Input, m_InputBuffer);
                        cmd.SetComputeTextureParam(m_Shader, kernel, ShaderIDs._Output, m_OutputBuffer);
                        cmd.DispatchCompute(m_Shader, kernel, groupsX, groupsY, 1);
                    }
                }
            }
        }

        void GroupSharedPropagation(CommandBuffer cmd, int groupsX, int groupsY, int passes, int gpuPasses)
        {
            // Avoid an infinite loop on the GPU that would crash the machine.
            if (gpuPasses < 1)
            {
                throw new ArgumentException($"{nameof(gpuPasses)} must be superior or equal to 1.");
            }

            // Tiled processing creates its own type of artefacts.
            // Multiple passes here allow tiles to exchange information and remedy tot his.
            for (var pass = 0; pass != passes; ++pass)
            {
                Utilities.Swap(ref m_InputBuffer, ref m_OutputBuffer);

                // Times 2 as vertical and horizontal are implemented as 2 separate passes.
                cmd.SetComputeIntParam(m_Shader, ShaderIDs._Passes, gpuPasses * 2);

                var kernel = m_KernelIds.PropagateGroupShared;
                cmd.SetComputeTextureParam(m_Shader, kernel, ShaderIDs._Input, m_InputBuffer);
                cmd.SetComputeTextureParam(m_Shader, kernel, ShaderIDs._Output, m_OutputBuffer);

                cmd.DispatchCompute(m_Shader, kernel, groupsX, groupsY, 1);
            }
        }

        void Blur(CommandBuffer cmd, int groupsX, int groupsY, RenderTexture target, int sampleCount, float radius)
        {
            // 1 is the limit for the code to work. In practice we should be at 2 or above to avoid numerical issues.
            // Namely, blur introducing a shrinkage of the field.
            if (sampleCount < 1)
            {
                throw new ArgumentException(
                    $"{nameof(sampleCount)} must be superior or equal to 1.");
            }

            Utilities.AllocateIfNeededForCompute(ref m_TempBlurBuffer, target.width, target.height, target.graphicsFormat);
            var gaussianWeights = GaussianWeights.Get(sampleCount);

            Utilities.SetTexelSize(cmd, m_BlurShader, target);

            cmd.SetComputeIntParam(m_BlurShader, ShaderIDs._SampleCount, sampleCount);
            cmd.SetComputeFloatParam(m_BlurShader, ShaderIDs._Radius, radius);

            {
                var kernel = m_BlurKernelIds.Horizontal;
                cmd.SetComputeBufferParam(m_BlurShader, kernel, ShaderIDs._BlurWeights, gaussianWeights);
                cmd.SetComputeTextureParam(m_BlurShader, kernel, ShaderIDs._Input, target);
                cmd.SetComputeTextureParam(m_BlurShader, kernel, ShaderIDs._Output, m_TempBlurBuffer);
                cmd.DispatchCompute(m_BlurShader, kernel, groupsX, groupsY, 1);
            }
            {
                var kernel = m_BlurKernelIds.Vertical;
                cmd.SetComputeBufferParam(m_BlurShader, kernel, ShaderIDs._BlurWeights, gaussianWeights);
                cmd.SetComputeTextureParam(m_BlurShader, kernel, ShaderIDs._Input, m_TempBlurBuffer);
                cmd.SetComputeTextureParam(m_BlurShader, kernel, ShaderIDs._Output, target);
                cmd.DispatchCompute(m_BlurShader, kernel, groupsX, groupsY, 1);
            }
        }
    }
}
