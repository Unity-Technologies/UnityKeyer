using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Media.Keyer.Tests
{
    [CreateAssetMenu(fileName = "GaussianDistribution", menuName = "GaussianDistribution")]
    public class GaussianDistribution : ScriptableObject
    {
        [Serializable]
        public struct Cluster
        {
            [SerializeField]
            public float3x3[] Covariances;
            [SerializeField]
            public float3[] Centroids;
            [SerializeField]
            public Texture Image;
        }

        [SerializeField]
        List<Cluster> m_Clusters;

        public IReadOnlyList<Cluster> Clusters => m_Clusters;

        public void Append(Cluster cluster)
        {
            if (m_Clusters == null)
            {
                m_Clusters = new List<Cluster>();
            }
            m_Clusters.Add(cluster);

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}
