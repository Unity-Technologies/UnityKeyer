using NUnit.Framework;
using Unity.Media.Keyer.Editor;
using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Assert = UnityEngine.Assertions.Assert;
using Object = UnityEngine.Object;

namespace Unity.Media.Keyer.Tests.Editor
{
    // Note: the Inspector must be in Normal mode for these tests to pass.
    [TestFixture]
    class DisplayPropertyDrawerTests
    {
        static readonly Type k_InspectorWindowType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");

        Keyer m_Keyer;
        KeyerSettings m_Settings;
        EditorWindow m_InspectorWindow;
        VisualElement m_Root;
        UnityEditor.Editor m_Editor;
        RenderTexture m_Target;

        [SetUp]
        public void Setup()
        {
            m_Settings = ScriptableObject.CreateInstance<KeyerSettings>();
            m_Keyer = EditorUtilities.CreateKeyer();
            m_Target = new RenderTexture(128, 128, 0, GraphicsFormat.R8G8B8A8_UNorm);
            m_Keyer.Result = m_Target;
            m_Keyer.Foreground = Texture2D.blackTexture;
            m_Keyer.Settings = m_Settings;
            m_Editor = UnityEditor.Editor.CreateEditor(m_Keyer);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Keyer.gameObject);
            Object.DestroyImmediate(m_Settings);
            Object.DestroyImmediate(m_Editor);
            m_Target.Release();
        }

        // Needed due to Editor initialization triggering tons of events.
        [UnityTest, Order(1)]
        public IEnumerator Warmup()
        {
            yield return SelectAndShowInspector();

            for (var i = 0; i != 32; ++i)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
        }

        [UnityTest, Order(2)]
        public IEnumerator ChoicesAreUpdatedOnProceduralSettingsChange()
        {
            yield return ChoicesAreUpdated(UpdateProcedural());
        }

        [UnityTest, Order(3)]
        public IEnumerator ChoicesAreUpdatedOnEditorSettingsChange()
        {
            yield return ChoicesAreUpdated(UpdateSerialized());
        }

        IEnumerator ChoicesAreUpdated(IEnumerator change)
        {
            yield return SelectAndShowInspector();

            // Fetch the display field.
            var displayField = m_InspectorWindow.rootVisualElement.Query<DropdownField>(name: "Display").First();
            Assert.IsNotNull(displayField);

            // By default the garbage mask is disabled, so the option should not be available.
            Assert.IsFalse(displayField.choices.Contains(Keyer.Display.GarbageMask.ToString()));

            yield return change;

            Assert.IsTrue(displayField.choices.Contains(Keyer.Display.GarbageMask.ToString()));
        }

        IEnumerator SelectAndShowInspector()
        {
            // Select the keyer and show the inspector so that the keyer editor is rendered.
            Selection.activeGameObject = m_Keyer.gameObject;

            m_InspectorWindow = EditorWindow.GetWindow(k_InspectorWindowType);
            m_InspectorWindow.Show();

            yield return null;
        }

        IEnumerator UpdateSerialized()
        {
            var serializedSettings = new SerializedObject(m_Settings);
            var enableGMaskProp = serializedSettings.FindProperty("m_GarbageMask").FindPropertyRelative("m_Enabled");
            enableGMaskProp.boolValue = true;

            // Most important part. Updating the serialized object should trigger an update of the display options list.
            serializedSettings.ApplyModifiedProperties();

            yield return null;
        }

        IEnumerator UpdateProcedural()
        {
            m_Settings.GarbageMask.Enabled = true;

            // Keyer.Update must be executed.
            EditorApplication.QueuePlayerLoopUpdate();
            yield return null;
        }
    }
}
