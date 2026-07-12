using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.Stages;

namespace SmearFramework.Tests
{
    public class PixelizationStageTests
    {
        private PipelineContext _ctx;
        private PipelineConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<PipelineConfig>();
            _ctx = new PipelineContext(_config, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null) Object.DestroyImmediate(_config);
        }

        [Test]
        public void Execute_NoRawFrames_Throws()
        {
            var stage = new PixelizationStage();
            Assert.Throws<KeyNotFoundException>(() => stage.Execute(_ctx));
        }

        [Test]
        public void Execute_3Frames_ProducesMatchingPixelizedFrames()
        {
            var raw = MakeSyntheticRaw(frameCount: 3, size: 64);
            _ctx.Set("frames_highres", raw);
            var stage = new PixelizationStage();
            stage.Execute(_ctx);

            var pixelized = _ctx.Get<RawFrameData>("frames_pixelized");
            Assert.IsNotNull(pixelized);
            Assert.That(pixelized.FrameCount, Is.EqualTo(3));
            Assert.That(pixelized.Width, Is.EqualTo(_config.PixelWidth));
            Assert.That(pixelized.Height, Is.EqualTo(_config.PixelHeight));
            for (int f = 0; f < 3; f++)
                Assert.IsNotNull(pixelized.Frames[f]);
        }

        [Test]
        public void Execute_PaletteSizeRespected()
        {
            var raw = MakeSyntheticRaw(frameCount: 1, size: 64);
            _ctx.Set("frames_highres", raw);
            new PixelizationStage().Execute(_ctx);

            var pixels = _ctx.Get<RawFrameData>("frames_pixelized").Frames[0].GetPixels();
            var uniqueColors = new System.Collections.Generic.HashSet<Color>();
            for (int i = 0; i < pixels.Length; i++) uniqueColors.Add(pixels[i]);
            Assert.That(uniqueColors.Count, Is.LessThanOrEqualTo(_config.PostProcessPaletteSize));
        }

        private static RawFrameData MakeSyntheticRaw(int frameCount, int size)
        {
            var data = new RawFrameData(frameCount, size, size);
            for (int f = 0; f < frameCount; f++)
                data.Frames[f] = MakeGradient(size);
            return data;
        }

        private static Texture2D MakeGradient(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color((float)x / size, (float)y / size, 0.5f);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
