using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Unity.Media.Keyer.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Media.Keyer.Tests.Editor
{
    [TestFixture]
    class KeyerSelectorTests
    {
        class TestWindow : EditorWindow
        {
            KeyerSelector m_KeyerSelector;
            DropdownField m_DropdownField;

            public DropdownField DropdownField => m_DropdownField;
            public KeyerSelector KeyerSelector => m_KeyerSelector;

            void CreateGUI()
            {
                m_DropdownField = new DropdownField();
                m_KeyerSelector = new KeyerSelector(m_DropdownField);
                rootVisualElement.Add(m_DropdownField);
            }
        }

        class ValueChangedScope : IDisposable
        {
            readonly Stack<Keyer> m_ChangedValues = new();
            readonly TestWindow m_Window;

            public int Count => m_ChangedValues.Count;

            public Keyer Pop() => m_ChangedValues.Pop();

            public ValueChangedScope(TestWindow window)
            {
                m_Window = window;
                m_Window.KeyerSelector.ValueChanged += OnValueChanged;
            }

            public void Dispose()
            {
                m_Window.KeyerSelector.ValueChanged -= OnValueChanged;
                m_ChangedValues.Clear();
            }

            void OnValueChanged(Keyer keyer)
            {
                m_ChangedValues.Push(keyer);
            }
        }

        // In Application.cpp we are checking changes to the Hierarchy window every 150 ms (see: 'static Ticker hierarchyWindowTick(.15f)')
        const int k_MaxWaitTimeInMilliseconds = 3000;

        readonly List<Keyer> m_Keyers = new();
        KeyerSettings m_Settings;
        RenderTexture m_Target;
        TestWindow m_Window;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            m_Settings = ScriptableObject.CreateInstance<KeyerSettings>();
            m_Target = new RenderTexture(128, 128, 0, GraphicsFormat.R8G8B8A8_UNorm);

            for (var i = 0; i != 3; ++i)
            {
                var keyer = CreateKeyer($"keyer_{i}");
                m_Keyers.Add(keyer);
            }

            m_Window = EditorWindow.GetWindow<TestWindow>();
            m_Window.Show();
            yield return null;
        }

        Keyer CreateKeyer(string name)
        {
            var go = new GameObject(name, typeof(Keyer));
            var keyer = go.GetComponent<Keyer>();
            keyer.Result = m_Target;
            keyer.Foreground = Texture2D.blackTexture;
            keyer.Settings = m_Settings;
            return keyer;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var keyer in m_Keyers)
            {
                Object.DestroyImmediate(keyer.gameObject);
            }

            m_Keyers.Clear();
            m_Target.Release();
            Object.DestroyImmediate(m_Settings);
        }

        [Test]
        public void FetchInstancesProperly()
        {
            var dropDown = m_Window.DropdownField;
            Assert.IsTrue(dropDown.choices.Count >= m_Keyers.Count);

            foreach (var keyer in m_Keyers)
            {
                Assert.IsTrue(dropDown.choices.Contains(keyer.gameObject.name));
            }
        }

        [Test]
        public void NotifyOnUiSelection()
        {
            using (var scope = new ValueChangedScope(m_Window))
            {
                var keyer = m_Keyers[2];
                m_Window.DropdownField.value = keyer.gameObject.name;
                Assert.IsTrue(scope.Count == 1);
                Assert.IsTrue(scope.Pop() == keyer);
            }
        }

        [UnityTest]
        public IEnumerator DeactivatedInstanceIsUnlisted()
        {
            var keyer = m_Keyers[0];

            keyer.enabled = false;
            yield return null;

            var dropDown = m_Window.DropdownField;
            Assert.IsFalse(dropDown.choices.Contains(keyer.gameObject.name));

            keyer.enabled = true;
            yield return null;

            Assert.IsTrue(dropDown.choices.Contains(keyer.gameObject.name));
        }

        [Test]
        public void CanSelectInstance()
        {
            var keyer = m_Keyers[1];
            m_Window.KeyerSelector.SetValue(keyer, false);
            var dropDown = m_Window.DropdownField;
            Assert.IsTrue(dropDown.value == keyer.gameObject.name);
        }

        [UnityTest]
        public IEnumerator SetToNullAndNotifyIfInstanceIsNotListed()
        {
            var keyer = m_Keyers[1];
            keyer.enabled = false;
            yield return null;

            using (var scope = new ValueChangedScope(m_Window))
            {
                m_Window.KeyerSelector.SetValue(keyer, false);
                Assert.IsTrue(scope.Count == 1);
                Assert.IsTrue(scope.Pop() == null);
            }
        }

        [UnityTest]
        public IEnumerator SetToNullAndNotifyIfInstanceIsDeactivated()
        {
            var keyer = m_Keyers[1];

            using (var scope = new ValueChangedScope(m_Window))
            {
                m_Window.KeyerSelector.SetValue(keyer, false);
                Assert.IsTrue(scope.Count == 0);
            }

            keyer.enabled = false;
            yield return null;

            using (var scope = new ValueChangedScope(m_Window))
            {
                m_Window.KeyerSelector.SetValue(keyer, false);
                Assert.IsTrue(scope.Count == 1);
                Assert.IsTrue(scope.Pop() == null);
            }
        }

        [UnityTest]
        public IEnumerator HandlesDuplicateNames()
        {
            var newKeyer = CreateKeyer("keyer_0");
            yield return null;

            var dropDown = m_Window.DropdownField;
            Assert.IsTrue(dropDown.choices.Contains("keyer_0 (2)"));

            Object.DestroyImmediate(newKeyer);
        }

        [UnityTest]
        public IEnumerator HandlesImplicitRename()
        {
            var newKeyer = CreateKeyer("keyer_0");
            yield return null;

            var dropDown = m_Window.DropdownField;
            Assert.IsTrue(dropDown.choices.Contains("keyer_0 (2)"));

            m_Window.KeyerSelector.SetValue(newKeyer, false);

            // Deactivating keyer_0 will free the usage of its name by newKeyer.
            m_Keyers[0].enabled = false;
            yield return null;

            Assert.IsFalse(dropDown.choices.Contains("keyer_0 (2)"));
            Assert.IsTrue(dropDown.value == "keyer_0");
            Assert.IsTrue(m_Window.KeyerSelector.GetValue() == newKeyer);

            Object.DestroyImmediate(newKeyer);
        }

        [UnityTest]
        public IEnumerator HandlesUserRename()
        {
            const string newName = "newname";

            var keyer = m_Keyers[1];
            m_Window.KeyerSelector.SetValue(keyer, false);
            keyer.gameObject.name = newName;

            var stopWatch = Stopwatch.StartNew();
            while (stopWatch.ElapsedMilliseconds <= k_MaxWaitTimeInMilliseconds)
            {
                yield return null;
            }

            Assert.IsTrue(m_Window.KeyerSelector.GetValue() == keyer);
            Assert.IsTrue(m_Window.DropdownField.value == newName);
        }
    }
}
