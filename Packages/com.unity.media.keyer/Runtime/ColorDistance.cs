using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    /// <summary>
    /// Class for the persistent data of the Color Distance segmentation algorithm.
    /// </summary>
    [Serializable]
    public class ColorDistance
    {
        [SerializeField, Obsolete]
        bool m_Enabled;

        /// <summary>
        /// The color of the key representing the background.
        /// </summary>
        [SerializeField]
        Color m_ChromaKey = Color.green;

        /// <summary>
        /// The threshold used to separate background and foreground.
        /// </summary>
        [SerializeReference]
        Vector2 m_Threshold = new(.1f, .9f);

        /// <summary>
        /// The color of the key representing the background.
        /// </summary>
        public Color ChromaKey
        {
            get => m_ChromaKey;
            set => m_ChromaKey = value;
        }

        /// <summary>
        /// The threshold used to separate the background and the foreground.
        /// </summary>
        public Vector2 Threshold
        {
            get => m_Threshold;
            set => m_Threshold = value;
        }

        internal int GetSerializedFieldsHashCode()
        {
            unchecked
            {
                var hashCode = ChromaKey.GetHashCode();
                hashCode = (hashCode * 397) ^ Threshold.GetHashCode();
                return hashCode;
            }
        }
    }
}
