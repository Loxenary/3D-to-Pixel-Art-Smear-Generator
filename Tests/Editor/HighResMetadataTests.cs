using NUnit.Framework;
using UnityEngine;
using SmearFramework.DataTypes;

namespace SmearFramework.Tests
{
    public class HighResMetadataTests
    {
        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var meta = new HighResMetadata
            {
                schema_version = 1,
                prefab = "Spinkick",
                clip = "spinkick_anim",
                baked_at = "2026-04-17T14:30:00",
                sheet_width = 2048,
                sheet_height = 2048,
                cell_width = 256,
                cell_height = 256,
                cols = 8,
                rows = 8,
                frame_count = 24,
                fps = 12,
                smeared_count = 7,
                smear_intensity = new[] { 0f, 0f, 0.12f, 0.45f }
            };

            var json = JsonUtility.ToJson(meta);
            var roundTrip = JsonUtility.FromJson<HighResMetadata>(json);

            Assert.AreEqual(meta.schema_version, roundTrip.schema_version);
            Assert.AreEqual(meta.prefab, roundTrip.prefab);
            Assert.AreEqual(meta.clip, roundTrip.clip);
            Assert.AreEqual(meta.sheet_width, roundTrip.sheet_width);
            Assert.AreEqual(meta.frame_count, roundTrip.frame_count);
            Assert.AreEqual(meta.smeared_count, roundTrip.smeared_count);
            Assert.AreEqual(meta.smear_intensity.Length, roundTrip.smear_intensity.Length);
        }

        [Test]
        public void MissingOptionalSmearIntensity_Deserializes()
        {
            const string json = "{\"schema_version\":1,\"prefab\":\"p\",\"clip\":\"c\",\"frame_count\":24}";
            var m = JsonUtility.FromJson<HighResMetadata>(json);
            Assert.AreEqual(24, m.frame_count);
            Assert.IsNotNull(m.smear_intensity);
        }

        [Test]
        public void SchemaVersion_DefaultsToOne()
        {
            var m = new HighResMetadata();
            Assert.AreEqual(1, m.schema_version);
        }

        [Test]
        public void SpriteSheetMetadata_RoundTrip_PreservesFrameEntries()
        {
            var meta = new SpriteSheetMetadata
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

            var json = JsonUtility.ToJson(meta);
            var roundTrip = JsonUtility.FromJson<SpriteSheetMetadata>(json);

            Assert.AreEqual(1, roundTrip.schema_version);
            Assert.AreEqual("pixel", roundTrip.outputMode);
            Assert.AreEqual("SpinKick", roundTrip.characterName);
            Assert.AreEqual(2, roundTrip.frames.Length);
            Assert.AreEqual("frame_001", roundTrip.frames[1].spriteName);
            Assert.IsTrue(roundTrip.frames[1].hasSmear);
            Assert.AreEqual(0.4f, roundTrip.frames[1].smearIntensity);
        }

        [Test]
        public void PackageManifest_RoundTrip_PreservesAssetFileNames()
        {
            var manifest = new PixelAnimationPackageManifest
            {
                schema_version = 1,
                packageType = "smear_pixel_animation",
                characterName = "SpinKick",
                clipName = "mixamo_com",
                outputMode = "pixel",
                spriteSheetFile = "sprite_sheet.png",
                animationFile = "animation.json",
                clipAssetFile = "SpinKick_mixamo_com_2d.anim",
                controllerAssetFile = "SpinKick_mixamo_com_2d.controller",
                prefabAssetFile = "SpinKick_mixamo_com_2d.prefab",
                generatedAt = "2026-06-13T10:20:00"
            };

            var json = JsonUtility.ToJson(manifest);
            var roundTrip = JsonUtility.FromJson<PixelAnimationPackageManifest>(json);

            Assert.AreEqual("smear_pixel_animation", roundTrip.packageType);
            Assert.AreEqual("SpinKick_mixamo_com_2d.anim", roundTrip.clipAssetFile);
            Assert.AreEqual("SpinKick_mixamo_com_2d.controller", roundTrip.controllerAssetFile);
            Assert.AreEqual("SpinKick_mixamo_com_2d.prefab", roundTrip.prefabAssetFile);
        }
    }
}
