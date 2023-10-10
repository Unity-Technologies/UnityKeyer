using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Unity.Media.Keyer.Tests
{
    // Check whether Keyer resources are ready to use after loading Keyer package and creating a new scene
    [InitializeOnLoad]
    class PokeResources
    {
        static PokeResources()
        {
            // Add a callback which pokes Keyer resources after a new scene is created
            EditorSceneManager.newSceneCreated += OnSceneCreated;
        }

        private static void OnSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            var shader = KeyerResources.GetInstance().Shaders.Blit;
            if (shader == null)
            {
                throw new InvalidOperationException("Could not find Blit shader!");
            }
        }
    }
}

