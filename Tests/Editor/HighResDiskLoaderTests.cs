using System.IO;
using NUnit.Framework;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.PixelArtConversion;
using SmearFramework.Stages;

namespace SmearFramework.Tests
{
    public class HighResDiskLoaderTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Application.dataPath, "..", "Temp", "HighResDiskLoaderTests");
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [Test]
        public void RoundTrip_WriteThenLoad_PreservesFrameCount()
        {
            var raw = MakeRaw(4, 16, 16);
            var meta = new HighResMetadata { prefab = "p", clip = "c", frame_count = 4, fps = 12 };
            HighResDiskWriter.Save(_folder, "t", raw, meta);

            var (loaded, loadedMeta) = HighResDiskLoader.Load(Path.Combine(_folder, "t_highres.png"));
            Assert.AreEqual(4, loaded.FrameCount);
            Assert.AreEqual(16, loaded.Width);
            Assert.AreEqual(16, loaded.Height);
            Assert.AreEqual(12, loadedMeta.fps);

            DisposeRaw(raw);
            DisposeRaw(loaded);
        }

        [Test]
        public void Load_MissingJson_Throws()
        {
            var pngPath = Path.Combine(_folder, "orphan_highres.png");
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            Assert.Throws<FileNotFoundException>(() => HighResDiskLoader.Load(pngPath));
        }

        [Test]
        public void Load_UnknownSchemaVersion_Throws()
        {
            var raw = MakeRaw(4, 16, 16);
            var meta = new HighResMetadata { prefab = "p", clip = "c", frame_count = 4, fps = 12 };
            HighResDiskWriter.Save(_folder, "t", raw, meta);
            DisposeRaw(raw);

            var jsonPath = Path.Combine(_folder, "t_highres.json");
            var bad = File.ReadAllText(jsonPath).Replace("\"schema_version\": 1", "\"schema_version\": 99");
            File.WriteAllText(jsonPath, bad);

            Assert.Throws<System.InvalidOperationException>(() =>
                HighResDiskLoader.Load(Path.Combine(_folder, "t_highres.png")));
        }

        [Test]
        public void Load_DimensionMismatch_Throws()
        {
            var raw = MakeRaw(4, 16, 16);
            var meta = new HighResMetadata { prefab = "p", clip = "c", frame_count = 4, fps = 12 };
            HighResDiskWriter.Save(_folder, "t", raw, meta);
            DisposeRaw(raw);

            var jsonPath = Path.Combine(_folder, "t_highres.json");
            var bad = File.ReadAllText(jsonPath).Replace("\"frame_count\": 4", "\"frame_count\": 999");
            File.WriteAllText(jsonPath, bad);

            Assert.Throws<System.InvalidOperationException>(() =>
                HighResDiskLoader.Load(Path.Combine(_folder, "t_highres.png")));
        }

        // Writes high-res frames to disk, reloads via HighResDiskLoader, feeds to PixelizationStage -- TF-15.
        [Test]
        public void DiskLoaded_FedToPixelizationStage_ProducesFramesPixelized()
        {
            var raw = MakeRaw(2, 64, 64);
            var meta = new HighResMetadata { prefab = "p", clip = "c", frame_count = 2, fps = 12 };
            HighResDiskWriter.Save(_folder, "pix", raw, meta);
            DisposeRaw(raw);

            var (loaded, _) = HighResDiskLoader.Load(Path.Combine(_folder, "pix_highres.png"));
            var config = ScriptableObject.CreateInstance<PipelineConfig>();
            try
            {
                var ctx = new PipelineContext(config, null, null);
                ctx.Set("frames_highres", loaded);
                new PixelizationStage().Execute(ctx);

                var pixelized = ctx.Get<RawFrameData>("frames_pixelized");
                Assert.IsNotNull(pixelized, "frames_pixelized must be present after PixelizationStage");
                Assert.AreEqual(2, pixelized.FrameCount);
                for (int f = 0; f < 2; f++)
                    Assert.IsNotNull(pixelized.Frames[f], $"frame {f} must be non-null");
            }
            finally
            {
                Object.DestroyImmediate(config);
                DisposeRaw(loaded);
            }
        }

        private RawFrameData MakeRaw(int count, int w, int h)
        {
            var raw = new RawFrameData(count, w, h);
            for (int i = 0; i < count; i++)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var pixels = new Color32[w * h];
                for (int p = 0; p < pixels.Length; p++) pixels[p] = new Color32((byte)(i * 60), 128, 64, 255);
                t.SetPixels32(pixels);
                t.Apply();
                raw.Frames[i] = t;
            }
            return raw;
        }

        private void DisposeRaw(RawFrameData raw)
        {
            for (int i = 0; i < raw.FrameCount; i++)
                if (raw.Frames[i] != null) Object.DestroyImmediate(raw.Frames[i]);
        }
    }
}
