using UnityEngine;

namespace SmearFramework.PixelArtConversion
{
    // note: gerstner-2012-pixelated-image-abstraction.md (CIELAB as standard color space for pixel art quantization)
    public struct CielabColor
    {
        public float L;
        public float A;
        public float B;

        public CielabColor(float l, float a, float b) { L = l; A = a; B = b; }

        // D65 reference white, normalized so Y = 100 maps to L = 100
        private const float XN = 95.047f;
        private const float YN = 100.000f;
        private const float ZN = 108.883f;

        // sRGB in, CIELAB out
        public static CielabColor RgbToLab(Color c)
        {
            float r = SrgbToLinear(c.r);
            float g = SrgbToLinear(c.g);
            float b = SrgbToLinear(c.b);

            // sRGB to XYZ (D65), standard matrix
            float x = (r * 0.4124564f + g * 0.3575761f + b * 0.1804375f) * 100f;
            float y = (r * 0.2126729f + g * 0.7151522f + b * 0.0721750f) * 100f;
            float z = (r * 0.0193339f + g * 0.1191920f + b * 0.9503041f) * 100f;

            float fx = LabF(x / XN);
            float fy = LabF(y / YN);
            float fz = LabF(z / ZN);

            return new CielabColor(
                116f * fy - 16f,
                500f * (fx - fy),
                200f * (fy - fz));
        }

        // CIELAB in, sRGB out
        public static Color LabToRgb(CielabColor lab)
        {
            float fy = (lab.L + 16f) / 116f;
            float fx = lab.A / 500f + fy;
            float fz = fy - lab.B / 200f;

            float x = XN * LabFInv(fx) / 100f;
            float y = YN * LabFInv(fy) / 100f;
            float z = ZN * LabFInv(fz) / 100f;

            // XYZ to linear sRGB, inverse of the matrix above
            float r = x * 3.2404542f + y * -1.5371385f + z * -0.4985314f;
            float g = x * -0.9692660f + y * 1.8760108f + z * 0.0415560f;
            float b = x * 0.0556434f + y * -0.2040259f + z * 1.0572252f;

            return new Color(
                Mathf.Clamp01(LinearToSrgb(r)),
                Mathf.Clamp01(LinearToSrgb(g)),
                Mathf.Clamp01(LinearToSrgb(b)));
        }

        // CIE76 perceptual distance between two Lab colors
        public static float DeltaE(CielabColor a, CielabColor b)
        {
            float dl = a.L - b.L;
            float da = a.A - b.A;
            float db = a.B - b.B;
            return Mathf.Sqrt(dl * dl + da * da + db * db);
        }

        // gamma decode
        private static float SrgbToLinear(float c)
        {
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        // gamma encode
        private static float LinearToSrgb(float c)
        {
            if (c <= 0f) return 0f;
            return c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;
        }

        // CIE f auxiliary function
        private static float LabF(float t)
        {
            const float delta = 6f / 29f;
            return t > delta * delta * delta
                ? Mathf.Pow(t, 1f / 3f)
                : t / (3f * delta * delta) + 4f / 29f;
        }

        // Inverse of LabF
        private static float LabFInv(float t)
        {
            const float delta = 6f / 29f;
            return t > delta
                ? t * t * t
                : 3f * delta * delta * (t - 4f / 29f);
        }
    }
}
