using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Unity.Media.Keyer.Editor
{
    /// <summary>
    /// Class with Editor Utilities for the Keyer.
    /// </summary>
    class EditorUtilities
    {
        static class ShaderIds
        {
            // HDRP
            public static readonly int _SurfaceType = Shader.PropertyToID("_SurfaceType");
            public static readonly int _UnlitColorMap = Shader.PropertyToID("_UnlitColorMap");
            public static readonly int _DoubleSidedEnable = Shader.PropertyToID("_DoubleSidedEnable");
            public static readonly int _BlendMode = Shader.PropertyToID("_BlendMode");
            public static readonly int _RenderQueueType = Shader.PropertyToID("_RenderQueueType");
            public static readonly int _AlphaCutoffEnable = Shader.PropertyToID("_AlphaCutoffEnable");
            public static readonly int _SrcBlend = Shader.PropertyToID("_SrcBlend");
            public static readonly int _DstBlend = Shader.PropertyToID("_DstBlend");
            public static readonly int _AlphaSrcBlend = Shader.PropertyToID("_AlphaSrcBlend");
            public static readonly int _AlphaDstBlend = Shader.PropertyToID("_AlphaDstBlend");
            public static readonly int _ZTestDepthEqualForOpaque = Shader.PropertyToID("_ZTestDepthEqualForOpaque");

            // URP
            public static readonly int _Cutoff = Shader.PropertyToID("_Cutoff");
            public static readonly int _AlphaClip = Shader.PropertyToID("_AlphaClip");
            public static readonly int _BaseMap = Shader.PropertyToID("_BaseMap");
            public static readonly int _Surface = Shader.PropertyToID("_Surface");
            public static readonly int _Blend = Shader.PropertyToID("_Blend");
            public static readonly int _BlendOp = Shader.PropertyToID(("_BlendOp"));
            public static readonly int _SrcBlendAlpha = Shader.PropertyToID("_SrcBlendAlpha");
            public static readonly int _DstBlendAlpha = Shader.PropertyToID("_DstBlendAlpha");
            public static readonly int _ZWrite = Shader.PropertyToID("_ZWrite");
            public static readonly int _QueueOffset = Shader.PropertyToID("_QueueOffset");

            // Legacy
            public static readonly int _Mode = Shader.PropertyToID("_Mode");
            public static readonly int _MainTex = Shader.PropertyToID("_MainTex");
        }

        const string k_PackagePath = "Packages/com.unity.media.keyer/";
        static readonly string k_DefaultInputPath = $"{k_PackagePath}Editor/DefaultAssets/DefaultInputTexture.png";

        internal static string KeyerAssetPath => "Assets/Keyer/";

        // TODO Replace with runtime utilities.
        internal static RenderTexture CreateDefaultRenderTexture(string name, int width, int height, RenderTextureFormat format, RenderTextureReadWrite readWrite, FilterMode filterMode, int depthBits, int antiAliasing)
        {
            var rt = new RenderTexture(width, height, depthBits, format, readWrite);
            rt.name = name;
            rt.filterMode = filterMode;
            rt.antiAliasing = antiAliasing;
            rt.autoGenerateMips = true;
            return rt;
        }

        static RenderTexture CreateDefaultRWRenderTexture(string name, int width, int height, RenderTextureFormat format)
        {
            return CreateDefaultRenderTexture(name, width, height, format, RenderTextureReadWrite.Default, FilterMode.Trilinear, 32, 1);
        }

        static Material CreateKeyerMaterialHDRP(RenderTexture rt)
        {
            var keyerMaterial = new Material(Shader.Find("HDRP/Unlit"));
            keyerMaterial.name = "KeyerMaterial";
            keyerMaterial.SetTexture(ShaderIds._UnlitColorMap, rt);
            keyerMaterial.SetFloat(ShaderIds._DoubleSidedEnable, 1.0f);
            keyerMaterial.SetFloat(ShaderIds._SurfaceType, 1.0f);
            keyerMaterial.SetInt(ShaderIds._RenderQueueType, 4);
            keyerMaterial.SetFloat(ShaderIds._RenderQueueType, 5);
            keyerMaterial.SetFloat(ShaderIds._BlendMode, 0);
            keyerMaterial.SetFloat(ShaderIds._AlphaCutoffEnable, 0);
            keyerMaterial.SetFloat(ShaderIds._SrcBlend, 1f);
            keyerMaterial.SetFloat(ShaderIds._DstBlend, 10f);
            keyerMaterial.SetFloat(ShaderIds._AlphaSrcBlend, 1f);
            keyerMaterial.SetFloat(ShaderIds._AlphaDstBlend, 10f);
            keyerMaterial.SetFloat(ShaderIds._ZTestDepthEqualForOpaque, 4f);
            keyerMaterial.SetFloat(ShaderIds._DoubleSidedEnable, 1.0f);
            keyerMaterial.EnableKeyword("_BLENDMODE_ALPHA");
            keyerMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            keyerMaterial.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
            keyerMaterial.DisableKeyword("_BLENDMODE_ADD");
            keyerMaterial.DisableKeyword("_BLENDMODE_PRE_MULTIPLY");
            keyerMaterial.renderQueue = (int)RenderQueue.Transparent;

            CreateAsset(keyerMaterial, $"{KeyerAssetPath}KeyerMaterial.mat");
            return keyerMaterial;
        }

        static Material CreateKeyerShadowMaterialHDRP(RenderTexture rt)
        {
            var keyerShadowMaterial = new Material(Shader.Find("HDRP/Lit"));
            keyerShadowMaterial.name = "KeyerCutoutMaterial";
            keyerShadowMaterial.SetFloat(ShaderIds._DoubleSidedEnable, 1.0f);
            keyerShadowMaterial.SetFloat(ShaderIds._AlphaCutoffEnable, 1.0f);
            keyerShadowMaterial.SetTexture("_BaseColorMap", rt);

            CreateAsset(keyerShadowMaterial, $"{KeyerAssetPath}KeyerShadowMaterial.mat");
            return keyerShadowMaterial;
        }

        static Material CreateKeyerMaterialURP(RenderTexture rt)
        {
            var keyerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")); // Found name in URP/Shaders/Unlit.shader
            keyerMaterial.SetFloat(ShaderIds._Cutoff, 0.5f);
            keyerMaterial.SetFloat(ShaderIds._AlphaClip, 0.0f);
            keyerMaterial.SetFloat(ShaderIds._Surface, 1.0f);
            keyerMaterial.SetFloat(ShaderIds._Blend, 0.0f);
            keyerMaterial.SetFloat(ShaderIds._BlendOp, 0.0f);
            keyerMaterial.SetFloat(ShaderIds._SrcBlend, 1.0f);
            keyerMaterial.SetFloat(ShaderIds._DstBlend, 0.0f);
            keyerMaterial.SetFloat(ShaderIds._SrcBlendAlpha, 1.0f);
            keyerMaterial.SetFloat(ShaderIds._DstBlendAlpha, 1.0f);
            keyerMaterial.SetFloat(ShaderIds._ZWrite, 1.0f);
            keyerMaterial.SetFloat(ShaderIds._RenderQueueType, 0.0f);
            keyerMaterial.SetTexture(ShaderIds._BaseMap, rt);
            keyerMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            CreateAsset(keyerMaterial, $"{KeyerAssetPath}KeyerMaterial.mat");
            return keyerMaterial;
        }

        static Material CreateKeyerShadowMaterialURP(RenderTexture rt)
        {
            var keyerShadowMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); // Found name in URP/Shaders/Unlit.shader
            keyerShadowMaterial.SetFloat(ShaderIds._Cutoff, 0.5f);
            keyerShadowMaterial.SetFloat(ShaderIds._AlphaClip, 1.0f);
            keyerShadowMaterial.SetFloat(ShaderIds._Surface, 0.0f);
            keyerShadowMaterial.SetFloat(ShaderIds._Blend, 0.0f);
            keyerShadowMaterial.SetFloat(ShaderIds._BlendOp, 0.0f);
            keyerShadowMaterial.SetFloat(ShaderIds._SrcBlend, 1.0f);
            keyerShadowMaterial.SetFloat(ShaderIds._DstBlend, 0.0f);
            keyerShadowMaterial.SetFloat(ShaderIds._SrcBlendAlpha, 1.0f);
            keyerShadowMaterial.SetFloat(ShaderIds._DstBlendAlpha, 1.0f);
            keyerShadowMaterial.SetFloat(ShaderIds._ZWrite, 1.0f);
            keyerShadowMaterial.SetFloat(ShaderIds._QueueOffset, 0.0f);
            keyerShadowMaterial.SetTexture(ShaderIds._BaseMap, rt);
            keyerShadowMaterial.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");

            CreateAsset(keyerShadowMaterial, $"{KeyerAssetPath}KeyerShadowMaterial.mat");
            return keyerShadowMaterial;
        }

        static Material CreateKeyerMaterialLegacy(RenderTexture rt)
        {
            var keyerMaterial = new Material(Shader.Find("Unlit/Transparent"));
            keyerMaterial.SetTexture("_MainTex", rt);

            CreateAsset(keyerMaterial, $"{KeyerAssetPath}KeyerMaterial.mat");
            return keyerMaterial;
        }

        static Material CreateKeyerShadowMaterialLegacy(RenderTexture rt)
        {
            var keyerShadowMaterial = new Material(Shader.Find("Standard"));
            keyerShadowMaterial.SetFloat(ShaderIds._Mode, 1.0f);
            keyerShadowMaterial.SetInt(ShaderIds._SrcBlend, (int)BlendMode.One);
            keyerShadowMaterial.SetInt(ShaderIds._DstBlend, (int)BlendMode.Zero);
            keyerShadowMaterial.SetInt(ShaderIds._ZWrite, 1);
            keyerShadowMaterial.SetTexture(ShaderIds._MainTex, rt);
            keyerShadowMaterial.EnableKeyword("_ALPHATEST_ON");
            keyerShadowMaterial.DisableKeyword("_ALPHABLEND_ON");
            keyerShadowMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            keyerShadowMaterial.renderQueue = 2450;

            CreateAsset(keyerShadowMaterial, $"{KeyerAssetPath}KeyerShadowMaterial.mat");
            return keyerShadowMaterial;
        }

        static Material CreateKeyerMaterial(RenderTexture rt)
        {
#if HDRP_AVAILABLE
            var keyerMaterial = CreateKeyerMaterialHDRP(rt);
#elif URP_AVAILABLE
            var keyerMaterial = CreateKeyerMaterialURP(rt);
#else
            var keyerMaterial = CreateKeyerMaterialLegacy(rt);
#endif
            return keyerMaterial;
        }

        static Material CreateKeyerShadowMaterial(RenderTexture rt)
        {
#if HDRP_AVAILABLE
            var keyerCutoutMaterial = CreateKeyerShadowMaterialHDRP(rt);
#elif URP_AVAILABLE
            var keyerCutoutMaterial = CreateKeyerShadowMaterialURP(rt);
#else
            var keyerCutoutMaterial = CreateKeyerShadowMaterialLegacy(rt);
#endif
            return keyerCutoutMaterial;
        }

        /// <summary>
        /// Creates a default KeyerSettings asset.
        /// </summary>
        /// <returns>The KeyerSettings asset.</returns>
        public static KeyerSettings CreateDefaultKeyerSettings()
        {
            var keyerSettings = KeyerSettings.CreateInstance<KeyerSettings>();
            CreateAsset(keyerSettings, $"{KeyerAssetPath}KeyerSettings.asset");
            return keyerSettings;
        }

        static RenderTexture CreateResultRenderTexture()
        {
            var rt = CreateDefaultRWRenderTexture("KeyerResultRenderTexture", 1280, 1080, RenderTextureFormat.ARGBHalf);
            CreateAsset(rt, $"{KeyerAssetPath}KeyerResultRenderTexture.renderTexture");
            return rt;
        }

        static void CreateAsset<T>(T asset, string path) where T : Object
        {
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        static Keyer CreateGameObject(GameObject parent, RenderTexture rt)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var keyerSettings = CreateDefaultKeyerSettings();
            quad.transform.parent = parent.transform;
            quad.name = "Keyer";

            var material = CreateKeyerMaterial(rt);
            var meshRenderer = quad.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            var keyer = quad.AddComponent<Keyer>();
            keyer.Settings = keyerSettings;
            keyer.Result = rt;

            var defaultInputTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(k_DefaultInputPath);
            keyer.Foreground = defaultInputTexture;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            return keyer;
        }

        static void CreateShadowGameObject(GameObject parent, RenderTexture rt)
        {
            var keyerShadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var keyerShadowMaterial = CreateKeyerShadowMaterial(rt);
            keyerShadow.transform.parent = parent.transform;
            keyerShadow.name = "Keyer Shadow";
            keyerShadow.GetComponent<Renderer>().sharedMaterial = keyerShadowMaterial;
            keyerShadow.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        static void CreateKeyerAssetFolder()
        {
            if (!AssetDatabase.IsValidFolder(KeyerAssetPath))
            {
                AssetDatabase.CreateFolder("Assets", "Keyer");
            }
        }

        /// <summary>
        /// Creates a default Keyer in the scene.
        /// </summary>
        [MenuItem("GameObject/Virtual Production/Create Default Keyer")]
        public static Keyer CreateKeyer()
        {
            CreateKeyerAssetFolder();
            var rt = CreateResultRenderTexture();
            var gameObject = new GameObject("Keyer Container");
            var keyer = CreateGameObject(gameObject, rt);
            CreateShadowGameObject(gameObject, rt);
            return keyer;
        }
    }
}
