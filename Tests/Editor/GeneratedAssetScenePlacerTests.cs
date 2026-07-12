using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SmearFramework.Editor;

namespace SmearFramework.Tests
{
    public class GeneratedAssetScenePlacerTests
    {
        private const string ROOT = SmearFrameworkPaths.TestTemp + "/GeneratedAssetScenePlacerTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(ROOT);
            Directory.CreateDirectory(ToSystemPath(ROOT));
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(ROOT);
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void PlacePrefab_InstantiatesPrefabInActiveScene()
        {
            // create and save a prefab, then discard the temp root object
            var tempRoot = new GameObject("GeneratedPrefab");
            string prefabPath = ROOT + "/GeneratedPrefab.prefab";
            PrefabUtility.SaveAsPrefabAsset(tempRoot, prefabPath);
            Object.DestroyImmediate(tempRoot);

            var result = GeneratedAssetScenePlacer.PlacePrefab(prefabPath);

            Assert.IsTrue(result.Success, "Expected Success=true but got: " + result.Message);
            Assert.IsNotNull(result.Instance);
            Assert.IsTrue(result.Instance.scene.IsValid(), "Instance scene should be valid");
            Assert.AreEqual("GeneratedPrefab", result.Instance.name);
            Assert.AreEqual(result.Instance, Selection.activeGameObject);
        }

        [Test]
        public void PlacePrefab_SelectsExistingInstanceOnSecondPlacement()
        {
            var tempRoot = new GameObject("GeneratedPrefab");
            string prefabPath = ROOT + "/GeneratedPrefab.prefab";
            PrefabUtility.SaveAsPrefabAsset(tempRoot, prefabPath);
            Object.DestroyImmediate(tempRoot);

            Assert.IsFalse(GeneratedAssetScenePlacer.HasSceneInstance(prefabPath));

            var first = GeneratedAssetScenePlacer.PlacePrefab(prefabPath);
            Assert.IsTrue(GeneratedAssetScenePlacer.HasSceneInstance(prefabPath));
            first.Instance.transform.position = new Vector3(2f, 0f, 0f);

            var second = GeneratedAssetScenePlacer.PlacePrefab(prefabPath);

            Assert.IsTrue(second.Success, "Expected Success=true but got: " + second.Message);
            Assert.AreEqual(first.Instance, second.Instance);
            Assert.AreEqual(new Vector3(2f, 0f, 0f), second.Instance.transform.position);
            Assert.AreEqual(1, CountScenePrefabRoots());
            StringAssert.Contains("Selected existing", second.Message);
        }

        [Test]
        public void PlacePrefab_MissingPathReturnsFailure()
        {
            var result = GeneratedAssetScenePlacer.PlacePrefab(ROOT + "/Missing.prefab");

            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Instance);
            StringAssert.Contains("Generated prefab could not be loaded", result.Message);
        }

        // Count root prefab instances in the active scene.
        int CountScenePrefabRoots()
        {
            int count = 0;
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var transforms = roots[r].GetComponentsInChildren<Transform>(true);
                for (int t = 0; t < transforms.Length; t++)
                {
                    var go = transforms[t].gameObject;
                    if (PrefabUtility.GetNearestPrefabInstanceRoot(go) == go)
                        count++;
                }
            }
            return count;
        }

        // Convert an asset path into a disk path under the project root.
        string ToSystemPath(string assetPath)
        {
            return Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), assetPath);
        }
    }
}
