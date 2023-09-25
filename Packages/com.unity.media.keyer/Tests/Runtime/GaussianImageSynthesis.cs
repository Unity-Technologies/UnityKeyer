using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Media.Keyer.Tests
{
    // This component allows us to generate an image based on a collection of user controlled clusters.
    // It is meant to be used to generate testing data to be consumed in ClusteringTests.
    [ExecuteAlways]
    class GaussianImageSynthesis : MonoBehaviour
    {
        // Clusters are described by their Centroid (a color) and the eigenvectors/eigenvalues of their covariance matrices.
        // Eigenvectors are the XYZ basis vectors, rotated.
        [Serializable]
        struct Cluster
        {
            const float k_MaxRange = 0.02f;
            public Color Centroid;
            public float3 EigenVectorsRotation;
            [Range(0, k_MaxRange)]
            public float EigenValueX;
            [Range(0, k_MaxRange)]
            public float EigenValueY;
            [Range(0, k_MaxRange)]
            public float EigenValueZ;

            public float3 Scale => new(EigenValueX, EigenValueY, EigenValueZ);

            public float3x3 GetCovariance() => AlgebraUtil.GetCovariance(math.radians(EigenVectorsRotation), Scale);
        }

        // Base directory to export Numpy data to, outside of Assets.
        // Meant to be consumed by the python visualization script.
        const string k_ClustersOutputDirectory = "TestResults";

        [SerializeField]
        Cluster[] m_Clusters;

        [Tooltip("Rotation of the gizmo-based visualization of clusters.")]
        [SerializeField, Range(0, 1)]
        float m_GizmoRotation;

        [Tooltip("Directory to export Numpy data to.")]
        [SerializeField]
        string m_SubDirectory;

        [Tooltip("The generated image size.")]
        [SerializeField]
        int m_ImageSize;

        [Tooltip("The test data collection to append current data to.")]
        [SerializeField]
        GaussianDistribution m_TestData;

        // Crude visualization of clusters, meant to be visually compared to the Python visualization built using Plotly.
        void OnDrawGizmos()
        {
            if (m_Clusters == null || m_Clusters.Length == 0)
            {
                return;
            }

            // Match external (Python Plotly) visualization basis.
            var basisChange = Matrix4x4.identity;
            basisChange.SetColumn(1, (Vector3)AlgebraUtil.AxisX);
            basisChange.SetColumn(0, (Vector3)AlgebraUtil.AxisZ * -1);
            basisChange.SetColumn(2, (Vector3)AlgebraUtil.AxisY);

            var rotate = Matrix4x4.Rotate(Quaternion.AngleAxis(m_GizmoRotation * 360, Vector3.up));
            var translate = Matrix4x4.Translate(new Vector3(.5f, 0, -.5f));
            var view = translate * rotate * translate.inverse;

            var gizmoMatrix = transform.localToWorldMatrix * (view * basisChange);

            Gizmos.matrix = gizmoMatrix;
            Gizmos.color = Color.gray;
            Gizmos.DrawWireCube(Vector3.one * .5f, Vector3.one);

            foreach (var cluster in m_Clusters)
            {
                Gizmos.matrix = gizmoMatrix * GetEllipsoidTransform(cluster);

                // Draw eigenvectors. X, Y and Z.
                // Colors match unity gizmo convention.
                Gizmos.color = Color.red;
                Gizmos.DrawLine(Vector3.zero, AlgebraUtil.AxisX * 3);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(Vector3.zero, AlgebraUtil.AxisY * 3);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(Vector3.zero, AlgebraUtil.AxisZ * 3);

                Gizmos.color = cluster.Centroid;
                Gizmos.DrawSphere(Vector3.zero, 1);
                Gizmos.DrawWireSphere(Vector3.zero, 3);
            }
        }

        void OnValidate()
        {
            m_ImageSize = Mathf.Clamp(m_ImageSize, 256, 1024);

            if (m_Clusters == null || m_Clusters.Length == 0)
            {
                return;
            }

            for (var i = 0; i != m_Clusters.Length; ++i)
            {
                // Clamp centroids since if they get too close to the RGB edges the distribution of samples gets clamped,
                // preventing us from properly reconstructing the clusters later on.
                var one = new float3(1, 1, 1);
                m_Clusters[i].Centroid = math.clamp(m_Clusters[i].Centroid.ToFloat3(), one * .25f, one * .75f).ToColor();
            }
        }

        // Generates a .png image whose pixels are drawn from the multivariate Gaussian distribution described by the clusters.
        void GenerateImage(string path, int width, int height)
        {
            var totalSamples = width * height;
            var totalBlocks = totalSamples / 32;
            var pixels = new NativeArray<Color32>(totalSamples, Allocator.TempJob);

            // First we need to determine which space within the image will be allocated to each cluster.
            var weights = new NativeArray<float>(m_Clusters.Length, Allocator.Temp);

            for (var i = 0; i != m_Clusters.Length; ++i)
            {
                var scale = m_Clusters[i].Scale;

                // We can gauge "volume" this way because we *know* we are working with orthogonal eigenvectors.
                weights[i] = scale.x * scale.y * scale.z;
            }

            var blockCount = ComputeBlockCounts(weights, totalBlocks);
            var blockIndex = 0;

            // The we draw samples from each cluster.
            for (var i = 0; i != m_Clusters.Length; ++i)
            {
                var numBlocks = blockCount[i];

                GaussianSampler.DrawSamples(
                    m_Clusters[i].Centroid.ToFloat3(),
                    m_Clusters[i].GetCovariance(), pixels,
                    blockIndex * GaussianSampler.BlockSize,
                    numBlocks * GaussianSampler.BlockSize);
                blockIndex += numBlocks;
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixelData(pixels, 0);
            var pngBytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, pngBytes);

            Utilities.Destroy(tex);
            pixels.Dispose();
        }

        [ContextMenu("Append Test Case")]
        void AppendTestCase()
        {
            if (m_TestData == null)
            {
                Debug.LogError($"{nameof(m_TestData)} must not be null.");
                return;
            }

            Assert.IsTrue(m_Clusters != null && m_Clusters.Length > 0);

            var centroids = new float3[m_Clusters.Length];
            var covariances = new float3x3[m_Clusters.Length];
            for (var i = 0; i != m_Clusters.Length; ++i)
            {
                centroids[i] = m_Clusters[i].Centroid.ToFloat3();
                covariances[i] = m_Clusters[i].GetCovariance();
            }

            var image = EditorGenerateImage();
            Assert.IsNotNull(image);

            m_TestData.Append(new GaussianDistribution.Cluster
            {
                Centroids = centroids,
                Covariances = covariances,
                Image = image
            });
        }

        [ContextMenu("Generate Image")]
        Texture EditorGenerateImage()
        {
#if UNITY_EDITOR
            var path = EditorUtility.SaveFilePanel(
                "Save as PNG", Application.dataPath, "generated.png", "png");

            if (String.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("Empty path.");
            }

            GenerateImage(path, m_ImageSize, m_ImageSize);

            // We only attempt reimport if we saved in Assets.
            var relativePath = Path.GetRelativePath(Application.dataPath, path);
            if (!relativePath.Contains(".."))
            {
                var assetPath = Path.Combine("Assets", relativePath);
                AssetDatabase.ImportAsset(assetPath);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.sRGBTexture = false;
                    importer.mipmapEnabled = false;

                    // As the generated image has a lot of high frequency information,
                    // it is crucial to turn compression off.
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }

                return AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
            }

            return null;
#else
            Debug.LogError("Cannot generate image outside of the Editor.");
            return null;
#endif
        }

        [ContextMenu("Numpy Export Clusters")]
        void NumpyExportClusters()
        {
            if (m_Clusters == null || m_Clusters.Length == 0)
            {
                Debug.LogError("Empty clusters. No output written.");
                return;
            }

            var basePath = Utilities.GetAbsoluteOutputDirectory(k_ClustersOutputDirectory, m_SubDirectory);

            // Write centroids.
            {
                var path = Path.Combine(basePath, "ref_covariances.txt");
                using var writer = new StreamWriter(path);
                foreach (var item in m_Clusters)
                {
                    NumpyExport.Write(writer, item.GetCovariance());
                }
            }

            // Write covariances.
            {
                var path = Path.Combine(basePath, "ref_centroids.txt");
                using var writer = new StreamWriter(path);
                foreach (var item in m_Clusters)
                {
                    NumpyExport.Write(writer, item.Centroid.ToVector3());
                }
            }
        }

        static Matrix4x4 GetEllipsoidTransform(Cluster cluster)
        {
            return Matrix4x4.TRS(
                new Vector3(
                    cluster.Centroid.r,
                    cluster.Centroid.g,
                    cluster.Centroid.b),
                Quaternion.Euler(cluster.EigenVectorsRotation), math.sqrt(cluster.Scale));
        }

        // Evaluate the number of 32px blocks allocated to each cluster, based on its eigenvalues.
        static NativeArray<int> ComputeBlockCounts(NativeArray<float> weights, int totalBlocks)
        {
            // Calculate the number of samples per cluster.
            var blockCount = new NativeArray<int>(weights.Length, Allocator.Temp);
            var sum = 0f;
            for (var i = 0; i != weights.Length; ++i)
            {
                sum += weights[i];
            }

            var remainingBlocks = totalBlocks;
            for (var i = 0; i != weights.Length; ++i)
            {
                var blocks = (int)math.floor(totalBlocks * weights[i] / sum);
                remainingBlocks -= blocks;
                blockCount[i] = blocks;
            }

            blockCount[^1] += remainingBlocks;
            return blockCount;
        }
    }
}
