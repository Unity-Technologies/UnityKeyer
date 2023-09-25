using System;
using UnityEngine;

namespace Unity.Media.Keyer.Samples.ExtractKeyingColorDemo
{
    [ExecuteAlways]
    public class ConfigureNoiseMaterial : MonoBehaviour
    {
        static class ShaderIDs
        {
            public static readonly int _NoiseTexture = Shader.PropertyToID("_NoiseTexture");
            public static readonly int _NoiseParams = Shader.PropertyToID("_NoiseParams");
        }

        const float k_MinUvScale = 0.01f;
        const float k_MaxUvScale = 1;

        [SerializeField]
        Material m_NoiseCustomPassMaterial;

        [SerializeField, Range(k_MinUvScale, k_MaxUvScale)]
        float m_UvScale;

        [SerializeField, Range(0, 1)]
        float m_Amount;

        [SerializeField, Range(0, 1)]
        float m_Offset;

        [SerializeField]
        Texture m_NoiseTexture;

        public float Amount
        {
            set
            {
                m_Amount = Mathf.Clamp01(value);
                UpdateMaterial();
            }
        }

        public float Offset
        {
            set
            {
                m_Offset = Mathf.Clamp01(value);
                UpdateMaterial();
            }
        }

        public float UvScale
        {
            set
            {
                m_UvScale = Mathf.Clamp(value, k_MinUvScale, k_MaxUvScale);
                UpdateMaterial();
            }
        }

        void OnEnable()
        {
            UpdateMaterial();
        }

        void OnValidate()
        {
            UpdateMaterial();
        }

        void UpdateMaterial()
        {
            if (m_NoiseCustomPassMaterial == null)
            {
                return;
            }

            m_NoiseCustomPassMaterial.SetTexture(ShaderIDs._NoiseTexture, m_NoiseTexture);
            m_NoiseCustomPassMaterial.SetVector(ShaderIDs._NoiseParams, new Vector3(m_UvScale, m_Amount, m_Offset));
        }
    }
}
