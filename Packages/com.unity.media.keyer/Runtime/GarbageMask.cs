using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Media.Keyer
{
    /// <summary>
    /// The input type of the mask.
    /// </summary>
    public enum GarbageMaskMode : byte
    {
        /// <summary>
        /// The mask is a texture.
        /// </summary>
        Texture,

        /// <summary>
        /// The mask is a rasterized polygon.
        /// </summary>
        Polygon
    }

    /// <summary>
    /// Class for the Garbage Mask Keyer Node.
    /// </summary>
    [Serializable]
    public class GarbageMask
    {
        const int k_MinDistance = 4;
        internal const int k_MaxDistance = 64;

        [SerializeField]
        bool m_Enabled;

        /// <summary>
        /// Enable to use the Garbage Mask.
        /// </summary>
        public bool Enabled
        {
            get => m_Enabled;
            set => m_Enabled = value;
        }

        [SerializeField, Tooltip("Choose either a texture or a polygon as input for the Garbage Mask.")]
        GarbageMaskMode m_Mode;

        /// <summary>
        /// Input type for the mask.
        /// </summary>
        public GarbageMaskMode Mode
        {
            get => m_Mode;
            set => m_Mode = value;
        }

        [SerializeField, Tooltip("Inverts the Garbage Mask.")]
        bool m_Invert;

        /// <summary>
        /// Inverts the Garbage Mask.
        /// </summary>
        public bool Invert
        {
            get => m_Invert;
            set => m_Invert = value;
        }

        [SerializeField, Tooltip("Sets the input texture for the Garbage Mask.")]
        Texture m_Texture;

        /// <summary>
        /// Sets the input texture for the Garbage Mask.
        /// </summary>
        public Texture Texture
        {
            get => m_Texture;
            set => m_Texture = value;
        }

        [SerializeField, Range(0, k_MaxDistance), Tooltip("Threshold distance in pixels used for dilation.")]
        float m_Threshold;

        /// <summary>
        /// Threshold distance in pixels used for dilatation.
        /// </summary>
        public float Threshold
        {
            get => m_Threshold;
            set => m_Threshold = value;
        }

        [SerializeField, Range(0, k_MaxDistance), Tooltip("Blend the edge of the Signed Distance Field from opaque to transparent.")]
        float m_Blend;

        /// <summary>
        /// Blending distance in pixels used for dilatation.
        /// </summary>
        public float Blend
        {
            get => m_Blend;
            set => m_Blend = value;
        }

        [SerializeField]
        List<Vector2> m_Points = new();

        // Were we to make this public, we would need a mechanism to detect changes to update the generated mask.
        internal List<Vector2> Points
        {
            get => m_Points;
            set => m_Points = value;
        }

        [SerializeField]
        bool m_SdfEnabled;

        /// <summary>
        /// If true, the Signed Distance Field is generated.
        /// </summary>
        public bool SdfEnabled
        {
            get => m_SdfEnabled;
            set => m_SdfEnabled = value;
        }

        [SerializeField, Tooltip("Set the quality of the Signed Distance Field algorithm from lowest to highest.")]
        SdfQuality m_SdfQuality;

        /// <summary>
        /// Quality of Signed Distance Field rendering.
        /// </summary>
        public SdfQuality SdfQuality
        {
            get => m_SdfQuality;
            set => m_SdfQuality = value;
        }

        // Must match constants in PolygonRenderer.
        [SerializeField, Range(k_MinDistance, k_MaxDistance), Tooltip("Set the maximum distance in pixels covered by the Signed Distance Field.")]
        int m_SdfDistance;

        /// <summary>
        /// Maximal distance in pixels covered by the Signed Distance Field.
        /// </summary>
        public int SdfDistance
        {
            get => m_SdfDistance;
            set => m_SdfDistance = value;
        }

        internal int GetSerializedFieldsHashCode()
        {
            unchecked
            {
                var hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ Invert.GetHashCode();
                hashCode = (hashCode * 397) ^ Mode.GetHashCode();
                if (Texture != null)
                {
                    hashCode = (hashCode * 397) ^ Texture.GetHashCode();
                }

                hashCode = (hashCode * 397) ^ Threshold.GetHashCode();
                hashCode = (hashCode * 397) ^ Blend.GetHashCode();
                if (Points != null)
                {
                    hashCode = (hashCode * 397) ^ Points.GetHashCode();
                }

                hashCode = (hashCode * 397) ^ SdfEnabled.GetHashCode();
                hashCode = (hashCode * 397) ^ SdfQuality.GetHashCode();
                hashCode = (hashCode * 397) ^ SdfDistance.GetHashCode();
                return hashCode;
            }
        }
    }
}
