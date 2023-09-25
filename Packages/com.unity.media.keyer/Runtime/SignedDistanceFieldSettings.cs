using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    /// <summary>
    /// The quality level of the SDF rendering.
    /// </summary>
    public enum SdfQuality : byte
    {
        /// <summary>
        /// Low quality, suited for low distances and performance critical scenarios.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Medium quality, good balance between performance and quality.
        /// </summary>
        Mid = 1,

        /// <summary>
        /// High quality, best suited for large distances.
        /// </summary>
        High = 2
    }

    partial class SignedDistanceField
    {
        [Serializable]
        public struct Settings
        {
            const int k_MinLods = 0;
            const int k_MaxLods = 8;
            const int k_MinScale = 0;
            const int k_MaxScale = GarbageMask.k_MaxDistance;
            const int k_MinSteps = 0;
            const int k_MaxSteps = 64;
            const int k_MinBlurRadius = 2;
            const int k_MaxBlurRadius = 32;
            const int k_MinBlurSampleCount = 2;
            const int k_MaxBlurSampleCount = 24;
            const int k_MinGroupsSharedPasses = 1;
            const int k_MaxGroupsSharedPasses = k_GroupSize;

            [Range(k_MinScale, k_MaxScale)]
            public float Scale;

            [Range(k_MinSteps, k_MaxSteps)]
            public int Steps;

            public bool UseSkipLodSteps;

            [Range(0, 1)]
            public float SkipLodSteps;

            // We may decide to cap min lod for fast render of wide SDF.
            [Range(k_MinLods, k_MaxLods - 1)]
            public int MinLods;

            [Range(k_MinLods + 1, k_MaxLods)]
            public int MaxLods;

            public int[] StepsPerLod;

            public bool UseGroupsShared;

            [Range(k_MinGroupsSharedPasses, k_MaxGroupsSharedPasses)]
            public int GroupsSharedPasses;

            public bool UseBlur;

            [Range(k_MinBlurRadius, k_MaxBlurRadius)]
            public float BlurRadius;

            [Range(k_MinBlurSampleCount, k_MaxBlurSampleCount)]
            public int BlurSampleCount;

            // Attributes do not provide validation, we need to do it explicitly.
            public void Validate()
            {
                if (StepsPerLod == null || StepsPerLod.Length != MaxLods - MinLods)
                {
                    StepsPerLod = new int[MaxLods - MinLods];
                }

                SkipLodSteps = Mathf.Clamp01(SkipLodSteps);

                if (UseSkipLodSteps)
                {
                    for (var i = 0; i != StepsPerLod.Length; ++i)
                    {
                        StepsPerLod[i] = (int)(Steps * Mathf.Pow(2, -SkipLodSteps * i));
                    }
                }

                MaxLods = Mathf.Clamp(MaxLods, k_MinLods + 1, k_MaxLods);
                MinLods = Mathf.Clamp(MinLods, k_MinLods, MaxLods - 1);

                Scale = Mathf.Clamp(Scale, k_MinScale, k_MaxScale);
                Steps = Mathf.Clamp(Steps, k_MinSteps, k_MaxSteps);
                GroupsSharedPasses = Mathf.Clamp(GroupsSharedPasses, k_MinGroupsSharedPasses, k_MaxGroupsSharedPasses);
                BlurRadius = Mathf.Clamp(BlurRadius, k_MinBlurRadius, k_MaxBlurRadius);
                BlurSampleCount = Mathf.Clamp(BlurSampleCount, k_MinBlurSampleCount, k_MaxBlurSampleCount);
            }

            public static Settings Get(SdfQuality quality, int distance)
            {
                var settings = new Settings
                {
                    Scale = distance,
                    UseGroupsShared = false,
                    UseSkipLodSteps = true,
                    MinLods = 0,
                    UseBlur = true
                };

                switch (quality)
                {
                    case SdfQuality.Low:
                        settings.Steps = 4;
                        settings.MaxLods = 4;
                        settings.SkipLodSteps = 1;
                        settings.BlurSampleCount = 5;
                        settings.BlurRadius = 2;
                        break;
                    case SdfQuality.Mid:
                        settings.Steps = 4;
                        settings.MaxLods = 8;
                        settings.SkipLodSteps = .5f;
                        settings.BlurSampleCount = 9;
                        settings.BlurRadius = 4;
                        break;
                    case SdfQuality.High:
                        settings.Steps = 8;
                        settings.MaxLods = 8;
                        settings.SkipLodSteps = 0;
                        settings.BlurSampleCount = 9;
                        settings.BlurRadius = 4;
                        break;
                }

                settings.Validate();
                return settings;
            }
        }
    }
}
