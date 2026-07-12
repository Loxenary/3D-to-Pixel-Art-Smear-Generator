using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using SmearFramework.PixelArtConversion;

namespace SmearFramework.Tests
{
    public class IOKMQuantizerTests
    {
        private static Color[] MakeFourColorImage()
        {
            var pixels = new Color[64]; // 8x8
            for (int i = 0; i < 16; i++) pixels[i] = Color.red;
            for (int i = 16; i < 32; i++) pixels[i] = Color.green;
            for (int i = 32; i < 48; i++) pixels[i] = Color.blue;
            for (int i = 48; i < 64; i++) pixels[i] = Color.yellow;
            return pixels;
        }

        [Test]
        public void Quantize_FourColorImage_K4_ReturnsFourPaletteEntries()
        {
            var pixels = MakeFourColorImage();
            var result = IOKMQuantizer.Quantize(pixels, k: 4);
            Assert.That(result.Palette.Length, Is.EqualTo(4));
        }

        [Test]
        public void Quantize_FourColorImage_EveryOutputPixelIsInPalette()
        {
            var pixels = MakeFourColorImage();
            var result = IOKMQuantizer.Quantize(pixels, k: 4);
            var palette = new HashSet<Color>(result.Palette);
            for (int i = 0; i < result.Output.Length; i++)
                Assert.That(palette.Contains(result.Output[i]), $"pixel {i} not in palette");
        }

        [Test]
        public void Quantize_IsDeterministic()
        {
            var pixels = MakeFourColorImage();
            var first = IOKMQuantizer.Quantize(pixels, k: 4);
            var second = IOKMQuantizer.Quantize(pixels, k: 4);
            for (int i = 0; i < first.Output.Length; i++)
            {
                Assert.That(first.Output[i].r, Is.EqualTo(second.Output[i].r).Within(1e-5f));
                Assert.That(first.Output[i].g, Is.EqualTo(second.Output[i].g).Within(1e-5f));
                Assert.That(first.Output[i].b, Is.EqualTo(second.Output[i].b).Within(1e-5f));
            }
        }

        [Test]
        public void Quantize_WithLUT_SnapsEveryPixelToLUT()
        {
            var pixels = MakeFourColorImage();
            var lut = new Color[] { Color.white, Color.black };
            var result = IOKMQuantizer.QuantizeWithLUT(pixels, lut);
            var lutSet = new HashSet<Color>(lut);
            for (int i = 0; i < result.Length; i++)
                Assert.That(lutSet.Contains(result[i]), $"pixel {i} not in LUT");
        }
    }
}
