using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Media.Keyer.Editor
{
    [CustomPropertyDrawer(typeof(GarbageMask))]
    class GarbageMaskPropertyDrawer : PropertyDrawer
    {
        const string k_SaveTitle = "Save Garbage Mask";
        const string k_SaveDefaultName = "GarbageMask";
        const string k_SaveMessage = "Save generated garbage mask as a texture.";

        static readonly Dictionary<GarbageMaskMode, string> k_ModeToString = new();
        static readonly Dictionary<string, GarbageMaskMode> k_StringToMode = new();
        static readonly List<string> k_ModeOptions = new();

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            k_ModeToString.Clear();
            k_StringToMode.Clear();
            k_ModeOptions.Clear();

            k_ModeToString.Add(GarbageMaskMode.Texture, "Texture");
            k_ModeToString.Add(GarbageMaskMode.Polygon, "Polygon");

            foreach (var pair in k_ModeToString)
            {
                k_StringToMode.Add(pair.Value, pair.Key);
                k_ModeOptions.Add(pair.Value);
            }
        }

        static readonly string[] k_SdfGeneratorProperties =
        {
            "m_SdfQuality", "m_SdfDistance", "m_Threshold", "m_Blend"
        };

        // Normally point are only edited through the polygon tool.
        static readonly string[] k_AdvancedProperties =
        {
            "m_Points"
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            var modeProperty = property.FindPropertyRelative("m_Mode");
            var modeDropdown = new DropdownField("Mode");
            modeDropdown.BindProperty(modeProperty);
            modeDropdown.AddToClassList(DropdownField.alignedFieldUssClassName);
            container.Add(modeDropdown);

            var textureProperty = property.FindPropertyRelative("m_Texture");
            var textureField = new ObjectField("Texture")
            {
                objectType = typeof(Texture)
            };
            textureField.BindProperty(textureProperty);
            textureField.AddToClassList(ObjectField.alignedFieldUssClassName);
            container.Add(textureField);

            var openEditorButton = new Button { text = "Edit Polygon", tooltip = "Create a 2D Gmask polygon." };
            openEditorButton.RegisterCallback<ClickEvent>(_ =>
            {
                var window = EditorWindow.GetWindow<PolygonEditor>();

                if (TryGetSelectedKeyer(out var keyer))
                {
                    window.Keyer = keyer;
                }

                window.Show();
            });

            container.Add(openEditorButton);

            var saveButton = new Button { text = "Save" };
            saveButton.tooltip = "Save the generated Garbage Mask as a png texture.";
            saveButton.RegisterCallback<ClickEvent>(_ =>
            {
                var settings = (KeyerSettings)property.serializedObject.targetObject;
                if (settings == null)
                {
                    throw new InvalidOperationException(
                        $"Could not access underlying {nameof(GarbageMask)}.");
                }

                // If possible, we match the resolution of the foreground.
                var resolution = new Vector2Int(1024, 1024);
                if (TryGetSelectedKeyer(out var keyer))
                {
                    var foreground = keyer.Foreground;
                    if (foreground != null)
                    {
                        resolution = new Vector2Int(foreground.width, foreground.height);
                    }
                }

                var points = settings.GarbageMask.Points;
                if (points == null || points.Count < 3)
                {
                    throw new InvalidOperationException(
                        "Cannot encode mask as png, invalid geometry.");
                }

                var bytes = Geometry.Utilities.EncodeToPNG(points, resolution);
                SaveMaskAsPng(bytes);
            });

            container.Add(saveButton);

            var invertField = new PropertyField(property.FindPropertyRelative("m_Invert"));
            container.Add(invertField);

            var sdfEnabledProperty = property.FindPropertyRelative("m_SdfEnabled");
            var sdfToggleFoldout = new ToggleFoldout
            {
                Property = sdfEnabledProperty,
                Label = "Signed Distance Field",
                Enabled = sdfEnabledProperty.boolValue
            };
            var title = sdfToggleFoldout.Q<Label>("header-title");
            title.tooltip = "Dilate the Garbage Mask using the Signed Distance Field algorithm.";
            var header = sdfToggleFoldout.Q<VisualElement>("header-container");
            header?.RemoveFromClassList("header-back-color");

            AddProperties(property, sdfToggleFoldout.contentContainer, k_SdfGeneratorProperties);
            container.Add(sdfToggleFoldout);

            modeDropdown.RegisterValueChangedCallback(_ =>
            {
                property.serializedObject.ApplyModifiedProperties();
                var mode = (GarbageMaskMode)modeProperty.intValue;
                var buttonDisplay = mode == GarbageMaskMode.Polygon ? DisplayStyle.Flex : DisplayStyle.None;
                openEditorButton.style.display = buttonDisplay;
                saveButton.style.display = buttonDisplay;
                textureField.style.display = mode == GarbageMaskMode.Texture ? DisplayStyle.Flex : DisplayStyle.None;
            });

#if DEBUG_ADVANCED
            AddProperties(property, container, k_AdvancedProperties);
#endif
            return container;
        }

        static void AddProperties(SerializedProperty property, VisualElement container, string[] properties)
        {
            foreach (var prop in properties)
            {
                var field = new PropertyField(property.FindPropertyRelative(prop));
                container.Add(field);
            }
        }

        // This is not a generic implementation.
        // It configures the asset specifically for masks.
        static void SaveMaskAsPng(byte[] bytes)
        {
            var path = EditorUtility.SaveFilePanelInProject(k_SaveTitle, k_SaveDefaultName, "png", k_SaveMessage);
            if (path != "")
            {
                File.WriteAllBytes(path, bytes);

                AssetDatabase.ImportAsset(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.isReadable = true;
                    importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.textureType = TextureImporterType.SingleChannel;
                    importer.textureShape = TextureImporterShape.Texture2D;
                    importer.sRGBTexture = false;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
                    importer.SetTextureSettings(settings);
                    EditorUtility.IsDirty(importer);
                }
            }
        }

        static bool TryGetSelectedKeyer(out Keyer keyer)
        {
            if (Selection.activeGameObject != null)
            {
                keyer = Selection.activeGameObject.GetComponent<Keyer>();
                return true;
            }

            keyer = null;
            return false;
        }
    }
}
