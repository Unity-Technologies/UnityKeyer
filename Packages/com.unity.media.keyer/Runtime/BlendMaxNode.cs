using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    /// <summary>
    /// Class for the persistent data of the BlendMax Keyer Node.
    /// </summary>
    [Serializable]
    public class BlendMaxNode
    {
        [SerializeField]
        bool m_Enabled = true;
        [Range(0, 1)]
        [SerializeField]
        float m_Strength = 1.0f;

        /// <summary>
        /// Enables the Blend Max node.
        /// </summary>
        public bool Enabled
        {
            get => m_Enabled;
            set => m_Enabled = value;
        }

        /// <summary>
        /// Strength of the blending.
        /// </summary>
        public float Strength
        {
            get => m_Strength;
            set => m_Strength = value;
        }

        internal int GetSerializedFieldsHashCode()
        {
            unchecked
            {
                var hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ Strength.GetHashCode();
                return hashCode;
            }
        }
    }
}
