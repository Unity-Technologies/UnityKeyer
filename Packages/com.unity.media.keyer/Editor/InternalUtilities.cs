using UnityEditor;
using UnityEngine;

namespace Unity.Media.Keyer.Editor
{
    static class InternalUtilities
    {
        [MenuItem("internal:Edit/VirtualProduction/Keyer/Upgrade Settings Assets")]
        static void UpgradeSettingsAssets()
        {
            // In KeyerSettings, we added m_SoftMaskEnabled which replaces m_Enable within ColorDistance and ColorDifference.
            // Here we synchronize the properties if needed.
            foreach (var guid in AssetDatabase.FindAssets($"t: {typeof(KeyerSettings).FullName}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<KeyerSettings>(path);

                if (settings != null)
                {
                    Debug.Log($"Checking {nameof(KeyerSettings)} asset at path \"{path}\"");

                    var serializedObject = new SerializedObject(settings);
                    var segmentationAlgorithmSoftProperty = serializedObject.FindProperty("m_SegmentationAlgorithmSoft");
                    var colorDifferenceEnabledProperty = serializedObject.FindProperty("m_ColorDifferenceSoftMask").FindPropertyRelative("m_Enabled");
                    var colorDistanceEnabledProperty = serializedObject.FindProperty("m_ColorDistanceSoftMask").FindPropertyRelative("m_Enabled");

                    var segmentationAlgorithm = (SegmentationAlgorithm)segmentationAlgorithmSoftProperty.intValue;
                    var colorDifferenceEnabled = colorDifferenceEnabledProperty.boolValue;
                    var colorDistanceEnabled = colorDistanceEnabledProperty.boolValue;

                    // Should we update m_SoftMaskEnabled?
                    if (segmentationAlgorithm == SegmentationAlgorithm.ColorDifference && colorDifferenceEnabled ||
                        segmentationAlgorithm == SegmentationAlgorithm.ColorDistance && colorDistanceEnabled)
                    {
                        var softMaskEnabledProperty = serializedObject.FindProperty("m_SoftMaskEnabled");
                        softMaskEnabledProperty.boolValue = true;
                        serializedObject.ApplyModifiedPropertiesWithoutUndo();

                        EditorUtility.SetDirty(settings);
                        AssetDatabase.SaveAssetIfDirty(settings);

                        Debug.Log($"Edited {nameof(KeyerSettings)} asset at path \"{path}\"");
                    }
                }
            }
        }
    }
}
