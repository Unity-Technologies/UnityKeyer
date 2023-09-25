using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    /// <summary>
    /// Class for the persistent data of the Despill Keyer Node.
    /// </summary>
    [Serializable]
    public class Despill
    {
        [SerializeField]
        bool m_Enabled;
        [SerializeField]
        BackgroundChannel m_BackgroundColor = BackgroundChannel.Green;
        [Range(0.0f, 1.0f)]
        [SerializeField, Tooltip("Apply the Despill algorithm to the foreground image. The range is from 0 for no Despill to 1 for a full application.")]
        float m_DespillAmount = 1f;

        /// <summary>
        /// The solid background color.
        /// </summary>
        public BackgroundChannel BackgroundColor
        {
            get => m_BackgroundColor;
            set => m_BackgroundColor = value;
        }

        /// <summary>
        /// Enables the Despill node.
        /// </summary>
        public bool Enabled
        {
            get => m_Enabled;
            set => m_Enabled = value;
        }

        /// <summary>
        /// The Despill amount ranging from 0 (no despill) to 1 (full despill).
        /// </summary>
        public float DespillAmount
        {
            get => m_DespillAmount;
            set => m_DespillAmount = value;
        }

        internal int GetSerializedFieldsHashCode()
        {
            unchecked
            {
                var hashCode = Enabled.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)BackgroundColor;
                hashCode = (hashCode * 397) ^ DespillAmount.GetHashCode();
                return hashCode;
            }
        }
    }
}
