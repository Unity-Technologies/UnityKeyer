using System;
using System.Collections;
using NUnit.Framework;
using Unity.Media.Keyer.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Media.Keyer.Tests.Editor
{
    public class CreateKeyerTests
    {
        GameObject m_KeyerParent;
        Keyer m_Keyer;

        [SetUp]
        public void Setup()
        {
            m_Keyer = EditorUtilities.CreateKeyer();
            m_KeyerParent = m_Keyer.transform.parent.gameObject;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_KeyerParent);

            // All created assets are destroyed as part of the directory cleanup.
            EditorTestsUtilities.CleanUpDirectoryContent("Assets/Keyer");
        }

        [Test]
        public void CreatesKeyerTest()
        {
            // Validate creation of the keyer asset path
            Assert.IsTrue(AssetDatabase.IsValidFolder(EditorUtilities.KeyerAssetPath));

            // Validate creation of the keyer component
            Assert.IsTrue(m_Keyer != null);
            Assert.IsTrue(m_Keyer.Foreground != null);
            Assert.IsTrue(m_Keyer.Settings != null);

            // Validate creation of the keyer material
            var meshRenderer = m_Keyer.GetComponentInParent<MeshRenderer>();
            var keyerMaterial = meshRenderer.sharedMaterial;
            Assert.IsTrue(keyerMaterial != null);
            Assert.IsTrue(keyerMaterial.name.Contains("KeyerMaterial"));

            // Validate creation of the keyer shadow material
            var keyerShadow = GameObject.Find("Keyer Shadow");
            Assert.IsTrue(keyerShadow != null);
            var meshRendererCutout = keyerShadow.GetComponentInParent<MeshRenderer>();
            var keyerMaterialCutout = meshRendererCutout.sharedMaterial;
            Assert.IsTrue(keyerMaterialCutout != null);
            Assert.IsTrue(keyerMaterialCutout.name.Contains("KeyerShadowMaterial"));
        }

        [Test]
        public void PublicApiTest()
        {
            // Validate creation of the keyer component
            Assert.IsTrue(m_Keyer != null);
            Assert.IsTrue(m_Keyer.Foreground != null);
            Assert.IsTrue(m_Keyer.Settings != null);

            // Validate public API
            m_Keyer.Foreground = Texture2D.whiteTexture;
            m_Keyer.DisplayMode = Keyer.Display.Front;
            var newSettings = ScriptableObject.CreateInstance<KeyerSettings>();
            var newResult = new RenderTexture(1, 1, 1);
            m_Keyer.Result = newResult;
            m_Keyer.Settings = newSettings;

            m_Keyer.Settings = null;
            m_Keyer.Result = null;

            // Destroy newly created instance. Previous one is destroyed in TearDown.
            Object.DestroyImmediate(newSettings, true);
            newResult.Release();
        }

        [UnityTest]
        public IEnumerator KeyerWithNullSettingsDoesNotThrow()
        {
            Assert.IsTrue(m_Keyer != null);
            Assert.IsTrue(m_Keyer.Settings != null);

            // Set settings to null then make sure it doesn't throw on the next frame
            m_Keyer.Settings = null;

            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;
        }
    }
}
