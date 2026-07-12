using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SmearFramework.DataTypes;
using SmearFramework.Editor;

namespace SmearFramework.Tests
{
    public class PixelAnimationPackageExporterTests
    {
        private const string ROOT = SmearFrameworkPaths.TestTemp + "/PixelAnimationPackageExporterTests";
        private static readonly string EXTERNAL_ROOT = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Temp/PixelPackagePortableExporterTests");

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(ROOT);
            Directory.CreateDirectory(ToSystemPath(ROOT));
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(ROOT);
            if (Directory.Exists(EXTERNAL_ROOT))
                Directory.Delete(EXTERNAL_ROOT, recursive: true);
            AssetDatabase.Refresh();
        }

        [Test]
        public void BuildPackageFolder_SanitizesCharacterAndClipNames()
        {
            string folder = PixelAnimationPackageExporter.BuildPackageFolder(
                ROOT, "Spin Kick(Clone)", "mixamo.com", "pixel");

            Assert.AreEqual(
                "Assets/SmearFramework.Generated/Temp/Tests/PixelAnimationPackageExporterTests/Spin_Kick/mixamo_com/pixel",
                folder);
        }

        [Test]
        public void Export_CreatesSpriteAssetsClipAndPrefab()
        {
            var sheet = MakeSheet(new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255));
            var meta = MakeMetadata();

            var result = PixelAnimationPackageExporter.Export(ROOT, "SpinKick", "mixamo.com", sheet, meta);

            Assert.IsTrue(File.Exists(ToSystemPath(result.SpriteSheetPath)));
            Assert.IsTrue(File.Exists(ToSystemPath(result.AnimationJsonPath)));
            Assert.IsTrue(File.Exists(ToSystemPath(result.PackageJsonPath)));

            var sprites = AssetDatabase.LoadAllAssetsAtPath(result.SpriteSheetPath).OfType<Sprite>().ToArray();
            Assert.AreEqual(2, sprites.Length);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(result.ClipPath);
            Assert.IsNotNull(clip);
            Assert.IsFalse(clip.legacy);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(result.ControllerPath);
            Assert.IsNotNull(controller);
            Assert.AreSame(clip, controller.layers[0].stateMachine.defaultState.motion);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(prefab.GetComponent<SpriteRenderer>());
            Assert.IsNull(prefab.GetComponent<Animation>());
            var animator = prefab.GetComponent<Animator>();
            Assert.IsNotNull(animator);
            Assert.AreSame(controller, animator.runtimeAnimatorController);

            Object.DestroyImmediate(sheet);
        }

        [Test]
        public void Export_ReusesPrefabAssetGuidAcrossIterations()
        {
            var firstSheet = MakeSheet(new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255));
            var secondSheet = MakeSheet(new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255));
            var meta = MakeMetadata();

            var first = PixelAnimationPackageExporter.Export(ROOT, "SpinKick", "mixamo.com", firstSheet, meta);
            string firstGuid = AssetDatabase.AssetPathToGUID(first.PrefabPath);

            var second = PixelAnimationPackageExporter.Export(ROOT, "SpinKick", "mixamo.com", secondSheet, meta);
            string secondGuid = AssetDatabase.AssetPathToGUID(second.PrefabPath);

            Assert.AreEqual(first.PrefabPath, second.PrefabPath);
            Assert.AreEqual(firstGuid, secondGuid);

            Object.DestroyImmediate(firstSheet);
            Object.DestroyImmediate(secondSheet);
        }


        [Test]
        public void Export_RebakeKeepsPlacedPrefabLinkedForIteration()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var firstSheet = MakeSheet(new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255));
            var secondSheet = MakeSheet(new Color32(0, 0, 255, 255), new Color32(255, 255, 0, 255));
            var meta = MakeMetadata();

            var first = PixelAnimationPackageExporter.Export(ROOT, "SpinKick", "mixamo.com", firstSheet, meta);
            string firstGuid = AssetDatabase.AssetPathToGUID(first.PrefabPath);
            byte[] firstBytes = File.ReadAllBytes(ToSystemPath(first.SpriteSheetPath));

            var placed = GeneratedAssetScenePlacer.PlacePrefab(first.PrefabPath);
            Assert.IsTrue(placed.Success, "Expected first placement to succeed but got: " + placed.Message);
            placed.Instance.transform.position = new Vector3(3f, 1f, 0f);

            var second = PixelAnimationPackageExporter.Export(ROOT, "SpinKick", "mixamo.com", secondSheet, meta);
            string secondGuid = AssetDatabase.AssetPathToGUID(second.PrefabPath);
            byte[] secondBytes = File.ReadAllBytes(ToSystemPath(second.SpriteSheetPath));

            var reused = GeneratedAssetScenePlacer.PlacePrefab(second.PrefabPath);

            Assert.AreEqual(first.PrefabPath, second.PrefabPath);
            Assert.AreEqual(firstGuid, secondGuid);
            Assert.IsFalse(firstBytes.SequenceEqual(secondBytes), "Expected the re-bake output to change after the input sheet changed.");
            Assert.IsTrue(GeneratedAssetScenePlacer.HasSceneInstance(second.PrefabPath));
            Assert.IsTrue(reused.Success, "Expected second placement to succeed but got: " + reused.Message);
            Assert.AreSame(placed.Instance, reused.Instance);
            Assert.AreEqual(new Vector3(3f, 1f, 0f), reused.Instance.transform.position);
            Assert.AreSame(
                AssetDatabase.LoadAssetAtPath<GameObject>(second.PrefabPath),
                PrefabUtility.GetCorrespondingObjectFromSource(reused.Instance) as GameObject);
            Assert.IsNotNull(reused.Instance.GetComponent<Animator>());
            Assert.AreEqual(1, CountScenePrefabRoots());
            StringAssert.Contains("Selected existing", reused.Message);

            Object.DestroyImmediate(firstSheet);
            Object.DestroyImmediate(secondSheet);
        }
        [Test]
        public void ExportToExternalFolder_RewritesPackageWithChosenFilename()
        {
            var sheet = MakeSheet(new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255));
            var meta = MakeMetadata();

            var result = PixelAnimationPackageExporter.Export(ROOT, "SpinKick", "mixamo.com", sheet, meta);
            string destPackage = PixelPackagePortableExporter.ExportToExternalFolder(
                new SpriteSheetResult
                {
                    PackageFolder = ToSystemPath(result.PackageFolder),
                    PrefabPath = result.PrefabPath
                },
                EXTERNAL_ROOT,
                "roundhouse");

            Assert.AreEqual("roundhouse", Path.GetFileName(destPackage));
            Assert.IsTrue(Directory.Exists(destPackage));
            Assert.IsTrue(File.Exists(Path.Combine(destPackage, "roundhouse.png")));
            Assert.IsTrue(File.Exists(Path.Combine(destPackage, "animation.json")));
            Assert.IsTrue(File.Exists(Path.Combine(destPackage, "package.json")));
            Assert.IsTrue(File.Exists(Path.Combine(destPackage, "roundhouse_2d.anim")));
            Assert.IsTrue(File.Exists(Path.Combine(destPackage, "roundhouse_2d.controller")));
            Assert.IsTrue(File.Exists(Path.Combine(destPackage, "roundhouse_2d.prefab")));
            Assert.IsFalse(File.Exists(Path.Combine(destPackage, "sprite_sheet.png.meta")));

            var exportedMeta = JsonUtility.FromJson<SpriteSheetMetadata>(File.ReadAllText(Path.Combine(destPackage, "animation.json")));
            Assert.AreEqual("roundhouse.png", exportedMeta.sheetFile);
            Assert.AreEqual("roundhouse_0000", exportedMeta.frames[0].spriteName);
            Assert.AreEqual("roundhouse_0001", exportedMeta.frames[1].spriteName);

            var manifest = JsonUtility.FromJson<PixelAnimationPackageManifest>(File.ReadAllText(Path.Combine(destPackage, "package.json")));
            Assert.AreEqual("roundhouse.png", manifest.spriteSheetFile);
            Assert.AreEqual("roundhouse_2d.anim", manifest.clipAssetFile);
            Assert.AreEqual("roundhouse_2d.controller", manifest.controllerAssetFile);
            Assert.AreEqual("roundhouse_2d.prefab", manifest.prefabAssetFile);

            Object.DestroyImmediate(sheet);
        }

        // Build a tiny 2-frame sheet for importer tests.
        Texture2D MakeSheet(Color32 left, Color32 right)
        {
            var tex = new Texture2D(128, 64, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            var pixels = new Color32[128 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 128; x++)
                    pixels[y * 128 + x] = x < 64 ? left : right;
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
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

        // Build metadata that matches the 2-frame test sheet.
        SpriteSheetMetadata MakeMetadata()
        {
            return new SpriteSheetMetadata
            {
                schema_version = 1,
                outputMode = "pixel",
                characterName = "SpinKick",
                clipName = "mixamo_com",
                sheetFile = "sprite_sheet.png",
                frameCount = 2,
                frameWidth = 64,
                frameHeight = 64,
                columns = 2,
                rows = 1,
                fps = 12,
                loopPlayback = true,
                pixelsPerUnit = 32,
                pivotX = 0.5f,
                pivotY = 0f,
                frameDuration = 1f / 12f,
                totalDuration = 2f / 12f,
                sheetWidth = 128,
                sheetHeight = 64,
                captureResolution = 512,
                smearEnabled = true,
                frames = new[]
                {
                    new SpriteSheetFrameMetadata { index = 0, spriteName = "frame_000", x = 0, y = 0, width = 64, height = 64, hasSmear = false, smearIntensity = 0f },
                    new SpriteSheetFrameMetadata { index = 1, spriteName = "frame_001", x = 64, y = 0, width = 64, height = 64, hasSmear = true, smearIntensity = 0.4f },
                }
            };
        }

        // Convert an asset path into a disk path under the project root.
        string ToSystemPath(string assetPath)
        {
            return Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), assetPath);
        }
    }
}
