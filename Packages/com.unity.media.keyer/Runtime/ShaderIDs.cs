using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    static class ShaderIDs
    {
        // Common.
        public static readonly int _Size = Shader.PropertyToID("_Size");
        public static readonly int _Id = Shader.PropertyToID("_Id");
        public static readonly int _SourceTexture = Shader.PropertyToID("_SourceTexture");
        public static readonly int _MaskTexture = Shader.PropertyToID("_MaskTexture");
        public static readonly int _SourceScaleBias = Shader.PropertyToID("_SourceScaleBias");
        public static readonly int _SourceMipLevel = Shader.PropertyToID("_SourceMipLevel");
        public static readonly int _SourceTexelSize = Shader.PropertyToID("_SourceTexelSize");
        public static readonly int _TargetTexelSize = Shader.PropertyToID("_TargetTexelSize");
        public static readonly int _TexelSize = Shader.PropertyToID("_TexelSize");
        public static readonly int _TargetScaleBias = Shader.PropertyToID("_TargetScaleBias");
        public static readonly int _Color = Shader.PropertyToID("_Color");

        public static readonly int _Input = Shader.PropertyToID("_Input");
        public static readonly int _Output = Shader.PropertyToID("_Output");

        public static readonly int _CoverageTexture = Shader.PropertyToID("_CoverageTexture");
        public static readonly int _IdTexture = Shader.PropertyToID("_IdTexture");
        public static readonly int _CoverageTextureIn = Shader.PropertyToID("_CoverageTextureIn");
        public static readonly int _IdTextureIn = Shader.PropertyToID("_IdTextureIn");
        public static readonly int _CoverageTextureOut = Shader.PropertyToID("_CoverageTextureOut");
        public static readonly int _IdTextureOut = Shader.PropertyToID("_IdTextureOut");

        public static readonly int _Vertices = Shader.PropertyToID("_Vertices");
        public static readonly int _Indices = Shader.PropertyToID("_Indices");

        // Plot Rendering.
        public static readonly int _Line = Shader.PropertyToID("_Line");
        public static readonly int _SourceBuffer = Shader.PropertyToID("_SourceBuffer");
        public static readonly int _PlotParams = Shader.PropertyToID("_PlotParams");
        public static readonly int _Channels = Shader.PropertyToID("_Channels");

        // Keying Color Extraction.
        public static readonly int _KeyingColorRGB = Shader.PropertyToID("_KeyingColorRGB");
        public static readonly int _SampleCoords = Shader.PropertyToID("_SampleCoords");
        public static readonly int _Colors = Shader.PropertyToID("_Colors");
        public static readonly int _BatchSize = Shader.PropertyToID("_BatchSize");
        public static readonly int _ColorsIn = Shader.PropertyToID("_ColorsIn");
        public static readonly int _ColorsOut = Shader.PropertyToID("_ColorsOut");

        // Expectation-Maximization.
        public static readonly int _CentroidsIn = Shader.PropertyToID("_CentroidsIn");
        public static readonly int _CentroidsOut = Shader.PropertyToID("_CentroidsOut");
        public static readonly int _Count = Shader.PropertyToID("_Count");
        public static readonly int _ColorBins = Shader.PropertyToID("_ColorBins");
        public static readonly int _Weights = Shader.PropertyToID("_Weights");
        public static readonly int _SourceSize = Shader.PropertyToID("_SourceSize");
        public static readonly int _CovariancesIn = Shader.PropertyToID("_CovariancesIn");
        public static readonly int _CovariancesOut = Shader.PropertyToID("_CovariancesOut");
        public static readonly int _SumsIn = Shader.PropertyToID("_SumsIn");
        public static readonly int _SumsOut = Shader.PropertyToID("_SumsOut");
        public static readonly int _SqrtDetReciprocals = Shader.PropertyToID("_SqrtDetReciprocals");
        public static readonly int _InverseCovariances = Shader.PropertyToID("_InverseCovariances");
        public static readonly int _NumClusters = Shader.PropertyToID("_NumClusters");
        public static readonly int _SelectedColorBins = Shader.PropertyToID("_SelectedColorBins");
        public static readonly int _AppendSelectedColorBins = Shader.PropertyToID("_AppendSelectedColorBins");
        public static readonly int _IndirectBuffer = Shader.PropertyToID("_IndirectBuffer");
        public static readonly int _IndirectArgsOffset = Shader.PropertyToID("_IndirectArgsOffset");

        // Render Passes.
        public static readonly int _Mask = Shader.PropertyToID("_Mask");
        public static readonly int _Foreground = Shader.PropertyToID("_Foreground");
        public static readonly int _Background = Shader.PropertyToID("_Background");
        public static readonly int _BlurWeights = Shader.PropertyToID("_BlurWeights");
        public static readonly int _SampleCount = Shader.PropertyToID("_SampleCount");
        public static readonly int _Radius = Shader.PropertyToID("_Radius");
        public static readonly int _Rect = Shader.PropertyToID("_Rect");
        public static readonly int _Range = Shader.PropertyToID("_Range");
        public static readonly int _Alpha = Shader.PropertyToID("_Alpha");
        public static readonly int _Amount = Shader.PropertyToID("_Amount");
        public static readonly int _Invert = Shader.PropertyToID("_Invert");
        public static readonly int _Scale = Shader.PropertyToID("_Scale");
        public static readonly int _GarbageMaskParams = Shader.PropertyToID("_GarbageMaskParams");
        public static readonly int _Clip = Shader.PropertyToID("_Clip");
        public static readonly int _FinalOutput = Shader.PropertyToID("_FinalOutput");
        public static readonly int _SourceInput = Shader.PropertyToID("_SourceInput");
        public static readonly int _Passes = Shader.PropertyToID("_Passes");
        public static readonly int _Jump = Shader.PropertyToID("_Jump");
        public static readonly int _ColorDistanceParams = Shader.PropertyToID("_ColorDistanceParams");
    }
}
