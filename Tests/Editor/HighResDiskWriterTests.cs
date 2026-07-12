using System.IO;
using NUnit.Framework;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.PixelArtConversion;

namespace SmearFramework.Tests
{
    public class HighResDiskWriterTests
    {
        private string _tempFolder;

        [SetUp]
        public void SetUp()
        {
            _tempFolder = Path.Combine(Application.dataPath, "..", "Temp", "HighResDiskWriterTests");
            if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, true);
            Directory.CreateDirectory(_tempFolder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempFolder)) Directory.Delete(_tempFolder, true);
        }

        [Test]
        public void Save_WritesPngAndJson()
        {
            var frames = MakeFrames(4, 32, 32);
            var raw = new RawFrameData(frames.Length, 32, 32);
            for (int i = 0; i < frames.Length; i++) raw.Frames[i] = frames[i];

            var meta = new HighResMetadata
            {
                prefab = "test",
                clip = "clip",
                frame_count = 4,
                fps = 12
            };

            var (pngPath, jsonPath) = HighResDiskWriter.Save(_tempFolder, "test_clip", raw, meta);

            Assert.IsTrue(File.Exists(pngPath));
            Assert.IsTrue(File.Exists(jsonPath));

            foreach (var f in frames) Object.DestroyImmediate(f);
        }

        [Test]
        public void Save_EmbedsMetadataIntoSheetFields()
        {
            var frames = MakeFrames(4, 16, 16);
            var raw = new RawFrameData(frames.Length, 16, 16);
            for (int i = 0; i < frames.Length; i++) raw.Frames[i] = frames[i];

            var meta = new HighResMetadata { prefab = "p", clip = "c", frame_count = 4, fps = 12 };
            var (_, jsonPath) = HighResDiskWriter.Save(_tempFolder, "p_c", raw, meta);

            var json = File.ReadAllText(jsonPath);
            var loaded = JsonUtility.FromJson<HighResMetadata>(json);

            Assert.AreEqual(16, loaded.cell_width);
            Assert.AreEqual(16, loaded.cell_height);
            Assert.AreEqual(4, loaded.frame_count);
            Assert.That(loaded.cols * loaded.rows, Is.GreaterThanOrEqualTo(4));

            foreach (var f in frames) Object.DestroyImmediate(f);
        }

        [Test]
        public void BuildBaseName_StripsCloneAndPunctuation()
        {
            string baseName = OutputNameUtility.BuildBaseName("Spin Kick(Clone)", "mixamo.com");
            Assert.AreEqual("Spin_Kick_mixamo_com", baseName);
        }

        [Test]
        public void SanitizeSegment_TempNameFallsBack()
        {
            string segment = OutputNameUtility.SanitizeSegment("_BakeTemp", "character");
            Assert.AreEqual("character", segment);
        }

        private Texture2D[] MakeFrames(int count, int w, int h)
        {
            var frames = new Texture2D[count];
            for (int i = 0; i < count; i++)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var pixels = new Color32[w * h];
                for (int p = 0; p < pixels.Length; p++)
                    pixels[p] = new Color32((byte)(i * 60), 128, 64, 255);
                t.SetPixels32(pixels);
                t.Apply();
                frames[i] = t;
            }
            return frames;
        }
    }
}
