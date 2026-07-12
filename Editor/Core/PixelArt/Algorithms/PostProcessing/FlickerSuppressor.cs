using UnityEngine;
using SmearFramework.PixelArtConversion;

namespace SmearFramework.PostProcessing
{
    // note: kuo-2016-feature-aware-pixel-animation.md (temporal coherence motivation)
    public static class FlickerSuppressor
    {
        // returns a copy of current with near-unchanged pixels snapped to previous.
        // protectedPixels: when true at index i, the pixel is a smear overlay and bypasses suppression.
        public static Texture2D Suppress(Texture2D current, Texture2D previous, float threshold, bool[] protectedPixels = null)
        {
            if (previous == null) return CopyTexture(current);

            var curPixels = current.GetPixels();
            var prevPixels = previous.GetPixels();
            var output = new Color[curPixels.Length];

            for (int i = 0; i < curPixels.Length; i++)
            {
                if (protectedPixels != null && protectedPixels[i])
                    output[i] = curPixels[i]; // smear overlay -- keep it; don't snap back to a non-overlay prev frame
                else
                    output[i] = ChoosePixel(curPixels[i], prevPixels[i], threshold);
            }

            return BuildResult(current.width, current.height, output);
        }

        // under threshold means "probably jitter, not real motion" -- keep the stable value
        private static Color ChoosePixel(Color cur, Color prev, float threshold)
        {
            bool curOpaque = cur.a >= 0.5f;
            bool prevOpaque = prev.a >= 0.5f;
            // don't compare across the opacity boundary -- suppression only makes sense between two opaque pixels
            if (curOpaque != prevOpaque) return cur;
            if (!curOpaque) return cur; // both transparent -- keep current
            var lc = CielabColor.RgbToLab(cur);
            var lp = CielabColor.RgbToLab(prev);
            return CielabColor.DeltaE(lc, lp) < threshold ? prev : cur;
        }

        // no previous frame -- just return a point-filtered copy
        private static Texture2D CopyTexture(Texture2D source)
        {
            var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels(source.GetPixels());
            tex.Apply();
            return tex;
        }

        // pack the processed pixels into a texture
        private static Texture2D BuildResult(int w, int h, Color[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
