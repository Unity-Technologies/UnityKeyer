using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    /// <summary>
    /// Class for the persistent data of the Clip Node of the Keyer.
    /// </summary>
    [Serializable]
    public class Clip
    {
        [SerializeField]
        bool m_Enabled;
        [Range(0.0f, 1.0f)]
        [SerializeField]
        float m_ClipWhite = 1.0f;
        [Range(0.0f, 1.0f)]
        [SerializeField]
        float m_ClipBlack = 0.0f;

        /// <summary>
        /// Enables the Clip Mask node.
        /// </summary>
        public bool Enabled
        {
            get => m_Enabled;
            set => m_Enabled = value;
        }

        /// <summary>
        /// Clips the white pixels, scales the white values, and clips all values above to 1.0f.
        /// </summary>
        public float ClipWhite
        {
            get => m_ClipWhite;
            set => m_ClipWhite = value;
        }

        /// <summary>
        /// Clips the black pixels, scales the black values, and clips all values below to 0.0f.
        /// </summary>
        public float ClipBlack
        {
            get => m_ClipBlack;
            set => m_ClipBlack = value;
        }

        internal int GetSerializedFieldsHashCode()
        {
            unchecked
            {
                var hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ ClipWhite.GetHashCode();
                hashCode = (hashCode * 397) ^ ClipBlack.GetHashCode();
                return hashCode;
            }
        }
    }
}
