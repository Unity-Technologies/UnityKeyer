using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    /// <summary>
    /// Sets the blur quality of the Blur Node.
    /// </summary>
    public enum BlurQuality : byte
    {
        /// <summary>
        /// Low quality blur.
        /// </summary>
        Low = 4,
        /// <summary>
        /// Medium quality blur.
        /// </summary>
        Medium = 8,
        /// <summary>
        /// High quality blur.
        /// </summary>
        High = 16
    }

    /// <summary>
    /// Class for the persistent data of the Blur Node of the Keyer.
    /// </summary>
    [Serializable]
    public class Blur
    {
        [SerializeField]
        bool m_Enabled;
        [Range(0, 30)]
        [SerializeField]
        float m_Radius;
        [SerializeField]
        BlurQuality m_Quality = BlurQuality.High;

        /// <summary>
        /// Enables the Blur node.
        /// </summary>
        public bool Enabled
        {
            get => m_Enabled;
            set => m_Enabled = value;
        }

        /// <summary>
        /// The Radius of the blur from 0 to 30.
        /// </summary>
        public float Radius
        {
            get => m_Radius;
            set => m_Radius = value;
        }

        /// <summary>
        /// The level of Blur quality from low to high.
        /// </summary>
        public BlurQuality Quality
        {
            get => m_Quality;
            set => m_Quality = value;
        }

        internal int GetSerializedFieldsHashCode()
        {
            unchecked
            {
                var hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ Radius.GetHashCode();
                hashCode = (hashCode * 397) ^ Quality.GetHashCode();
                return hashCode;
            }
        }
    }
}
