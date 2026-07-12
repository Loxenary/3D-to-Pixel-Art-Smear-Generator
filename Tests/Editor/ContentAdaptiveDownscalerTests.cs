using NUnit.Framework;
using UnityEngine;
using SmearFramework.PixelArtConversion;

namespace SmearFramework.Tests
{
    public class ContentAdaptiveDownscalerTests
    {
        [Test]
        public void Downscale_SolidColor_StaysSolid()
        {
            var pixels = FillSolid(64, 64, Color.red);
            var result = ContentAdaptiveDownscaler.Downscale(pixels, 64, 64, 32, 32, iterations: 4);

            Assert.That(result.Length, Is.EqualTo(32 * 32));
            for (int i = 0; i < result.Length; i++)
            {
                Assert.That(result[i].r, Is.EqualTo(1f).Within(0.05f), $"pixel {i} not red enough");
                Assert.That(result[i].g, Is.EqualTo(0f).Within(0.05f), $"pixel {i} bled green");
                Assert.That(result[i].b, Is.EqualTo(0f).Within(0.05f), $"pixel {i} bled blue");
            }
        }

        [Test]
        public void Downscale_OutputSize_MatchesTarget()
        {
            var pixels = FillSolid(128, 128, new Color(0.3f, 0.5f, 0.7f));
            var result = ContentAdaptiveDownscaler.Downscale(pixels, 128, 128, 32, 32, iterations: 3);
            Assert.That(result.Length, Is.EqualTo(32 * 32));
        }

        [Test]
        public void Downscale_IsDeterministic()
        {
            var pixels = MakeCheckerboard(64, 64, blockSize: 8);
            var first = ContentAdaptiveDownscaler.Downscale(pixels, 64, 64, 32, 32, iterations: 4);
            var second = ContentAdaptiveDownscaler.Downscale(pixels, 64, 64, 32, 32, iterations: 4);

            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(first[i].r, Is.EqualTo(second[i].r).Within(1e-5f));
                Assert.That(first[i].g, Is.EqualTo(second[i].g).Within(1e-5f));
                Assert.That(first[i].b, Is.EqualTo(second[i].b).Within(1e-5f));
            }
        }

        [Test]
        public void Downscale_SharpVerticalEdge_StaysSharp()
        {
            var pixels = MakeHalfHalf(64, 64);
            var result = ContentAdaptiveDownscaler.Downscale(pixels, 64, 64, 32, 32, iterations: 5);

            // sample the middle row and count cells that are neither clearly black nor clearly white
            int midRow = 16;
            int midGreyCells = 0;
            for (int x = 0; x < 32; x++)
            {
                var c = result[midRow * 32 + x];
                if (c.r > 0.2f && c.r < 0.8f) midGreyCells++;
            }
            Assert.That(midGreyCells, Is.LessThanOrEqualTo(1),
                $"sharp edge should produce at most 1 mid-grey transition cell, got {midGreyCells}");
        }

        [Test]
        public void Downscale_SolidSilhouette_Survives4to1()
        {
            var pixels = MakeCenteredSquare(128, 128, squareSize: 8);
            var result = ContentAdaptiveDownscaler.Downscale(pixels, 128, 128, 32, 32, iterations: 5);

            // the 8x8 square maps to roughly a 2x2 cell region centered near (15,15) in the 32x32 output
            int darkInRegion = 0;
            for (int y = 14; y <= 17; y++)
                for (int x = 14; x <= 17; x++)
                {
                    var c = result[y * 32 + x];
                    if (c.r < 0.3f) darkInRegion++;
                }
            Assert.That(darkInRegion, Is.GreaterThanOrEqualTo(1),
                $"an 8x8 silhouette should leave at least 1 dark cell in the 4x4 output region, got {darkInRegion}");
        }

        [Test]
        public void Downscale_AllWhite_StaysWhite()
        {
            var pixels = FillSolid(64, 64, Color.white);
            var result = ContentAdaptiveDownscaler.Downscale(pixels, 64, 64, 16, 16, iterations: 3);
            for (int i = 0; i < result.Length; i++)
                Assert.That(result[i].r, Is.GreaterThan(0.95f), $"pixel {i} darkened");
        }

        private static Color[] FillSolid(int w, int h, Color c)
        {
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
            return pixels;
        }

        private static Color[] MakeCheckerboard(int w, int h, int blockSize)
        {
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool dark = ((x / blockSize) + (y / blockSize)) % 2 == 0;
                    pixels[y * w + x] = dark ? Color.black : Color.white;
                }
            return pixels;
        }

        private static Color[] MakeHalfHalf(int w, int h)
        {
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = (x < w / 2) ? Color.black : Color.white;
            return pixels;
        }

        private static Color[] MakeCenteredSquare(int w, int h, int squareSize)
        {
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            int x0 = (w - squareSize) / 2;
            int y0 = (h - squareSize) / 2;
            for (int y = y0; y < y0 + squareSize; y++)
                for (int x = x0; x < x0 + squareSize; x++)
                    pixels[y * w + x] = Color.black;
            return pixels;
        }
    }
}
