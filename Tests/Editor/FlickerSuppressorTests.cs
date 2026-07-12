using NUnit.Framework;
using UnityEngine;
using SmearFramework.PostProcessing;

namespace SmearFramework.Tests
{
    public class FlickerSuppressorTests
    {
        [Test]
        public void Suppress_IdenticalFrames_NoPixelsChange()
        {
            var a = MakeUniform(8, 8, new Color(0.4f, 0.5f, 0.6f));
            var b = MakeUniform(8, 8, new Color(0.4f, 0.5f, 0.6f));
            var result = FlickerSuppressor.Suppress(b, a, threshold: 5f);
            var ra = a.GetPixels();
            var rr = result.GetPixels();
            for (int i = 0; i < ra.Length; i++)
            {
                Assert.That(rr[i].r, Is.EqualTo(ra[i].r).Within(1e-4f));
                Assert.That(rr[i].g, Is.EqualTo(ra[i].g).Within(1e-4f));
                Assert.That(rr[i].b, Is.EqualTo(ra[i].b).Within(1e-4f));
            }
        }

        [Test]
        public void Suppress_LargeDifference_KeepsCurrent()
        {
            var prev = MakeUniform(8, 8, Color.black);
            var curr = MakeUniform(8, 8, Color.white);
            var result = FlickerSuppressor.Suppress(curr, prev, threshold: 5f);
            var rp = result.GetPixels();
            for (int i = 0; i < rp.Length; i++)
                Assert.That(rp[i].r, Is.GreaterThan(0.9f), "deltaE is far above threshold, should keep current");
        }

        [Test]
        public void Suppress_FirstFrame_NoPrevFrame_ReturnsCurrentUnchanged()
        {
            var curr = MakeUniform(8, 8, Color.red);
            var result = FlickerSuppressor.Suppress(curr, null, threshold: 5f);
            var rp = result.GetPixels();
            for (int i = 0; i < rp.Length; i++)
            {
                Assert.That(rp[i].r, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(rp[i].g, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(rp[i].b, Is.EqualTo(0f).Within(1e-4f));
            }
        }

        private static Texture2D MakeUniform(int w, int h, Color c)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
