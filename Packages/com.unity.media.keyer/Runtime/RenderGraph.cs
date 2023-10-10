using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Unity.Media.Keyer
{
    class RenderGraph : IDisposable
    {
        public interface IBuilder : IDisposable
        {
            void AddPass(IRenderPass pass);

            TextureHandles Handles { get; }
        }

        readonly struct Builder : IBuilder
        {
            readonly RenderGraph m_RenderGraph;

            public TextureHandles Handles => m_RenderGraph.m_TextureHandles;

            public Builder(RenderGraph renderGraph, int width, int height)
            {
                s_ActiveBuilderInstances++;
                m_RenderGraph = renderGraph;
                m_RenderGraph.m_TextureHandles.Dispose();
                m_RenderGraph.m_RenderPasses.Clear();
                m_RenderGraph.m_TexturePool.ResizeIfNeeded(width, height);
            }

            public void AddPass(IRenderPass pass)
            {
                if (m_RenderGraph.m_RenderPasses.Contains(pass))
                {
                    throw new InvalidOperationException(
                        $"Pass {pass} cannot be added twice.");
                }

                m_RenderGraph.m_RenderPasses.Add(pass);
            }

            public void Dispose()
            {
                m_RenderGraph.CullUnusedPasses();

#if DEVELOPMENT_BUILD || UNITY_EDITOR

                // Render Graph validation, all transient targets should be read.
                // The success of this test only depends on the correctness of the above code,
                // which is why it is not needed at runtime.
                m_RenderGraph.m_TextureHandles.CheckForUnusedTransients();
#endif
                s_ActiveBuilderInstances--;
            }
        }

        // Used for safety check. We should not Execute while building.
        // The builder needs to be given the chance to sanitize the graph before execution.
        static int s_ActiveBuilderInstances;
        static readonly List<IRenderPass> s_CulledRenderPasses = new();

        readonly List<IRenderPass> m_RenderPasses = new();
        readonly TexturePool m_TexturePool = new();
        TextureHandles m_TextureHandles;

        // Used for testing.
        public IReadOnlyList<IRenderPass> RenderPasses => m_RenderPasses.AsReadOnly();

        public RenderGraph()
        {
            m_TextureHandles = new TextureHandles(m_TexturePool);
        }

        public void Initialize()
        {
            // TODO Should be exposed to the user or connected to project settings.
            BufferFormatUtility.SetQuality(RenderingQuality.HDRMedium);
        }

        public void Dispose()
        {
            m_RenderPasses.Clear();
            m_TextureHandles.Dispose();
            m_TexturePool.Dispose();
        }

        public IBuilder GetBuilder(int width, int height) => new Builder(this, width, height);

        public void Execute(CommandBuffer cmd)
        {
            if (s_ActiveBuilderInstances > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot {nameof(Execute)} while {nameof(IBuilder)} instances are not disposed.");
            }

            var resources = KeyerResources.GetInstance();

            var context = new Context
            {
                Shaders = resources.Shaders,
                KernelIds = resources.KernelIds.Rendering
            };

            m_TextureHandles.Reset();

            foreach (var pass in m_RenderPasses)
            {
                pass.Execute(cmd, context);
                m_TextureHandles.ReleaseConsumedTransients();
            }

            // By this point all transient textures should have been released.
            Assert.IsTrue(m_TextureHandles.GetAllocatedTransientTexturesCount() == 0);
        }

        // Removes from the render graph passes whose output is not used.
        void CullUnusedPasses()
        {
            Assert.IsTrue(s_CulledRenderPasses.Count == 0);

            // Repeatedly remove unused passes.
            // Once we remove an unused pass another one may become unused as well.
            for (; ; )
            {
                // Collect unused passes.
                foreach (var pass in m_RenderPasses)
                {
                    // Just a sanity check since we're here
                    if (pass.Output.Type == ResourceType.Transient && pass.Output.TransientUsage != TransientUsage.Write)
                    {
                        var handleName = m_TextureHandles.GetName(pass.Output);
                        throw new InvalidOperationException(
                            $"Pass {nameof(pass.Output)} transient \"{handleName}\" should have usage {TransientUsage.Write}.");
                    }

                    if (m_TextureHandles.IsUnusedTransient(pass.Output))
                    {
                        s_CulledRenderPasses.Add(pass);
                    }
                }

                // When no more unused passes can be found, the process is complete.
                if (s_CulledRenderPasses.Count == 0)
                {
                    return;
                }

                // Remove unused passes. Destroy their transient input(s) and output.
                foreach (var pass in s_CulledRenderPasses)
                {
                    foreach (var input in pass.Inputs)
                    {
                        m_TextureHandles.DestroyIfTransient(input);
                    }

                    m_TextureHandles.DestroyIfTransient(pass.Output);
                    m_RenderPasses.Remove(pass);
                }

                s_CulledRenderPasses.Clear();
            }
        }
    }
}
