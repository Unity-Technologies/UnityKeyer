using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    /// <summary>
    /// Class for the persistent data of the Crop Node of the Keyer.
    /// </summary>
    [Serializable]
    public class Crop
    {
        [SerializeField]
        bool m_Enabled;
        [Range(0.0f, 1.0f)]
        [SerializeField, Tooltip("Crop the top edge of the resulting mask. The Cropped part is made transparent.")]
        float m_Top;
        [Range(0.0f, 1.0f)]
        [SerializeField, Tooltip("Crop the bottom edge of the resulting mask. The Cropped part is made transparent.")]
        float m_Bottom;
        [Range(0.0f, 1.0f)]
        [SerializeField, Tooltip("Crop the left edge of the resulting mask. The Cropped part is made transparent.")]
        float m_Left;
        [Range(0.0f, 1.0f)]
        [SerializeField, Tooltip("Crop the right edge of the resulting mask. The Cropped part is made transparent.")]
        float m_Right;

        /// <summary>
        /// Enables the Crop node.
        /// </summary>
        public bool Enabled
        {
            get => m_Enabled;
            set => m_Enabled = value;
        }

        /// <summary>
        /// Top of the cropping region.
        /// </summary>
        public float Top
        {
            get => m_Top;
            set => m_Top = value;
        }

        /// <summary>
        /// Bottom of the cropping region.
        /// </summary>
        public float Bottom
        {
            get => m_Bottom;
            set => m_Bottom = value;
        }

        /// <summary>
        /// Left of the cropping region.
        /// </summary>
        public float Left
        {
            get => m_Left;
            set => m_Left = value;
        }

        /// <summary>
        /// Right of the cropping region.
        /// </summary>
        public float Right
        {
            get => m_Right;
            set => m_Right = value;
        }

        internal int GetSerializedFieldsHashCode()
        {
            unchecked
            {
                var hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ Top.GetHashCode();
                hashCode = (hashCode * 397) ^ Bottom.GetHashCode();
                hashCode = (hashCode * 397) ^ Left.GetHashCode();
                hashCode = (hashCode * 397) ^ Right.GetHashCode();
                return hashCode;
            }
        }
    }
}
