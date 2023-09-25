using NUnit.Framework;
using Unity.Media.Keyer.Editor;
using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using UnityEditor.UIElements;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Assert = UnityEngine.Assertions.Assert;
using Object = UnityEngine.Object;

namespace Unity.Media.Keyer.Tests.Editor
{
    [TestFixture]
    class DisplayMaskFieldWrapperTests
    {
        class TestWindow : EditorWindow, IKeyerAccess
        {
            DisplayMaskFieldWrapper m_DisplayMaskFieldWrapper;
            MaskField m_MaskField;
            Keyer m_Keyer;
            VisualElement m_Container;
            UIUtils.DisplayFlags m_SelectedFlags = UIUtils.DisplayFlags.CoreMatte;
            public MaskField MaskField => m_MaskField;
            public DisplayMaskFieldWrapper DisplayMaskFieldWrapper => m_DisplayMaskFieldWrapper;
            public Keyer Keyer
            {
                set
                {
                    m_Keyer = value;
                    if (m_Keyer != null)
                    {
                        var access = (IKeyerAccess)m_Keyer;
                        access.Changed += m_Changed.Invoke;
                        var editor = UnityEditor.Editor.CreateEditor(m_Keyer);
                        m_Container.Add(editor.CreateInspectorGUI());
                    }
                }
            }

            void CreateGUI()
            {
                m_Container = new VisualElement();
                m_Container.style.flexDirection = FlexDirection.Column;
                m_Container.style.flexGrow = 1;
                m_MaskField = new MaskField();
                m_DisplayMaskFieldWrapper = new DisplayMaskFieldWrapper(m_MaskField, "Choose Display", this, m_SelectedFlags, OnDisplaySelectionChanged);
                m_Container.Add(m_MaskField);
                rootVisualElement.Add(m_Container);
            }

            public Keyer GetKeyer()
            {
                return m_Keyer;
            }

            void OnDisplaySelectionChanged(UIUtils.DisplayFlags displaySelection)
            {
                m_SelectedFlags = displaySelection;
            }

            Action m_Changed = delegate { };

            event Action IKeyerAccess.Changed
            {
                add => m_Changed += value;
                remove => m_Changed -= value;
            }
        }

        EditorWindow m_InspectorWindow;
        Keyer m_Keyer;
        KeyerSettings m_Settings;
        RenderTexture m_Target;
        TestWindow m_Window;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            m_Settings = ScriptableObject.CreateInstance<KeyerSettings>();
            m_Keyer = EditorUtilities.CreateKeyer();
            m_Target = new RenderTexture(128, 128, 0, GraphicsFormat.R8G8B8A8_UNorm);
            m_Keyer.Result = m_Target;
            m_Keyer.Foreground = Texture2D.blackTexture;
            m_Keyer.Settings = m_Settings;

            m_Window = EditorWindow.GetWindow<TestWindow>();
            m_Window.Keyer = m_Keyer;
            m_Window.Show();
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Keyer.gameObject);
            m_Target.Release();
            Object.DestroyImmediate(m_Settings);
            m_Window.Close();
            // All created assets are destroyed as part of the directory cleanup.
            EditorTestsUtilities.CleanUpDirectoryContent("Assets/Keyer");
        }

        [Test]
        public void InitializeProperlyWithCoreMatte()
        {
            var maskField = m_Window.MaskField;
            Assert.IsTrue(maskField.choices.Count >= 3);
            var selectedFlags = m_Window.DisplayMaskFieldWrapper.GetSelectedDisplays();
            Assert.IsTrue(selectedFlags == UIUtils.DisplayFlags.CoreMatte);
        }

        [Test]
        public void ValidateDisplaySelectionChangeWhenMaskFieldSelectionChange()
        {
            var maskField = m_Window.MaskField;
            maskField.value = (int)UIUtils.DisplayFlags.Result;
            var displayMaskFieldWrapper = m_Window.DisplayMaskFieldWrapper;
            var selectedFlags = displayMaskFieldWrapper.GetSelectedDisplays();
            Assert.IsTrue(selectedFlags == UIUtils.DisplayFlags.Result);
            maskField.value = (int)UIUtils.DisplayFlags.CoreMatte;
            var newSelectedFlags = displayMaskFieldWrapper.GetSelectedDisplays();
            Assert.IsTrue(newSelectedFlags == UIUtils.DisplayFlags.CoreMatte);
        }

        [UnityTest]
        public IEnumerator ValidateDisplayChoicesChangeWhenKeyerSettingsChange()
        {
            var maskField = m_Window.MaskField;
            var count = maskField.choices.Count;
            yield return UpdateSetSoftMaskSerialized(true);
            var newCount = maskField.choices.Count;
            Assert.IsTrue(newCount > count);
        }

        [UnityTest]
        public IEnumerator ValidateDisplayChoicesRemovedWhenKeyerSettingsChange()
        {
            var maskField = m_Window.MaskField;
            var displayMaskFieldWrapper = m_Window.DisplayMaskFieldWrapper;
            // Enable soft mask will add the soft mask to the choices.
            yield return UpdateSetSoftMaskSerialized(true);
            maskField.value = (int)UIUtils.DisplayFlags.SoftMatte;
            var selectedFlags = displayMaskFieldWrapper.GetSelectedDisplays();
            Assert.IsTrue(selectedFlags == UIUtils.DisplayFlags.SoftMatte);
            // Disable soft mask will remove the soft matte from the list.
            yield return UpdateSetSoftMaskSerialized(false);
            var newSelectedFlags = displayMaskFieldWrapper.GetSelectedDisplays();
            Assert.IsTrue(newSelectedFlags != UIUtils.DisplayFlags.SoftMatte);
        }

        IEnumerator UpdateSetSoftMaskSerialized(bool value)
        {
            var serializedSettings = new SerializedObject(m_Settings);
            var enableSoftMask = serializedSettings.FindProperty("m_SoftMaskEnabled");
            enableSoftMask.boolValue = value;

            // Updating the serialized properties will an update of the display options list.
            serializedSettings.ApplyModifiedProperties();
            yield return null;
        }
    }
}
