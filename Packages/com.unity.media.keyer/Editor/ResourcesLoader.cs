using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace Unity.Media.Keyer.Editor
{
    // Using ScriptableSingleton so that we can have data surviving domain reload.
    // We use it to minimize the workload.
    class ResourcesLoader : ScriptableSingleton<ResourcesLoader>
    {
        // Using an AssetPostprocessor allows us to create resources once all required assets have been imported.
        class LoadCallback : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                if (!instance.m_LoadedResources)
                {
                    // Prevent infinite import loop.
                    instance.m_LoadedResources = true;
                    instance.LoadResources();
                }
            }
        }

        const string k_BaseComputeShaderPath = "Packages/com.unity.media.keyer/ShaderLibrary/";
        const string k_ResourceName = nameof(KeyerResources);
        const string k_AssetsName = "Assets";
        const string k_ResourcesName = "Resources";

        static readonly string k_ResourcesPath = Path.Combine(k_AssetsName, k_ResourcesName);
        static readonly string k_AssetPath = Path.Combine(k_ResourcesPath, $"{k_ResourceName}.asset");

        static readonly Type k_ComputeShaderType = typeof(ComputeShader);
        static readonly Type k_ShaderType = typeof(Shader);

        readonly Dictionary<string, ComputeShader> m_ComputeShadersByName = new();

        // Lifetime is a Unity session.
        // We should only have to load resources once.
        [SerializeField]
        bool m_LoadedResources;

        public static string AssetPath => k_AssetPath;

        // This should happen automatically, but we offer a manual version in case we modify our resources.
#if DEBUG_ADVANCED
        [MenuItem(KeyerResources.MenuEntry)]
#endif
        public static void LoadResourcesMenu()
        {
            // Prevent infinite import loop.
            instance.m_LoadedResources = true;
            instance.LoadResources();
        }

        void LoadResources()
        {
            var resources = Resources.Load<KeyerResources>(k_ResourceName);
            if (resources == null)
            {
                resources = CreateInstance<KeyerResources>();
                LoadResources(resources, m_ComputeShadersByName);

                if (!AssetDatabase.IsValidFolder(k_ResourcesPath))
                {
                    AssetDatabase.CreateFolder(k_AssetsName, k_ResourcesName);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                AssetDatabase.CreateAsset(resources, k_AssetPath);
            }
            else
            {
                LoadResources(resources, m_ComputeShadersByName);
            }

            EditorUtility.SetDirty(resources);
            AssetDatabase.SaveAssetIfDirty(resources);
        }

        static void LoadResources(KeyerResources resources, IDictionary<string, ComputeShader> computeShadersByName)
        {
            LoadShaders(resources.Shaders, computeShadersByName);
            LoadKernelIds(resources.KernelIds.Rendering, computeShadersByName);
            LoadKernelIds(resources.KernelIds.KeyExtract, computeShadersByName);
            LoadKernelIds(resources.KernelIds.SignedDistanceField, computeShadersByName);
            LoadKernelIds(resources.KernelIds.ExpectationMaximization, computeShadersByName);
        }

        static void LoadShaders(Shaders resources, IDictionary<string, ComputeShader> computeShadersByName)
        {
            computeShadersByName.Clear();

            foreach (var field in typeof(Shaders).GetFields())
            {
                if (field.FieldType == k_ComputeShaderType)
                {
                    var pathAttribute = field.GetCustomAttribute<ShaderNameAttribute>();
                    Assert.IsNotNull(pathAttribute, $"{nameof(ShaderNameAttribute)} expected on field {field.Name}.");

                    var shader = LoadAsset<ComputeShader>($"{k_BaseComputeShaderPath}{pathAttribute.Name}.compute");
                    field.SetValue(resources, shader);

                    computeShadersByName.Add(pathAttribute.Name, shader);
                }
                else if (field.FieldType == k_ShaderType)
                {
                    var nameAttribute = field.GetCustomAttribute<ShaderNameAttribute>();
                    Assert.IsNotNull(nameAttribute, $"{nameof(ShaderNameAttribute)} expected on field {field.Name}.");

                    var shader = LoadShader(nameAttribute.Name);
                    field.SetValue(resources, shader);
                }
            }
        }

        static void LoadKernelIds<T>(T kernelIds, IDictionary<string, ComputeShader> computeShaders)
        {
            static string GetName(MemberInfo field)
            {
                var nameAttribute = field.GetCustomAttribute<ShaderNameAttribute>();
                Assert.IsNotNull(nameAttribute, $"{nameof(ShaderNameAttribute)} expected on field {field.Name}.");
                return nameAttribute.Name;
            }

            ComputeShader GetComputeShader(string name)
            {
                computeShaders.TryGetValue(name, out var shader);
                Assert.IsNotNull(shader, $"Could not find compute shader \"{name}\"");
                return shader;
            }

            // Note: we may run into scaling issues if we'd like to reuse kernel names,
            // or have multiple arrays per shader. If so, we'll add more attribute properties.
            foreach (var field in typeof(T).GetFields())
            {
                if (field.FieldType == typeof(int))
                {
                    var name = GetName(field);

                    // The attribute lets us know which shader we should find the kernel in.
                    // By convention the kernel name matches the field name.
                    var shader = GetComputeShader(name);
                    var kernelId = shader.FindKernel(field.Name);
                    field.SetValue(kernelIds, kernelId);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unexpected field type \"{field.Name}\" \"{field.FieldType}\"");
                }
            }
        }

        static T LoadAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Could not load {nameof(T)} at \"{path}\".");
            }

            return asset;
        }

        static Shader LoadShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                throw new InvalidOperationException($"Could not find shader \"{name}\"");
            }

            return shader;
        }
    }
}
