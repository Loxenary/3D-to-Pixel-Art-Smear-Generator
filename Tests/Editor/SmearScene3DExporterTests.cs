using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEngine;
using SmearFramework.Editor;

namespace SmearFramework.Tests
{
    public class SmearScene3DExporterTests
    {
        const string ROOT = SmearFrameworkPaths.TestTemp + "/SmearScene3DExporterTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(ROOT);
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ROOT)));
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(ROOT);
            AssetDatabase.Refresh();
        }

        // Verifies that ClearExistingOutput removes the prefab, clip, and mesh folder left from a previous run.
        [Test]
        public void ClearExistingOutput_RemovesPrefabClipAndMeshFolder()
        {
            string prefabPath   = $"{ROOT}/Runner_smear3D.prefab";
            string clipPath     = $"{ROOT}/Runner_smear3D.anim";
            string meshFolder   = $"{ROOT}/Runner_smear3D_meshes";
            string staleMeshPath = $"{meshFolder}/frame_999.asset";

            // Arrange: create the stale assets that a prior export would leave behind.
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..", meshFolder)));
            AssetDatabase.Refresh();

            var tmpGo = new GameObject("Runner");
            PrefabUtility.SaveAsPrefabAsset(tmpGo, prefabPath);
            Object.DestroyImmediate(tmpGo);

            AssetDatabase.CreateAsset(new AnimationClip(), clipPath);
            AssetDatabase.CreateAsset(new Mesh(), staleMeshPath);
            AssetDatabase.Refresh();

            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(prefabPath), "prefab should exist before clear");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(clipPath),   "clip should exist before clear");
            Assert.IsTrue(AssetDatabase.IsValidFolder(meshFolder),              "mesh folder should exist before clear");

            // Act
            SmearScene3DExporter.ClearExistingOutput(ROOT, "Runner");
            AssetDatabase.Refresh();

            // Assert
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Object>(prefabPath), "prefab should be gone after clear");
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Object>(clipPath),   "clip should be gone after clear");
            Assert.IsFalse(AssetDatabase.IsValidFolder(meshFolder),           "mesh folder should be gone after clear");
        }
    }
}
