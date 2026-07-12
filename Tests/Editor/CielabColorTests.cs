using NUnit.Framework;
using UnityEngine;
using SmearFramework.PixelArtConversion;

namespace SmearFramework.Tests
{
    public class CielabColorTests
    {
        [Test]
        public void RgbToLab_Black_GivesL0()
        {
            var lab = CielabColor.RgbToLab(Color.black);
            Assert.That(lab.L, Is.EqualTo(0f).Within(0.5f));
        }

        [Test]
        public void RgbToLab_White_GivesL100()
        {
            var lab = CielabColor.RgbToLab(Color.white);
            Assert.That(lab.L, Is.EqualTo(100f).Within(0.5f));
        }

        [Test]
        public void RoundTrip_Red_StaysRed()
        {
            var original = Color.red;
            var lab = CielabColor.RgbToLab(original);
            var back = CielabColor.LabToRgb(lab);
            Assert.That(back.r, Is.EqualTo(original.r).Within(0.01f));
            Assert.That(back.g, Is.EqualTo(original.g).Within(0.01f));
            Assert.That(back.b, Is.EqualTo(original.b).Within(0.01f));
        }

        [Test]
        public void DeltaE_SameColor_IsZero()
        {
            var lab = CielabColor.RgbToLab(new Color(0.4f, 0.6f, 0.8f));
            Assert.That(CielabColor.DeltaE(lab, lab), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void DeltaE_BlackWhite_IsAbout100()
        {
            var black = CielabColor.RgbToLab(Color.black);
            var white = CielabColor.RgbToLab(Color.white);
            Assert.That(CielabColor.DeltaE(black, white), Is.EqualTo(100f).Within(1f));
        }
    }
}
