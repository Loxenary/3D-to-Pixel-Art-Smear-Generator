using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.Editor;

namespace SmearFramework.Tests
{
    public class PixelAnimationPackageImporterTests
    {
        private const string SOURCE_ROOT = SmearFrameworkPaths.TestTemp + "/PixelAnimationPackageImporterTests";
        private const string IMPORT_NAME = "roundhouse_imported";
        private const string IMPORT_FOLDER = SmearFrameworkPaths.ImportedPackages + "/" + IMPORT_NAME;
        private static readonly string EXTERNAL_ROOT = Path.Combine(
            Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
            "Temp/PixelAnimationPackageImporterTests");

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(SOURCE_ROOT);
            AssetDatabase.DeleteAsset(IMPORT_FOLDER);
            Directory.CreateDirectory(ToSystemPath(SOURCE_ROOT));
            if (Directory.Exists(EXTERNAL_ROOT))
                Directory.Delete(EXTERNAL_ROOT, recursive: true);
            Directory.CreateDirectory(EXTERNAL_ROOT);
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(SOURCE_ROOT);
            AssetDatabase.DeleteAsset(IMPORT_FOLDER);
            if (Directory.Exists(EXTERNAL_ROOT))
                Directory.Delete(EXTERNAL_ROOT, recursive: true);
            AssetDatabase.Refresh();
        }

        [Test]
        public void ImportExternalFolder_RebuildsLocalUnityAssetsFromPortableFolder()
        {
            var sheet = MakeSheet(new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255));
            var source = PixelAnimationPackageExporter.Export(
                SOURCE_ROOT, "SpinKick", "mixamo.com", sheet, MakeMetadata());
            string portableFolder = PixelPackagePortableExporter.ExportToExternalFolder(
                new SpriteSheetResult
                {
                    PackageFolder = ToSystemPath(source.PackageFolder),
                    PrefabPath = source.PrefabPath
                },
                EXTERNAL_ROOT,
                "roundhouse");

            var result = PixelAnimationPackageImporter.ImportExternalFolder(
                portableFolder, SmearFrameworkPaths.ImportedPackages, IMPORT_NAME);

            Assert.AreEqual(IMPORT_FOLDER, result.PackageFolder);
            Assert.IsTrue(File.Exists(ToSystemPath(IMPORT_FOLDER + "/roundhouse_imported.png")));
            Assert.IsTrue(File.Exists(ToSystemPath(IMPORT_FOLDER + "/animation.json")));
            Assert.IsTrue(File.Exists(ToSystemPath(IMPORT_FOLDER + "/package.json")));
            Assert.IsTrue(File.Exists(ToSystemPath(IMPORT_FOLDER + "/roundhouse_imported_2d.anim")));
            Assert.IsTrue(File.Exists(ToSystemPath(IMPORT_FOLDER + "/roundhouse_imported_2d.controller")));
            Assert.IsTrue(File.Exists(ToSystemPath(IMPORT_FOLDER + "/roundhouse_imported_2d.prefab")));

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(result.ClipPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(result.ControllerPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            Assert.IsNotNull(clip);
            Assert.IsNotNull(controller);
            Assert.IsNotNull(prefab);
            Assert.AreSame(clip, controller.layers[0].stateMachine.defaultState.motion);
            Assert.IsTrue(AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(
                binding => binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite"));

            var renderer = prefab.GetComponent<SpriteRenderer>();
            var animator = prefab.GetComponent<Animator>();
            Assert.IsNotNull(renderer);
            Assert.IsNotNull(renderer.sprite);
            Assert.IsNotNull(animator);
            Assert.AreSame(controller, animator.runtimeAnimatorController);

            UnityEngine.Object.DestroyImmediate(sheet);
        }

        [Test]
        public void ImportExternalFolder_RejectsInvalidPackageManifest()
        {
            string sourceFolder = Path.Combine(EXTERNAL_ROOT, "invalid");
            Directory.CreateDirectory(sourceFolder);
            File.WriteAllText(
                Path.Combine(sourceFolder, "package.json"),
                JsonUtility.ToJson(new PixelAnimationPackageManifest
                {
                    schema_version = 1,
                    packageType = "not_a_pixel_animation"
                }));

            var ex = Assert.Throws<InvalidOperationException>(() =>
                PixelAnimationPackageImporter.ImportExternalFolder(
                    sourceFolder, SmearFrameworkPaths.ImportedPackages));

            StringAssert.StartsWith("unsupported pixel package:", ex.Message);
        }

        Texture2D MakeSheet(Color32 left, Color32 right)
        {
            var texture = new Texture2D(128, 64, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            var pixels = new Color32[128 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 128; x++)
                    pixels[y * 128 + x] = x < 64 ? left : right;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

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
                    new SpriteSheetFrameMetadata
                    {
                        index = 0,
                        spriteName = "frame_000",
                        x = 0,
                        y = 0,
                        width = 64,
                        height = 64,
                        hasSmear = false,
                        smearIntensity = 0f
                    },
                    new SpriteSheetFrameMetadata
                    {
                        index = 1,
                        spriteName = "frame_001",
                        x = 64,
                        y = 0,
                        width = 64,
                        height = 64,
                        hasSmear = true,
                        smearIntensity = 0.4f
                    }
                }
            };
        }

        string ToSystemPath(string assetPath)
        {
            return Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), assetPath);
        }
    }
}
