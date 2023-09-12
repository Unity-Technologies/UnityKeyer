using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    /// <summary>
    /// Class for the persistent data of the Erode Node of the Keyer.
    /// </summary>
    [Serializable]
    public class Erode
    {
        [SerializeField]
        bool m_Enabled;
        /// <summary>
        /// The amount of erosion from 0 to 29.5.
        /// </summary>
        [Range(0.0f, 29.5f)]
        [SerializeField]
        float m_Amount = 1.0f;

        /// <summary>
        /// Enables the Erode node.
        /// </summary>
        public bool Enabled
        {
            get => m_Enabled;
            set => m_Enabled = value;
        }

        /// <summary>
        /// The amount of erosion from 0 to 29.5.
        /// </summary>
        public float Amount
        {
            get => m_Amount;
            set => m_Amount = value;
        }

        internal int GetSerializedFieldsHashCode()
        {
            unchecked
            {
                var hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ Amount.GetHashCode();
                return hashCode;
            }
        }
    }
}
