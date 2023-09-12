using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Media.Keyer.GraphicsTests
{
    public class Helpers
    {
        public const string k_ReferenceImagesRoot = "Assets/ReferenceImages";
        public const string k_ActualImagesRoot = "Assets/ActualImages";

        public static Texture2D ConvertRenderTextureToTexture2D(RenderTexture renderTexture)
        {
            var previousRT = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            RenderTexture.active = previousRT;
            return texture;
        }

        public static Texture2D LoadReferenceImage(string fileName)
        {
            var referenceImagePath = ReplaceCharacters(Path.Combine(k_ReferenceImagesRoot, $"{fileName}.png"));

            var importer = AssetImporter.GetAtPath(referenceImagePath) as TextureImporter;
            if (importer != null)
                if (SetupReferenceImageImportSettings(importer))
                    AssetDatabase.ImportAsset(referenceImagePath);

            return LoadImage(referenceImagePath);
        }

        public static void CompareKeyerResultAgainstReference(string referenceImageName, RenderTexture keyerResult, ImageComparisonSettings settings)
        {
            var refTexture = Helpers.LoadReferenceImage(referenceImageName);
            var actualTexture = Helpers.ConvertRenderTextureToTexture2D(keyerResult);
            ImageAssert.AreEqual(refTexture, actualTexture, settings);
        }

        public static Texture2D LoadActualImage(string fileName)
        {
            var actualImagePath = ReplaceCharacters(Path.Combine(k_ActualImagesRoot, $"{fileName}.png"));
            return LoadImage(actualImagePath);
        }

        public static Texture2D LoadImage(string filePath)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);
        }

        public static bool SetupReferenceImageImportSettings(TextureImporter textureImporter)
        {
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.isReadable = true;
            textureImporter.mipmapEnabled = false;
            textureImporter.npotScale = TextureImporterNPOTScale.None;
            var textureImporterPlatformSettings = new TextureImporterPlatformSettings
            {
                format = TextureImporterFormat.RGBA32
            };
            textureImporter.SetPlatformTextureSettings(textureImporterPlatformSettings);
            return EditorUtility.IsDirty(textureImporter);
        }

        static string ReplaceCharacters(string _str) => _str.Replace("(", "_").Replace(")", "_").Replace("\"", "").Replace(",", "-");

        public static void CreateProceduralScene(string sceneName)
        {
            var testScene = SceneManager.CreateScene(sceneName);
            SceneManager.SetActiveScene(testScene);

            var cameraObj = new GameObject("Camera");
            cameraObj.AddComponent<Camera>();
            cameraObj.tag = "MainCamera";
        }

        public static IEnumerator DoScreenCapture(RenderTexture target)
        {
            // IMPORTANT: ScreenCapture may grab whatever editor panel is being rendered if this yield instruction is changed!
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshotIntoRenderTexture(target);
        }
    }
}
