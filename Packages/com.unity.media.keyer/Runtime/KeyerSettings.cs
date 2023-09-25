using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Media.Keyer
{
    /// <summary>
    /// The available segmentation algorithms.
    /// </summary>
    public enum SegmentationAlgorithm : byte
    {
        /// <summary>
        /// Color Difference based on the classic (G - max(R, B)) formula.
        /// </summary>
        ColorDifference = 0,
        /// <summary>
        /// Color distance evaluated in a perceptually homogeneous color space.
        /// </summary>
        ColorDistance = 1
    }

    /// <summary>
    /// The solid background color for keying.
    /// </summary>
    public enum BackgroundChannel
    {
        /// <summary>
        /// Green screen background.
        /// </summary>
        Green = 0,
        /// <summary>
        /// Blue screen background.
        /// </summary>
        Blue = 1,
    }

    /// <summary>
    /// Class that holds the settings for the Keyer.
    /// </summary>
    [Serializable]
    [CreateAssetMenu(fileName = "KeyerSettings", menuName = "Virtual Production/KeyerSettings")]
    public class KeyerSettings : ScriptableObject
    {
        enum Versions
        {
            Initial = 0
        }

        [SerializeField, HideInInspector]
#pragma warning disable CS0414 // The field 'KeyerSettings.version' is assigned but its value is never used
        int m_Version = (int)Versions.Initial;
#pragma warning restore CS0414

        [SerializeField]
        SegmentationAlgorithm m_SegmentationAlgorithmCore;

        /// <summary>
        /// The segmentation algorithm used for the core mask, either Color Difference or Color Distance.
        /// </summary>
        public SegmentationAlgorithm SegmentationAlgorithmCore
        {
            get => m_SegmentationAlgorithmCore;
            set => m_SegmentationAlgorithmCore = value;
        }

        [SerializeField]
        SegmentationAlgorithm m_SegmentationAlgorithmSoft;

        /// <summary>
        /// The segmentation algorithm used for the soft mask, either Color Difference or Color Distance.
        /// </summary>
        public SegmentationAlgorithm SegmentationAlgorithmSoft
        {
            get => m_SegmentationAlgorithmSoft;
            set => m_SegmentationAlgorithmSoft = value;
        }

        [FormerlySerializedAs("m_CoreMask")]
        [SerializeField]
        ColorDifference m_ColorDifferenceCoreMask = new();

        /// <summary>
        /// The Color Difference Core mask property.
        /// </summary>
        public ColorDifference ColorDifferenceCoreMask => m_ColorDifferenceCoreMask;

        [FormerlySerializedAs("m_SoftMask")]
        [SerializeField]
        ColorDifference m_ColorDifferenceSoftMask = new();

        [SerializeField]
        bool m_SoftMaskEnabled;

        /// <summary>
        /// Determines whether or not the soft mask is active.
        /// </summary>
        public bool SoftMaskEnabled
        {
            get => m_SoftMaskEnabled;
            set => m_SoftMaskEnabled = value;
        }

        /// <summary>
        /// The Color Difference Soft mask property.
        /// </summary>
        public ColorDifference ColorDifferenceSoftMask => m_ColorDifferenceSoftMask;

        [SerializeField]
        ColorDistance m_ColorDistanceCoreMask = new();

        /// <summary>
        /// The Color Distance Core mask property.
        /// </summary>
        public ColorDistance ColorDistanceCoreMask => m_ColorDistanceCoreMask;

        [SerializeField]
        ColorDistance m_ColorDistanceSoftMask = new();

        /// <summary>
        /// The Color Distance Soft mask property.
        /// </summary>
        public ColorDistance ColorDistanceSoftMask => m_ColorDistanceSoftMask;

        [SerializeField]
        Despill m_Despill = new Despill() { Enabled = true };

        /// <summary>
        /// The Despill property used to remove the green spill from the original front image.
        /// </summary>
        public Despill Despill => m_Despill;

        [SerializeField]
        GarbageMask m_GarbageMask = new GarbageMask { Enabled = false };

        /// <summary>
        /// The Garbage mask property.
        /// </summary>
        public GarbageMask GarbageMask => m_GarbageMask;

        [SerializeField]
        Blur m_BlurMask = new Blur { Enabled = false };

        /// <summary>
        /// The Blur mask property.
        /// </summary>
        internal Blur BlurMask => m_BlurMask;

        [SerializeField]
        Clip m_ClipMask = new Clip { Enabled = false };

        /// <summary>
        /// The Clip mask property.
        /// </summary>
        internal Clip ClipMask => m_ClipMask;

        [SerializeField]
        Erode m_ErodeMask = new Erode { Enabled = false };

        /// <summary>
        /// The Erode mask property.
        /// </summary>
        public Erode ErodeMask => m_ErodeMask;

        [SerializeField]
        Crop m_CropMask = new Crop { Enabled = false };

        /// <summary>
        /// The Crop mask property.
        /// </summary>
        public Crop CropMask => m_CropMask;

        [SerializeField]
        BlendMaxNode m_BlendMask = new BlendMaxNode { Enabled = false };

        /// <summary>
        /// The Blend mask property.
        /// </summary>
        public BlendMaxNode BlendMask => m_BlendMask;

        /// <summary>
        /// Get a hash code for the current settings.
        /// </summary>
        /// <returns>Hash code for the current settings.</returns>
        public int GetSerializedFieldsHashCode()
        {
            unchecked
            {
                var hashCode = SegmentationAlgorithmCore.GetHashCode();
                hashCode = (hashCode * 397) ^ SegmentationAlgorithmSoft.GetHashCode();
                hashCode = (hashCode * 397) ^ SoftMaskEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ ColorDifferenceCoreMask.GetSerializedFieldsHashCode();
                hashCode = (hashCode * 397) ^ ColorDifferenceSoftMask.GetSerializedFieldsHashCode();
                hashCode = (hashCode * 397) ^ ColorDistanceCoreMask.GetSerializedFieldsHashCode();
                hashCode = (hashCode * 397) ^ ColorDistanceCoreMask.GetSerializedFieldsHashCode();
                hashCode = (hashCode * 397) ^ Despill.GetSerializedFieldsHashCode();
                hashCode = (hashCode * 397) ^ GarbageMask.GetSerializedFieldsHashCode();
                hashCode = (hashCode * 397) ^ BlurMask.GetSerializedFieldsHashCode();
                hashCode = (hashCode * 397) ^ ClipMask.GetSerializedFieldsHashCode();
                hashCode = (hashCode * 397) ^ ErodeMask.GetSerializedFieldsHashCode();
                hashCode = (hashCode * 397) ^ CropMask.GetSerializedFieldsHashCode();
                hashCode = (hashCode * 397) ^ BlendMask.GetSerializedFieldsHashCode();
                return hashCode;
            }
        }
    }
}
