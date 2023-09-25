using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.Media.Keyer.Tests.Editor
{
    public class EditorTestsUtilities
    {
        public static void CleanUpDirectoryContent(string directoryPath)
        {
            var info = new DirectoryInfo(directoryPath);
            if (info.Exists)
            {
                info.Delete(true);
            }
            File.Delete(directoryPath + ".meta");
            AssetDatabase.Refresh();
        }
    }
}
