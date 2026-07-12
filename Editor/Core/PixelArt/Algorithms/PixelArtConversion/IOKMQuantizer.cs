using UnityEngine;
using System.Collections.Generic;

namespace SmearFramework.PixelArtConversion
{
    // note: abernathy-2022-incremental-online-kmeans.md
    public static class IOKMQuantizer
    {
        public struct Result
        {
            public Color[] Output;
            public Color[] Palette;
        }

        // build a reusable LUT from one representative frame without producing full output pixels
        public static Color[] BuildLUT(Color[] inputPixels, int k)
        {
            if (inputPixels.Length == 0) return new Color[0];
            var lab = ConvertAllToLab(FilterOpaque(inputPixels));
            if (lab.Length == 0) return new Color[0];
            return CentersToRgb(BuildPalette(lab, k));
        }

        // returns only pixels with alpha >= 0.5 so the palette is built from visible colors only
        private static Color[] FilterOpaque(Color[] pixels)
        {
            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].a >= 0.5f) count++;
            var result = new Color[count];
            int j = 0;
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i].a >= 0.5f) result[j++] = pixels[i];
            return result;
        }

        // kick off IOKM quantization; may return fewer than k colors if the image is simple
        public static Result Quantize(Color[] inputPixels, int k)
        {
            if (inputPixels.Length == 0)
                return new Result { Output = new Color[0], Palette = new Color[0] };
            var opaqueLab = ConvertAllToLab(FilterOpaque(inputPixels));
            if (opaqueLab.Length == 0)
                return new Result { Output = new Color[inputPixels.Length], Palette = new Color[0] };
            var lab = ConvertAllToLab(inputPixels);
            var centers = BuildPalette(opaqueLab, k);
            var output = AssignPixelsToPalette(lab, centers, inputPixels);
            return new Result
            {
                Output = output,
                Palette = CentersToRgb(centers)
            };
        }

        // nearest-neighbor snap to a pre-built palette, skips the split pass entirely
        public static Color[] QuantizeWithLUT(Color[] inputPixels, Color[] lut)
        {
            if (lut == null || lut.Length == 0)
                throw new System.ArgumentException("QuantizeWithLUT requires a non-empty LUT", nameof(lut));
            var lutLab = new CielabColor[lut.Length];
            for (int i = 0; i < lut.Length; i++) lutLab[i] = CielabColor.RgbToLab(lut[i]);

            var output = new Color[inputPixels.Length];
            for (int i = 0; i < inputPixels.Length; i++)
            {
                if (inputPixels[i].a < 0.5f) { output[i] = Color.clear; continue; }
                var pLab = CielabColor.RgbToLab(inputPixels[i]);
                int best = FindNearestCenter(pLab, lutLab);
                output[i] = lut[best];
            }
            return output;
        }

        // one-shot RGB to CIELAB, done before any cluster work
        private static CielabColor[] ConvertAllToLab(Color[] input)
        {
            var lab = new CielabColor[input.Length];
            for (int i = 0; i < input.Length; i++) lab[i] = CielabColor.RgbToLab(input[i]);
            return lab;
        }

        // grow the palette by splitting the worst cluster until we hit k
        private static CielabColor[] BuildPalette(CielabColor[] lab, int k)
        {
            var centers = new List<CielabColor> { ComputeMean(lab) };
            while (centers.Count < k)
            {
                int worst = FindHighestDistortionCluster(lab, centers);
                SplitCluster(lab, centers, worst);
            }
            return centers.ToArray();
        }

        // final pass: snap each pixel to its nearest center; transparent input pixels stay transparent
        private static Color[] AssignPixelsToPalette(CielabColor[] lab, CielabColor[] centers, Color[] original)
        {
            var rgbPalette = CentersToRgb(centers);
            var output = new Color[lab.Length];
            for (int i = 0; i < lab.Length; i++)
            {
                if (original[i].a < 0.5f) { output[i] = Color.clear; continue; }
                int best = FindNearestCenter(lab[i], centers);
                output[i] = rgbPalette[best];
            }
            return output;
        }

        // helper -- converts cluster means to RGB for output
        private static Color[] CentersToRgb(CielabColor[] centers)
        {
            var rgb = new Color[centers.Length];
            for (int i = 0; i < centers.Length; i++) rgb[i] = CielabColor.LabToRgb(centers[i]);
            return rgb;
        }

        // root cluster seed -- global mean of the input
        private static CielabColor ComputeMean(CielabColor[] lab)
        {
            float sl = 0f, sa = 0f, sb = 0f;
            for (int i = 0; i < lab.Length; i++)
            {
                sl += lab[i].L; sa += lab[i].A; sb += lab[i].B;
            }
            float inv = 1f / lab.Length;
            return new CielabColor(sl * inv, sa * inv, sb * inv);
        }

        // brute-force nearest in CIELAB -- small k means this stays fast enough
        private static int FindNearestCenter(CielabColor pixel, CielabColor[] centers)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int c = 0; c < centers.Length; c++)
            {
                float d = CielabColor.DeltaE(pixel, centers[c]);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        // find the loosest cluster so we know which one to split next
        private static int FindHighestDistortionCluster(CielabColor[] lab, List<CielabColor> centers)
        {
            var arr = centers.ToArray(); // hoisted -- same for every pixel in this pass
            var totals = new float[centers.Count];
            for (int i = 0; i < lab.Length; i++)
            {
                int c = FindNearestCenter(lab[i], arr);
                totals[c] += CielabColor.DeltaE(lab[i], arr[c]);
            }
            int worst = 0;
            for (int i = 1; i < totals.Length; i++)
                if (totals[i] > totals[worst]) worst = i;
            return worst;
        }

        // MacQueen update -- single pass per pixel keeps this O(N)
        private static void SplitCluster(CielabColor[] lab, List<CielabColor> centers, int idx)
        {
            var arr = centers.ToArray(); // hoisted -- same for every pixel in this pass
            var parent = centers[idx];
            var childA = new CielabColor(parent.L + 0.5f, parent.A, parent.B);
            var childB = new CielabColor(parent.L - 0.5f, parent.A, parent.B);

            int nA = 0, nB = 0;
            for (int i = 0; i < lab.Length; i++)
            {
                int home = FindNearestCenter(lab[i], arr);
                if (home != idx) continue;
                if (CielabColor.DeltaE(lab[i], childA) < CielabColor.DeltaE(lab[i], childB))
                {
                    nA++;
                    float r = 1f / nA;
                    childA = new CielabColor(
                        childA.L + r * (lab[i].L - childA.L),
                        childA.A + r * (lab[i].A - childA.A),
                        childA.B + r * (lab[i].B - childA.B));
                }
                else
                {
                    nB++;
                    float r = 1f / nB;
                    childB = new CielabColor(
                        childB.L + r * (lab[i].L - childB.L),
                        childB.A + r * (lab[i].A - childB.A),
                        childB.B + r * (lab[i].B - childB.B));
                }
            }

            centers[idx] = childA;
            centers.Add(childB);
        }
    }
}
