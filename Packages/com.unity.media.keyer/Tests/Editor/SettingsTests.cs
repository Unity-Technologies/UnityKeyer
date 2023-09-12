using System;
using System.Linq;
using NUnit.Framework;
using Unity.Media.Keyer.Editor;
using UnityEngine;

namespace Unity.Media.Keyer.Tests.Editor
{
    public class SettingsTests
    {
        [Test]
        public void HashCodeChangedOnAssignedProperties()
        {
            var settings = EditorUtilities.CreateKeyer().Settings;

            Assert.IsTrue(CheckChange(settings, s => s.SoftMaskEnabled = true));
            Assert.IsTrue(CheckChange(settings, s => s.ColorDifferenceCoreMask.BackgroundColor = BackgroundChannel.Blue));
            Assert.IsTrue(CheckChange(settings, s => s.ColorDifferenceCoreMask.Scale = new Vector3(2, 2, 2)));
            Assert.IsTrue(CheckChange(settings, s => s.ColorDifferenceCoreMask.ClipWhite = 0.75f));
            Assert.IsTrue(CheckChange(settings, s => s.ColorDifferenceCoreMask.ClipBlack = 0.25f));
            Assert.IsTrue(CheckChange(settings, s => s.SegmentationAlgorithmCore = SegmentationAlgorithm.ColorDistance));
            Assert.IsTrue(CheckChange(settings, s => s.SegmentationAlgorithmSoft = SegmentationAlgorithm.ColorDistance));
            Assert.IsTrue(CheckChange(settings, s => s.ColorDifferenceSoftMask.BackgroundColor = BackgroundChannel.Blue));
            Assert.IsTrue(CheckChange(settings, s => s.ColorDifferenceSoftMask.Scale = new Vector3(2, 2, 2)));
            Assert.IsTrue(CheckChange(settings, s => s.ColorDifferenceSoftMask.ClipWhite = 0.75f));
            Assert.IsTrue(CheckChange(settings, s => s.ColorDifferenceSoftMask.ClipBlack = 0.25f));
            Assert.IsTrue(CheckChange(settings, s => s.Despill.Enabled = false));
            Assert.IsTrue(CheckChange(settings, s => s.Despill.BackgroundColor = BackgroundChannel.Blue));
            Assert.IsTrue(CheckChange(settings, s => s.Despill.DespillAmount = 0.5f));
            Assert.IsTrue(CheckChange(settings, s => s.GarbageMask.Enabled = true));
            Assert.IsTrue(CheckChange(settings, s => s.GarbageMask.Invert = true));
            Assert.IsTrue(CheckChange(settings, s => s.GarbageMask.Texture = new Texture2D(1, 1)));
            Assert.IsTrue(CheckChange(settings, s => s.GarbageMask.Points = new[] { Vector2.down }.ToList()));
            Assert.IsTrue(CheckChange(settings, s => s.GarbageMask.Threshold = .6f));
            Assert.IsTrue(CheckChange(settings, s => s.GarbageMask.SdfDistance = 22));
            Assert.IsTrue(CheckChange(settings, s => s.GarbageMask.SdfQuality = SdfQuality.Mid));
            Assert.IsTrue(CheckChange(settings, s => s.GarbageMask.Mode = GarbageMaskMode.Polygon));
            Assert.IsTrue(CheckChange(settings, s => s.GarbageMask.Blend = 12));
            Assert.IsTrue(CheckChange(settings, s => s.GarbageMask.SdfEnabled = true));
            Assert.IsTrue(CheckChange(settings, s => s.ErodeMask.Enabled = true));
            Assert.IsTrue(CheckChange(settings, s => s.ErodeMask.Amount = 0.5f));
            Assert.IsTrue(CheckChange(settings, s => s.CropMask.Enabled = true));
            Assert.IsTrue(CheckChange(settings, s => s.CropMask.Left = 0.5f));
            Assert.IsTrue(CheckChange(settings, s => s.CropMask.Right = 0.5f));
            Assert.IsTrue(CheckChange(settings, s => s.CropMask.Top = 0.5f));
            Assert.IsTrue(CheckChange(settings, s => s.CropMask.Bottom = 0.5f));
            Assert.IsTrue(CheckChange(settings, s => s.BlendMask.Enabled = true));
            Assert.IsTrue(CheckChange(settings, s => s.BlendMask.Strength = 0.5f));
            Assert.IsTrue(CheckChange(settings, s => s.BlurMask.Enabled = true));
            Assert.IsTrue(CheckChange(settings, s => s.BlurMask.Quality = BlurQuality.Medium));
            Assert.IsTrue(CheckChange(settings, s => s.BlurMask.Radius = 0.6f));
            Assert.IsTrue(CheckChange(settings, s => s.ClipMask.Enabled = true));
            Assert.IsTrue(CheckChange(settings, s => s.ClipMask.ClipBlack = 0.3f));
            Assert.IsTrue(CheckChange(settings, s => s.ClipMask.ClipWhite = 0.7f));

            EditorTestsUtilities.CleanUpDirectoryContent("Assets/Keyer");
        }

        static bool CheckChange(KeyerSettings settings, Action<KeyerSettings> propertyChange)
        {
            var currentHashCode = settings.GetSerializedFieldsHashCode();
            propertyChange(settings);
            return settings.GetSerializedFieldsHashCode() != currentHashCode;
        }

        [Test]
        public void HashCodeChangedGetSimilarValueAfterAssignment()
        {
            var keyer = EditorUtilities.CreateKeyer();
            var settings1 = keyer.Settings;
            var settings2 = EditorUtilities.CreateDefaultKeyerSettings();
            settings1.CropMask.Left = 0.5f;
            settings1.BlurMask.Quality = BlurQuality.Medium;
            var hash1 = settings1.GetSerializedFieldsHashCode();
            var hash2 = settings2.GetSerializedFieldsHashCode();
            Assert.AreNotEqual(hash1, hash2);
            keyer.Settings = settings2;
            var hash3 = keyer.Settings.GetSerializedFieldsHashCode();
            Assert.AreEqual(hash2, hash3);
            settings1.CropMask.Left = 0.0f;
            settings1.BlurMask.Quality = BlurQuality.High;
            var hash4 = settings1.GetSerializedFieldsHashCode();
            Assert.AreNotEqual(hash1, hash4);
            settings1.CropMask.Left = 0.5f;
            settings1.BlurMask.Quality = BlurQuality.Medium;
            var hash5 = settings1.GetSerializedFieldsHashCode();
            Assert.AreEqual(hash1, hash5);
            EditorTestsUtilities.CleanUpDirectoryContent("Assets/Keyer");
        }
    }
}
