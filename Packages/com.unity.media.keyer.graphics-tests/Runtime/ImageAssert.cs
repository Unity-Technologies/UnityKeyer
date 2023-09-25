using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.TestProtocol;
using Unity.TestProtocol.Messages;
using UnityEditor;
using UnityEngine;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Unity.Media.Keyer.GraphicsTests
{
    /// <summary>
    /// Provides test assertion helpers for working with images.
    /// </summary>
    public class ImageAssert
    {
        const int k_BatchSize = 1024;

        static string StripParametricTestCharacters(string name)
        {
            {
                var illegal = "\"";
                var found = name.IndexOf(illegal);
                while (found >= 0)
                {
                    name = name.Remove(found, 1);
                    found = name.IndexOf(illegal);
                }
            }
            {
                var illegal = ",";
                name = name.Replace(illegal, "-");
            }
            {
                var illegal = "(";
                name = name.Replace(illegal, "_");
            }
            {
                var illegal = ")";
                name = name.Replace(illegal, "_");
            }
            return name;
        }

        /// <summary>
        /// Compares an image to a 'reference' image to see if it looks correct.
        /// </summary>
        /// <param name="expected">What the image is supposed to look like.</param>
        /// <param name="actual">What the image actually looks like.</param>
        /// <param name="settings">Optional settings that control how the comparison is performed. Can be null, in which case the images are required to be exactly identical.</param>
        public static void AreEqual(Texture2D expected, Texture2D actual, ImageComparisonSettings settings = null, bool saveFailedImage = true)
        {
            if (actual == null)
                throw new ArgumentNullException(nameof(actual));

            var dirName = Helpers.k_ActualImagesRoot;
            var imageName = TestContext.CurrentContext.Test.MethodName != null ? TestContext.CurrentContext.Test.Name : "NoName";
            var failedImageMessage = new FailedImageMessage
            {
                PathName = dirName,
                ImageName = StripParametricTestCharacters(imageName),
            };

            try
            {
                Assert.That(expected, Is.Not.Null, "No reference image was provided. Path: " + dirName);

                Assert.That(actual.width, Is.EqualTo(expected.width),
                    "The expected image had width {0}px, but the actual image had width {1}px.", expected.width,
                    actual.width);
                Assert.That(actual.height, Is.EqualTo(expected.height),
                    "The expected image had height {0}px, but the actual image had height {1}px.", expected.height,
                    actual.height);

                Assert.That(actual.format, Is.EqualTo(expected.format),
                    "The expected image had format {0} but the actual image had format {1}.", expected.format,
                    actual.format);

                using (var expectedPixels = new NativeArray<Color32>(expected.GetPixels32(0), Allocator.TempJob))
                using (var actualPixels = new NativeArray<Color32>(actual.GetPixels32(0), Allocator.TempJob))
                using (var diffPixels = new NativeArray<Color32>(expectedPixels.Length, Allocator.TempJob))
                using (var sumOverThreshold = new NativeArray<float>(Mathf.CeilToInt(expectedPixels.Length / (float)k_BatchSize), Allocator.TempJob))
                using (var badPixels = new NativeArray<int>(sumOverThreshold.Length, Allocator.TempJob))
                {
                    if (settings == null)
                        settings = new GameObject().AddComponent<ImageComparisonSettings>();

                    // Extract flags
                    var testAverageDeltaE = settings.ActiveImageTests.HasFlag(ImageComparisonSettings.ImageTests.AverageDeltaE);
                    var testBadPixelsCount = settings.ActiveImageTests.HasFlag(ImageComparisonSettings.ImageTests.IncorrectPixelsCount);
                    var countBadDeltaE = testBadPixelsCount && settings.ActivePixelTests.HasFlag(ImageComparisonSettings.PixelTests.DeltaE);
                    var countBadGamma = testBadPixelsCount && settings.ActivePixelTests.HasFlag(ImageComparisonSettings.PixelTests.DeltaGamma);
                    var countBadAlpha = testBadPixelsCount && settings.ActivePixelTests.HasFlag(ImageComparisonSettings.PixelTests.DeltaAlpha);

                    new ComputeDiffJob
                    {
                        expected = expectedPixels,
                        actual = actualPixels,
                        diff = diffPixels,
                        sumOverThreshold = sumOverThreshold,
                        badPixels = badPixels,
                        deltaEThreshold = settings.PerPixelCorrectnessThreshold,
                        gammaThreshold = settings.PerPixelGammaThreshold,
                        alphaThreshold = settings.PerPixelAlphaThreshold,
                        addDeltaE = testAverageDeltaE,
                        countBadDeltaE = countBadDeltaE,
                        countBadGamma = countBadGamma,
                        countBadAlpha = countBadAlpha
                    }.Schedule(expectedPixels.Length, k_BatchSize).Complete();

                    var pixelCount = expected.width * expected.height;
                    var averageDeltaE = sumOverThreshold.Sum() / pixelCount;
                    var badPixelsCount = (badPixels.Sum() - 0.1f) / pixelCount;

                    try
                    {
                        if (testAverageDeltaE)
                            Assert.That(averageDeltaE, Is.LessThanOrEqualTo(settings.AverageCorrectnessThreshold));
                        if (testBadPixelsCount)
                            Assert.That(badPixelsCount, Is.LessThanOrEqualTo(settings.IncorrectPixelsThreshold));
                    }
                    catch (AssertionException)
                    {
                        var diffImage = new Texture2D(expected.width, expected.height, TextureFormat.RGB24, false);
                        var diffPixelsArray = new Color32[expected.width * expected.height];
                        diffPixels.CopyTo(diffPixelsArray);
                        diffImage.SetPixels32(diffPixelsArray, 0);
                        diffImage.Apply(false);

                        failedImageMessage.DiffImage = diffImage.EncodeToPNG();
                        failedImageMessage.ExpectedImage = expected.EncodeToPNG();
                        throw;
                    }
                }
            }
            catch (AssertionException)
            {
                failedImageMessage.ActualImage = actual.EncodeToPNG();
#if UNITY_EDITOR
                if (saveFailedImage)
                    ImageHandler.instance.SaveImage(failedImageMessage);
#endif
                throw;
            }
        }

        struct ComputeDiffJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Color32> expected;
            [ReadOnly] public NativeArray<Color32> actual;
            public NativeArray<Color32> diff;

            public float deltaEThreshold;
            public float gammaThreshold;
            public float alphaThreshold;

            public bool addDeltaE;

            public bool countBadDeltaE;
            public bool countBadGamma;
            public bool countBadAlpha;

            [NativeDisableParallelForRestriction]
            public NativeArray<float> sumOverThreshold;

            [NativeDisableParallelForRestriction]
            public NativeArray<int> badPixels;

            public void Execute(int index)
            {
                var exp = expected[index];
                var act = actual[index];
                var batch = index / k_BatchSize;

                var deltaE = 0.0f;
                var deltaGamma = 0.0f;
                var deltaAlpha = 0.0f;
                var pixelIsCorrect = true;

                if (addDeltaE || countBadDeltaE)
                {
                    deltaE = JABDeltaE(RGBtoJAB(exp), RGBtoJAB(act));
                    var deltaEOverThreshold = Mathf.Max(0f, deltaE - deltaEThreshold);
                    sumOverThreshold[batch] = sumOverThreshold[batch] + deltaEOverThreshold;
                    if (countBadDeltaE)
                        pixelIsCorrect &= deltaEOverThreshold <= 0;
                }

                if (countBadGamma)
                {
                    var deltaR = Mathf.Abs(exp.r - act.r);
                    var deltaG = Mathf.Abs(exp.g - act.g);
                    var deltaB = Mathf.Abs(exp.b - act.b);

                    deltaGamma = Mathf.Max(Mathf.Max(deltaR, deltaG), deltaB) / 255f;
                    pixelIsCorrect &= deltaGamma <= gammaThreshold;
                }

                if (countBadAlpha)
                {
                    deltaAlpha = Mathf.Abs(exp.a - act.a) / 255f;
                    pixelIsCorrect &= deltaAlpha <= alphaThreshold;
                }

                badPixels[batch] += pixelIsCorrect ? 0 : 1;

                // deltaE is linear, convert it to sRGB for easier debugging
                deltaE = Mathf.LinearToGammaSpace(deltaE);
                var result = Mathf.Max(Mathf.Max(deltaE, deltaAlpha), deltaGamma);
                var colorResult = new Color(result, result, result, 1f);
                diff[index] = colorResult;
            }
        }
        struct ComputeLinearHDRImageDiffJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Color> expected;
            [ReadOnly] public NativeArray<Color> actual;
            public NativeArray<Color> diff;
            public float pixelThreshold;

            [NativeDisableParallelForRestriction]
            public NativeArray<float> batchMSE;

            [NativeDisableParallelForRestriction]
            public NativeArray<float> batchMaxDelta;

            [NativeDisableParallelForRestriction]
            public NativeArray<int> batchBadPixels;

            public void Execute(int index)
            {
                var exp = expected[index];
                var act = actual[index];
                var batch = index / k_BatchSize;

                // compute pixel difference
                var deltaR = Mathf.Abs(exp.r - act.r);
                var deltaG = Mathf.Abs(exp.g - act.g);
                var deltaB = Mathf.Abs(exp.b - act.b);
                var deltaA = Mathf.Abs(exp.a - act.a);
                var maxDelta = Mathf.Max(Mathf.Max(Mathf.Max(deltaR, deltaG), deltaB), deltaA);
                if (maxDelta > pixelThreshold)
                    batchBadPixels[batch]++;
                batchMaxDelta[batch] = Mathf.Max(batchMaxDelta[batch], maxDelta);
                batchMSE[batch] += deltaR * deltaR;
                batchMSE[batch] += deltaG * deltaG;
                batchMSE[batch] += deltaB * deltaB;
                batchMSE[batch] += deltaA * deltaA;
                diff[index] = new Color(maxDelta, maxDelta, maxDelta, 1.0f);
            }
        }

        // Linear RGB to XYZ using D65 ref. white
        static Vector3 RGBtoXYZ(Color color)
        {
            var x = color.r * 0.4124564f + color.g * 0.3575761f + color.b * 0.1804375f;
            var y = color.r * 0.2126729f + color.g * 0.7151522f + color.b * 0.0721750f;
            var z = color.r * 0.0193339f + color.g * 0.1191920f + color.b * 0.9503041f;
            return new Vector3(x * 100f, y * 100f, z * 100f);
        }

        // sRGB to JzAzBz
        // https://www.osapublishing.org/oe/fulltext.cfm?uri=oe-25-13-15131&id=368272
        static Vector3 RGBtoJAB(Color color)
        {
            var xyz = RGBtoXYZ(color.linear);

            const float kB  = 1.15f;
            const float kG  = 0.66f;
            const float kC1 = 0.8359375f;        // 3424 / 2^12
            const float kC2 = 18.8515625f;       // 2413 / 2^7
            const float kC3 = 18.6875f;          // 2392 / 2^7
            const float kN  = 0.15930175781f;    // 2610 / 2^14
            const float kP  = 134.034375f;       // 1.7 * 2523 / 2^5
            const float kD  = -0.56f;
            const float kD0 = 1.6295499532821566E-11f;

            var x2 = kB * xyz.x - (kB - 1f) * xyz.z;
            var y2 = kG * xyz.y - (kG - 1f) * xyz.x;

            var l = 0.41478372f * x2 + 0.579999f * y2 + 0.0146480f * xyz.z;
            var m = -0.2015100f * x2 + 1.120649f * y2 + 0.0531008f * xyz.z;
            var s = -0.0166008f * x2 + 0.264800f * y2 + 0.6684799f * xyz.z;
            l = Mathf.Pow(l / 10000f, kN);
            m = Mathf.Pow(m / 10000f, kN);
            s = Mathf.Pow(s / 10000f, kN);

            // Can we switch to unity.mathematics yet?
            var lms = new Vector3(l, m, s);
            var a = new Vector3(kC1, kC1, kC1) + kC2 * lms;
            var b = Vector3.one + kC3 * lms;
            var tmp = new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

            lms.x = Mathf.Pow(tmp.x, kP);
            lms.y = Mathf.Pow(tmp.y, kP);
            lms.z = Mathf.Pow(tmp.z, kP);

            var jab = new Vector3(
                0.5f * lms.x + 0.5f * lms.y,
                3.524000f * lms.x + -4.066708f * lms.y + 0.542708f * lms.z,
                0.199076f * lms.x + 1.096799f * lms.y + -1.295875f * lms.z
            );

            jab.x = ((1f + kD) * jab.x) / (1f + kD * jab.x) - kD0;

            return jab;
        }

        static float JABDeltaE(Vector3 v1, Vector3 v2)
        {
            var c1 = Mathf.Sqrt(v1.y * v1.y + v1.z * v1.z);
            var c2 = Mathf.Sqrt(v2.y * v2.y + v2.z * v2.z);

            var h1 = Mathf.Atan(v1.z / v1.y);
            var h2 = Mathf.Atan(v2.z / v2.y);

            var deltaH = 2f * Mathf.Sqrt(c1 * c2) * Mathf.Sin((h1 - h2) / 2f);
            var deltaE = Mathf.Sqrt(Mathf.Pow(v1.x - v2.x, 2f) + Mathf.Pow(c1 - c2, 2f) + deltaH * deltaH);
            return deltaE;
        }

        /// <summary>
        /// Resize a source texture to match the dimensions of the destination texture
        /// </summary>
        public static Texture2D ResizeInto(Texture2D source, Texture2D dest)
        {
            Color[] destPix = new Color[dest.width * dest.height];
            var y = 0;
            while (y < dest.height)
            {
                var x = 0;
                while (x < dest.width)
                {
                    var xFrac = x * 1.0F / (dest.width);
                    var yFrac = y * 1.0F / (dest.height);
                    destPix[y * dest.width + x] = source.GetPixelBilinear(xFrac, yFrac);
                    x++;
                }
                y++;
            }
            var format = source != null ? source.format : TextureFormat.ARGB32;

            var newImage = new Texture2D(dest.width, dest.height, format, false);
            newImage.SetPixels(destPix);
            newImage.Apply();
            Object.Destroy(dest);
            return newImage;
        }
    }

#if UNITY_EDITOR
    public class ImageHandler : ScriptableSingleton<ImageHandler>
    {
        public string ImageResultsPath;

        public class TextureImporterSettings
        {
            public bool IsReadable { get; set; } = true;
            public bool UseMipMaps { get; set; } = false;
            public TextureImporterNPOTScale NPOTScale { get; set; } = TextureImporterNPOTScale.None;
            public TextureImporterCompression TextureCompressionType { get; set; } = TextureImporterCompression.Uncompressed;
            public FilterMode TextureFilterMode { get; set; } = FilterMode.Point;
        }

        public static void ReImportTextureWithSettings(string path, TextureImporterSettings settings)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
                return;
            importer.isReadable = settings.IsReadable;
            importer.npotScale = settings.NPOTScale;
            importer.mipmapEnabled = settings.UseMipMaps;
            importer.textureCompression = settings.TextureCompressionType;
            importer.filterMode = settings.TextureFilterMode;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        public void SaveImage(FailedImageMessage failedImageMessage, bool hdr = false, TextureImporterSettings textureImporterSettings = null)
        {
            var saveDir = string.IsNullOrEmpty(ImageResultsPath) ? failedImageMessage.PathName : ImageResultsPath;

            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }
            var extension = hdr ? "exr" : "png";
            var actualImagePath = Path.Combine(saveDir, $"{failedImageMessage.ImageName}.{extension}");
            File.WriteAllBytes(actualImagePath, failedImageMessage.ActualImage);
            ReportArtifact(actualImagePath);
            if (textureImporterSettings != null)
                ReImportTextureWithSettings(actualImagePath, textureImporterSettings);

            if (failedImageMessage.DiffImage != null)
            {
                var diffImagePath = Path.Combine(saveDir, $"{failedImageMessage.ImageName}.diff.{extension}");
                File.WriteAllBytes(diffImagePath, failedImageMessage.DiffImage);
                ReportArtifact(diffImagePath);
                if (textureImporterSettings != null)
                    ReImportTextureWithSettings(diffImagePath, textureImporterSettings);

                var expectedImagesPath =
                    Path.Combine(saveDir, $"{failedImageMessage.ImageName}.expected.{extension}");
                File.WriteAllBytes(expectedImagesPath, failedImageMessage.ExpectedImage);
                ReportArtifact(expectedImagesPath);
                if (textureImporterSettings != null)
                    ReImportTextureWithSettings(expectedImagesPath, textureImporterSettings);
            }
        }

        void ReportArtifact(string artifactPath)
        {
            var fullpath = Path.GetFullPath(artifactPath);
            var message = ArtifactPublishMessage.Create(fullpath);
            Debug.Log(UnityTestProtocolMessageBuilder.Serialize(message));
        }
    }
#endif
}
