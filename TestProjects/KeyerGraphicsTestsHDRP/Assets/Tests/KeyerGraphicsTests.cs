using System;
using System.Collections;
using NUnit.Framework;
using Unity.Media.Keyer;
using Unity.Media.Keyer.GraphicsTests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[TestFixture]
public class KeyerGraphicsTests
{
    [UnityTest]
    [TestCase("KeyerResultVanilla", ExpectedResult = null)]
    [TestCase("KeyerResultWithCrop", ExpectedResult = null)]
    [TestCase("KeyerResultWithSoftMask", ExpectedResult = null)]
    [TestCase("KeyerColorDistanceCoreMask", ExpectedResult = null)]
    [TestCase("KeyerCoreMaskWithErode", ExpectedResult = null)]
    [TestCase("KeyerDespillFix", ExpectedResult = null)]
    public IEnumerator LoadSceneAndCompareResult(string fileName)
    {
        var asyncLoad = SceneManager.LoadSceneAsync(fileName);
        yield return new WaitUntil(() => asyncLoad.isDone);

#if UNITY_2023_1_OR_NEWER
        var keyer = Object.FindFirstObjectByType<Keyer>();
        var settings = Object.FindFirstObjectByType<ImageComparisonSettings>();
#else
        var keyer = Object.FindObjectOfType<Keyer>();
        var settings = Object.FindObjectOfType<ImageComparisonSettings>();
#endif

        Assert.IsNotNull(keyer);
        Assert.IsNotNull(settings);
        Assert.IsTrue(keyer.enabled);
        yield return new WaitForEndOfFrame();

        // Reference image name is the same as scene file name
        Helpers.CompareKeyerResultAgainstReference(fileName, keyer.Result, settings);
    }
}
