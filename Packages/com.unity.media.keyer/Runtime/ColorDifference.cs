using System;
using UnityEngine;

namespace Unity.Media.Keyer
{
    /// <summary>
    /// Class for the persistent data of the Color Difference Keyer Node.
    /// </summary>
    [Serializable]
    public class ColorDifference
    {
        [SerializeField, Obsolete]
        bool m_Enabled;

        [SerializeField]
        BackgroundChannel m_BackgroundColor = BackgroundChannel.Green;
        [SerializeField]
        Vector3 m_Scale = new(5, 5, 2.5f);
        [Range(0.0f, 1.0f)]
        [SerializeField, Tooltip("Set a maximum threshold for the Core Mask white values. All values over the threshold are opaques.")]
        float m_ClipWhite = 1.0f;
        [Range(0.0f, 1.0f), Tooltip("Set a minimum threshold for the Core Mask black values. All values under the threshold are transparents.")]
        [SerializeField]
        float m_ClipBlack;

        /// <summary>
        /// The solid background color.
        /// </summary>
        public BackgroundChannel BackgroundColor
        {
            get => m_BackgroundColor;
            set => m_BackgroundColor = value;
        }

        /// <summary>
        /// Scales the RGB components of the input color prior to evaluating the (G - max(R, B)) formula.
        /// </summary>
        public Vector3 Scale
        {
            get => m_Scale;
            set => m_Scale = value;
        }

        /// <summary>
        /// Clips the white part of the mask.
        /// </summary>
        public float ClipWhite
        {
            get => m_ClipWhite;
            set => m_ClipWhite = value;
        }

        /// <summary>
        /// Clips the black part of the mask.
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
                var hashCode = (int)BackgroundColor;
                hashCode = (hashCode * 397) ^ Scale.GetHashCode();
                hashCode = (hashCode * 397) ^ ClipWhite.GetHashCode();
                hashCode = (hashCode * 397) ^ ClipBlack.GetHashCode();
                return hashCode;
            }
        }
    }
}
