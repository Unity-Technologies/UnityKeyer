using System;
using UnityEngine;

namespace Unity.Media.Keyer.GraphicsTests
{
    /// <summary>
    /// Settings to control how image comparison is performed by <c>ImageAssert.</c>
    /// </summary>
    [Serializable]
    public class ImageComparisonSettings : MonoBehaviour
    {
        /// <summary>
        /// The permitted perceptual difference between individual pixels of the images.
        /// The deltaE for each pixel of the image is compared and any differences below this
        /// threshold are ignored.
        /// </summary>
        [Tooltip("The permitted perceptual difference between individual pixels of the images.")]
        public float PerPixelCorrectnessThreshold;

        /// <summary>
        /// The permitted difference between the RGB components (in gamma) of individual pixels of the images.
        /// </summary>
        [Tooltip("The permitted difference between the RGB components (in gamma) of individual pixels of the images.")]
        public float PerPixelGammaThreshold = 1f / 255;

        /// <summary>
        /// The permitted difference between the alpha component of individual pixels of the images.
        /// </summary>
        [Tooltip("The permitted difference between the alpha component of individual pixels of the images.")]
        public float PerPixelAlphaThreshold = 1f / 255;

        /// <summary>
        /// The maximum permitted average error value across the entire image. If the average
        /// per-pixel difference across the image is above this value, the images are considered
        /// not to be equal.
        /// </summary>
        [Tooltip("The maximum permitted average error value across the entire image.")]
        public float AverageCorrectnessThreshold;

        /// <summary>
        /// The maximum ratio of pixels allowed to be incorrect across the image. A pixel is
        /// incorrect if it exceeds the specified per-pixel thresholds.
        /// </summary>
        [Tooltip("The maximum ratio of pixels allowed to be incorrect across the image.")]
        public float IncorrectPixelsThreshold = 1f / 512 / 512;

        /// <summary>
        /// Determines which tests are active when comparing the images.
        /// </summary>
        [Tooltip("Determines which tests are active when comparing the images.")]
        public ImageTests ActiveImageTests = ImageTests.AverageDeltaE;
        [Flags]
        public enum ImageTests
        {
            None = 0,
            AverageDeltaE = 1 << 0,
            IncorrectPixelsCount = 1 << 1,
            RMSE = 1 << 2
        }

        /// <summary>
        /// Determines which tests are active when determining whether an individual pixel is
        /// correct or not. An incorrect pixel will increase the counter associated with the
        /// IncorrectPixelsCount image test. This is only relevant when ActiveImageTests has
        /// the IncorrectPixelsCount flag set.
        /// </summary>
        [Tooltip("Determines which tests affect the counter used by the IncorrectPixelsCount image test.")]
        public PixelTests ActivePixelTests = PixelTests.DeltaE | PixelTests.DeltaAlpha | PixelTests.DeltaGamma;
        [Flags]
        public enum PixelTests
        {
            None = 0,
            DeltaE = 1 << 0,
            DeltaAlpha = 1 << 1,
            DeltaGamma = 1 << 2
        }
    }
}
