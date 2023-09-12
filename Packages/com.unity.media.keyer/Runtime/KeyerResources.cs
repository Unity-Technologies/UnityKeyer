using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    [Serializable]
    sealed class Shaders
    {
        [ShaderName("Hidden/Keyer/Blit")]
        public Shader Blit;
        [ShaderName("Hidden/Keyer/Plot")]
        public Shader Plot;
        [ShaderName("PlotPicking")]
        public ComputeShader PlotPicking;
        [ShaderName("Hidden/Keyer/GarbageMask2d")]
        public Shader GarbageMask2d;
        [ShaderName("Hidden/Keyer/Solid")]
        public Shader Solid;
        [ShaderName("ExpectationMaximization")]
        public ComputeShader ExpectationMaximization;
        [ShaderName("KeyExtract")]
        public ComputeShader KeyExtract;
        [ShaderName("ColorDifference")]
        public ComputeShader ColorDifference;
        [ShaderName("ColorDistance")]
        public ComputeShader ColorDistance;
        [ShaderName("Despill")]
        public ComputeShader Despill;
        [ShaderName("Crop")]
        public ComputeShader Crop;
        [ShaderName("Blur")]
        public ComputeShader Blur;
        [ShaderName("Clip")]
        public ComputeShader Clip;
        [ShaderName("BlendMax")]
        public ComputeShader BlendMax;
        [ShaderName("Combine")]
        public ComputeShader Combine;
        [ShaderName("SignedDistanceField")]
        public ComputeShader SignedDistanceField;
    }

    // We subdivide kernels among types for scaling reasons.
    // We would do the same with shaders if their number justified it.
    [Serializable]
    class KernelIds
    {
        [Serializable]
        public class ExpectationMaximizationIds
        {
            [ShaderName("ExpectationMaximization")]
            public int ResetColorBins;
            [ShaderName("ExpectationMaximization")]
            public int UpdateColorBins;
            [ShaderName("ExpectationMaximization")]
            public int ResetSelectedColorBins;
            [ShaderName("ExpectationMaximization")]
            public int SelectColorBins;
            [ShaderName("ExpectationMaximization")]
            public int InitializeCovariances;
            [ShaderName("ExpectationMaximization")]
            public int ProcessCovariances;
            [ShaderName("ExpectationMaximization")]
            public int UpdateIndirectArguments;
            [ShaderName("ExpectationMaximization")]
            public int UpdateWeightsAndCentroids;
            [ShaderName("ExpectationMaximization")]
            public int ReduceSumsAndCentroids;
            [ShaderName("ExpectationMaximization")]
            public int UpdateCovariances;
            [ShaderName("ExpectationMaximization")]
            public int ReduceCovariances;
            [ShaderName("ExpectationMaximization")]
            public int NormalizeCentroidsAndCovariances;
        }

        [Serializable]
        public class KeyExtractIds
        {
            [ShaderName("KeyExtract")]
            public int Filter;
            [ShaderName("KeyExtract")]
            public int Reduce;
        }

        [Serializable]
        public class SignedDistanceFieldIds
        {
            [ShaderName("SignedDistanceField")]
            public int Init;
            [ShaderName("SignedDistanceField")]
            public int PropagateHorizontal;
            [ShaderName("SignedDistanceField")]
            public int PropagateVertical;
            [ShaderName("SignedDistanceField")]
            public int PropagateGroupShared;
            [ShaderName("SignedDistanceField")]
            public int Final;
        }

        [Serializable]
        public class RenderingIds
        {
            [ShaderName("ColorDifference")]
            public int ColorDifferenceGreen;
            [ShaderName("ColorDifference")]
            public int ColorDifferenceBlue;
            [ShaderName("ColorDistance")]
            public int ColorDistance;
            [ShaderName("Despill")]
            public int DespillGreen;
            [ShaderName("Despill")]
            public int DespillBlue;
            [ShaderName("Crop")]
            public int Crop;
            [ShaderName("Blur")]
            public int BlurHorizontal;
            [ShaderName("Blur")]
            public int BlurVertical;
            [ShaderName("Clip")]
            public int Clip;
            [ShaderName("BlendMax")]
            public int BlendMax;
            [ShaderName("Combine")]
            public int Combine;
            [ShaderName("PlotPicking")]
            public int BlendId;
            [ShaderName("PlotPicking")]
            public int PropagateId;
        }

        public ExpectationMaximizationIds ExpectationMaximization = new();
        public KeyExtractIds KeyExtract = new();
        public SignedDistanceFieldIds SignedDistanceField = new();
        public RenderingIds Rendering = new();
    }

    class KeyerResources : ScriptableObject
    {
        public const string MenuEntry = "Window/Virtual Production/Create Keyer Resources";

        static KeyerResources s_Instance;

        public static KeyerResources GetInstance()
        {
            if (s_Instance == null)
            {
                var resources = Resources.Load<KeyerResources>(nameof(KeyerResources));
                if (resources == null)
                {
                    // We manage this ScriptableObject ourselves, this should not happen.
                    // Unless the user deleted the instance.
                    EditorBridge.LoadResources();
                    resources = Resources.Load<KeyerResources>(nameof(KeyerResources));
                }

                if (resources == null)
                {
                    throw new InvalidOperationException("Failed to load keyer resources.");
                }

                s_Instance = resources;
            }

            return s_Instance;
        }

        enum Versions
        {
            Initial = 0
        }

        [SerializeField, HideInInspector]
#pragma warning disable CS0414
        int m_Version = (int)Versions.Initial;
#pragma warning restore CS0414

        public Shaders Shaders = new();
        public KernelIds KernelIds = new();
    }
}
