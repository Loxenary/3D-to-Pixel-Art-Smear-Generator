using UnityEngine;

namespace SmearFramework.PixelArtConversion
{
    // note: kopf-2013-content-adaptive-downscaling.md
    public static class ContentAdaptiveDownscaler
    {
        #region Public API

        // main entry -- runs EM iterations and returns row-major sRGB output
        public static Color[] Downscale(Color[] inputPixels, int inputW, int inputH,
            int outW, int outH, int iterations)
        {
            ValidateInput(inputPixels, inputW, inputH, outW, outH);

            var inputAlpha = ExtractAlpha(inputPixels);
            var inputLab = ConvertInputToLab(inputPixels);
            var kernels = InitializeKernels(inputW, inputH, outW, outH);

            for (int it = 0; it < iterations; it++)
                RunEMIteration(kernels, inputLab, inputAlpha, inputW, inputH, outW, outH);

            // alpha: max-pool from input so thin features (1-2px antlers, fingers) survive the downscale
            var opaqueMap = MaxPoolAlpha(inputAlpha, inputW, inputH, outW, outH);
            return KernelsToOutput(kernels, opaqueMap);
        }

        #endregion

        #region Data

        // Per-output-pixel kernel state, all in CIELAB color space and pixel coordinates
        private struct Kernel
        {
            public float MuX, MuY;          // spatial mean
            public float Sxx, Sxy, Syy;     // spatial covariance entries (symmetric)
            public float VL, VA, VB;        // color mean in CIELAB
            public float ColorVar;          // sigma_k^2, scalar color variance
            public int LatticeX, LatticeY;  // output-grid position this kernel was born at
            public float Rx, Ry;            // input-to-output ratio used for clamping
        }

        // wide initial sigma_k so iteration 1 can see color contrast; paper uses 1e-4 in [0,1] RGB, CIELAB L is 0..100 so 30 is the rough equivalent
        private const float INITIAL_COLOR_SIGMA = 30f;

        #endregion

        #region Initialization

        // one kernel per output pixel, placed at the lattice cell center (eq. 6)
        private static Kernel[] InitializeKernels(int inW, int inH, int outW, int outH)
        {
            var kernels = new Kernel[outW * outH];
            float rx = (float)inW / outW;
            float ry = (float)inH / outH;

            for (int y = 0; y < outH; y++)
                for (int x = 0; x < outW; x++)
                    kernels[y * outW + x] = MakeInitialKernel(x, y, rx, ry);

            return kernels;
        }

        // helper -- isotropic init, variance set to (r/3)^2 so the support covers roughly one input cell
        private static Kernel MakeInitialKernel(int x, int y, float rx, float ry)
        {
            float muX = (x + 0.5f) * rx;
            float muY = (y + 0.5f) * ry;
            float sx = rx / 3f;
            float sy = ry / 3f;
            return new Kernel
            {
                MuX = muX,
                MuY = muY,
                Sxx = sx * sx,
                Syy = sy * sy,
                Sxy = 0f,
                VL = 50f, VA = 0f, VB = 0f, // mid-luminance neutral in CIELAB
                ColorVar = INITIAL_COLOR_SIGMA * INITIAL_COLOR_SIGMA,
                LatticeX = x,
                LatticeY = y,
                Rx = rx,
                Ry = ry
            };
        }

        #endregion

        #region EM pass

        // single EM pass -- transparent pixels are skipped so background doesn't bias character colors
        private static void RunEMIteration(Kernel[] kernels, CielabColor[] inputLab, float[] inputAlpha,
            int inW, int inH, int outW, int outH)
        {
            int kn = kernels.Length;
            var sumW = new float[kn];
            var sumWX = new float[kn];
            var sumWY = new float[kn];
            var sumWL = new float[kn];
            var sumWA = new float[kn];
            var sumWB = new float[kn];

            AccumulateResponsibilities(kernels, inputLab, inputAlpha, inW, inH, outW, outH,
                sumW, sumWX, sumWY, sumWL, sumWA, sumWB);

            ApplyMeanUpdate(kernels, sumW, sumWX, sumWY, sumWL, sumWA, sumWB);
            ApplySpatialClamp(kernels);
            UpdateSpatialCovariance(kernels, inputLab, inputAlpha, inW, inH, outW, outH, sumW);
            ApplyLocalityConstraint(kernels, inputLab, inputAlpha, inW, inH, outW, outH, sumW);
        }

        // outer loop -- delegates each row to avoid a 3-level loop here
        private static void AccumulateResponsibilities(Kernel[] kernels, CielabColor[] inputLab, float[] inputAlpha,
            int inW, int inH, int outW, int outH,
            float[] sumW, float[] sumWX, float[] sumWY,
            float[] sumWL, float[] sumWA, float[] sumWB)
        {
            float rx = (float)inW / outW;
            float ry = (float)inH / outH;

            for (int py = 0; py < inH; py++)
                AccumulateRow(kernels, inputLab, inputAlpha, py, inW, inH, outW, outH, rx, ry,
                    sumW, sumWX, sumWY, sumWL, sumWA, sumWB);
        }

        // per-row accumulation into the 3x3 kernel neighborhood
        private static void AccumulateRow(Kernel[] kernels, CielabColor[] inputLab, float[] inputAlpha, int py,
            int inW, int inH, int outW, int outH, float rx, float ry,
            float[] sumW, float[] sumWX, float[] sumWY,
            float[] sumWL, float[] sumWA, float[] sumWB)
        {
            int kyc = Mathf.Clamp(Mathf.FloorToInt(py / ry), 0, outH - 1);
            int kyStart = Mathf.Max(0, kyc - 1);
            int kyEnd = Mathf.Min(outH - 1, kyc + 1);

            for (int px = 0; px < inW; px++)
            {
                // skip transparent background pixels -- they carry no color information
                if (inputAlpha[py * inW + px] < 0.5f) continue;
                int kxc = Mathf.Clamp(Mathf.FloorToInt(px / rx), 0, outW - 1);
                UpdateNeighborhood(kernels, inputLab, px, py, inW, kxc, kyStart, kyEnd, outW,
                    sumW, sumWX, sumWY, sumWL, sumWA, sumWB);
            }
        }

        // push one input pixel into the kernels that overlap it (at most a 3x3 window)
        private static void UpdateNeighborhood(Kernel[] kernels, CielabColor[] inputLab,
            int px, int py, int inW, int kxc, int kyStart, int kyEnd, int outW,
            float[] sumW, float[] sumWX, float[] sumWY,
            float[] sumWL, float[] sumWA, float[] sumWB)
        {
            var c = inputLab[py * inW + px];
            for (int ky = kyStart; ky <= kyEnd; ky++)
                AccumulateIntoKernelStripe(kernels, c, px, py, kxc, ky, outW,
                    sumW, sumWX, sumWY, sumWL, sumWA, sumWB);
        }

        // inner loop for one kernel row -- 3 columns max
        private static void AccumulateIntoKernelStripe(Kernel[] kernels, CielabColor c,
            int px, int py, int kxc, int ky, int outW,
            float[] sumW, float[] sumWX, float[] sumWY,
            float[] sumWL, float[] sumWA, float[] sumWB)
        {
            int kxStart = Mathf.Max(0, kxc - 1);
            int kxEnd = Mathf.Min(outW - 1, kxc + 1);
            for (int kx = kxStart; kx <= kxEnd; kx++)
            {
                int idx = ky * outW + kx;
                float w = ComputeKernelWeight(kernels[idx], px, py, c);
                if (w <= 0f) continue;
                sumW[idx] += w;
                sumWX[idx] += w * px;
                sumWY[idx] += w * py;
                sumWL[idx] += w * c.L;
                sumWA[idx] += w * c.A;
                sumWB[idx] += w * c.B;
            }
        }

        // bilateral weight: spatial Gaussian (elliptical) * color Gaussian (isotropic in CIELAB)
        private static float ComputeKernelWeight(Kernel k, int px, int py, CielabColor c)
        {
            float dx = px - k.MuX;
            float dy = py - k.MuY;

            // invert the 2x2 covariance, det must stay positive
            float det = k.Sxx * k.Syy - k.Sxy * k.Sxy;
            if (det <= 1e-10f) return 0f;
            float invDet = 1f / det;
            float a = k.Syy * invDet;
            float b = -k.Sxy * invDet;
            float d = k.Sxx * invDet;

            float spatialExp = -0.5f * (dx * (a * dx + b * dy) + dy * (b * dx + d * dy));

            float dl = c.L - k.VL;
            float da = c.A - k.VA;
            float db = c.B - k.VB;
            float colorSq = dl * dl + da * da + db * db;
            float colorExp = -colorSq / (2f * k.ColorVar);

            return Mathf.Exp(spatialExp + colorExp);
        }

        // M-step: update spatial and color means from accumulated weights (eqs. 8 and 10)
        private static void ApplyMeanUpdate(Kernel[] kernels,
            float[] sumW, float[] sumWX, float[] sumWY,
            float[] sumWL, float[] sumWA, float[] sumWB)
        {
            for (int i = 0; i < kernels.Length; i++)
            {
                float w = sumW[i];
                if (w < 1e-10f) continue; // dead kernel, skip
                float inv = 1f / w;
                var k = kernels[i];
                k.MuX = sumWX[i] * inv;
                k.MuY = sumWY[i] * inv;
                k.VL = sumWL[i] * inv;
                k.VA = sumWA[i] * inv;
                k.VB = sumWB[i] * inv;
                kernels[i] = k;
            }
        }

        #endregion

        #region Correction steps

        // keep mu_k within r/4 of the lattice center (Section 4.1) so kernels don't wander
        private static void ApplySpatialClamp(Kernel[] kernels)
        {
            for (int i = 0; i < kernels.Length; i++)
            {
                var k = kernels[i];
                float pBarX = (k.LatticeX + 0.5f) * k.Rx;
                float pBarY = (k.LatticeY + 0.5f) * k.Ry;
                k.MuX = Mathf.Clamp(k.MuX, pBarX - k.Rx * 0.25f, pBarX + k.Rx * 0.25f);
                k.MuY = Mathf.Clamp(k.MuY, pBarY - k.Ry * 0.25f, pBarY + k.Ry * 0.25f);
                kernels[i] = k;
            }
        }

        // recompute Sigma_k from weighted second moments (eq. 9); reuses the same denom from M-step
        private static void UpdateSpatialCovariance(Kernel[] kernels, CielabColor[] inputLab, float[] inputAlpha,
            int inW, int inH, int outW, int outH, float[] denom)
        {
            var sxx = new float[kernels.Length];
            var syy = new float[kernels.Length];
            var sxy = new float[kernels.Length];

            AccumulateCovariance(kernels, inputLab, inputAlpha, inW, inH, outW, outH, sxx, syy, sxy);
            FinalizeCovariance(kernels, sxx, syy, sxy, denom);
        }

        // outer loop for covariance accumulation, rows only
        private static void AccumulateCovariance(Kernel[] kernels, CielabColor[] inputLab, float[] inputAlpha,
            int inW, int inH, int outW, int outH,
            float[] sxx, float[] syy, float[] sxy)
        {
            float ry = (float)inH / outH;
            for (int py = 0; py < inH; py++)
                AccumulateCovarianceRow(kernels, inputLab, inputAlpha, py, inW, outW, outH, ry, sxx, syy, sxy);
        }

        // per-row covariance pass -- mirrors AccumulateRow structure
        private static void AccumulateCovarianceRow(Kernel[] kernels, CielabColor[] inputLab, float[] inputAlpha,
            int py, int inW, int outW, int outH, float ry,
            float[] sxx, float[] syy, float[] sxy)
        {
            float rx = (float)inW / outW;
            int kyc = Mathf.Clamp(Mathf.FloorToInt(py / ry), 0, outH - 1);
            int kyStart = Mathf.Max(0, kyc - 1);
            int kyEnd = Mathf.Min(outH - 1, kyc + 1);

            for (int px = 0; px < inW; px++)
            {
                if (inputAlpha[py * inW + px] < 0.5f) continue;
                AccumulateCovarianceAt(kernels, inputLab, px, py, inW, outW,
                    kyStart, kyEnd, rx, sxx, syy, sxy);
            }
        }

        // adds (p - mu)(p - mu)^T into the 3x3 neighborhood for this pixel
        private static void AccumulateCovarianceAt(Kernel[] kernels, CielabColor[] inputLab,
            int px, int py, int inW, int outW, int kyStart, int kyEnd, float rx,
            float[] sxx, float[] syy, float[] sxy)
        {
            int kxc = Mathf.Clamp(Mathf.FloorToInt(px / rx), 0, outW - 1);
            int kxStart = Mathf.Max(0, kxc - 1);
            int kxEnd = Mathf.Min(outW - 1, kxc + 1);
            var c = inputLab[py * inW + px];

            for (int ky = kyStart; ky <= kyEnd; ky++)
                for (int kx = kxStart; kx <= kxEnd; kx++)
                    AddCovarianceTerm(kernels[ky * outW + kx], c, px, py, ky * outW + kx,
                        sxx, syy, sxy);
        }

        // atomic accumulate for one (pixel, kernel) pair
        private static void AddCovarianceTerm(Kernel k, CielabColor c, int px, int py, int idx,
            float[] sxx, float[] syy, float[] sxy)
        {
            float w = ComputeKernelWeight(k, px, py, c);
            if (w <= 0f) return;
            float dx = px - k.MuX;
            float dy = py - k.MuY;
            sxx[idx] += w * dx * dx;
            syy[idx] += w * dy * dy;
            sxy[idx] += w * dx * dy;
        }

        // normalize by denom to get the final Sigma_k
        private static void FinalizeCovariance(Kernel[] kernels,
            float[] sxx, float[] syy, float[] sxy, float[] denom)
        {
            for (int i = 0; i < kernels.Length; i++)
            {
                if (denom[i] < 1e-10f) continue;
                float inv = 1f / denom[i];
                var k = kernels[i];
                k.Sxx = Mathf.Max(sxx[i] * inv, 1e-4f);
                k.Syy = Mathf.Max(syy[i] * inv, 1e-4f);
                k.Sxy = sxy[i] * inv;
                kernels[i] = k;
            }
        }

        // locality constraint (Section 4.2): drive sigma_k from actual color spread, clamped so kernels don't vanish or blow up
        private static void ApplyLocalityConstraint(Kernel[] kernels, CielabColor[] inputLab, float[] inputAlpha,
            int inW, int inH, int outW, int outH, float[] denom)
        {
            var sumColorSq = new float[kernels.Length];
            AccumulateColorVariance(kernels, inputLab, inputAlpha, inW, inH, outW, outH, sumColorSq);
            FinalizeColorVariance(kernels, sumColorSq, denom);
        }

        // accumulate color variance -- outer row loop
        private static void AccumulateColorVariance(Kernel[] kernels, CielabColor[] inputLab, float[] inputAlpha,
            int inW, int inH, int outW, int outH, float[] sumColorSq)
        {
            float ry = (float)inH / outH;
            for (int py = 0; py < inH; py++)
                AccumulateColorVarianceRow(kernels, inputLab, inputAlpha, py, inW, outW, outH, ry, sumColorSq);
        }

        // per-row accumulation of weighted color distances
        private static void AccumulateColorVarianceRow(Kernel[] kernels, CielabColor[] inputLab, float[] inputAlpha,
            int py, int inW, int outW, int outH, float ry, float[] sumColorSq)
        {
            float rx = (float)inW / outW;
            int kyc = Mathf.Clamp(Mathf.FloorToInt(py / ry), 0, outH - 1);
            int kyStart = Mathf.Max(0, kyc - 1);
            int kyEnd = Mathf.Min(outH - 1, kyc + 1);

            for (int px = 0; px < inW; px++)
            {
                if (inputAlpha[py * inW + px] < 0.5f) continue;
                AddColorVarianceAt(kernels, inputLab, px, py, inW, outW,
                    kyStart, kyEnd, rx, sumColorSq);
            }
        }

        // weighted ||c - v_k||^2 for one input pixel into its 3x3 block
        private static void AddColorVarianceAt(Kernel[] kernels, CielabColor[] inputLab,
            int px, int py, int inW, int outW, int kyStart, int kyEnd, float rx,
            float[] sumColorSq)
        {
            int kxc = Mathf.Clamp(Mathf.FloorToInt(px / rx), 0, outW - 1);
            int kxStart = Mathf.Max(0, kxc - 1);
            int kxEnd = Mathf.Min(outW - 1, kxc + 1);
            var c = inputLab[py * inW + px];

            for (int ky = kyStart; ky <= kyEnd; ky++)
                for (int kx = kxStart; kx <= kxEnd; kx++)
                {
                    int idx = ky * outW + kx;
                    var k = kernels[idx];
                    float w = ComputeKernelWeight(k, px, py, c);
                    if (w <= 0f) continue;
                    float dl = c.L - k.VL;
                    float da = c.A - k.VA;
                    float db = c.B - k.VB;
                    sumColorSq[idx] += w * (dl * dl + da * da + db * db);
                }
        }

        // normalize variance sums and clamp sigma_k to the locality range
        private static void FinalizeColorVariance(Kernel[] kernels,
            float[] sumColorSq, float[] denom)
        {
            for (int i = 0; i < kernels.Length; i++)
            {
                if (denom[i] < 1e-10f) continue;
                float varRaw = sumColorSq[i] / denom[i];
                float sigmaRaw = Mathf.Sqrt(Mathf.Max(varRaw, 0f));
                float sigma = Mathf.Clamp(sigmaRaw, 5f, 10f); // paper Section 4.2 bounds, scaled to CIELAB L
                var k = kernels[i];
                k.ColorVar = sigma * sigma;
                kernels[i] = k;
            }
        }

        #endregion

        #region Input and output

        // throw early on clearly wrong input so errors surface close to the call site
        private static void ValidateInput(Color[] pixels, int inW, int inH, int outW, int outH)
        {
            if (pixels == null || pixels.Length != inW * inH)
                throw new System.ArgumentException(
                    $"input pixel count {pixels?.Length ?? 0} does not match {inW}x{inH}");
            if (outW <= 0 || outH <= 0)
                throw new System.ArgumentException("output dimensions must be positive");
            if (outW > inW || outH > inH)
                throw new System.ArgumentException("downscaler does not upscale, use a different stage");
        }

        // one-time alpha extraction -- keeps the alpha channel separate from the CIELAB color pipeline
        private static float[] ExtractAlpha(Color[] pixels)
        {
            var alpha = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) alpha[i] = pixels[i].a;
            return alpha;
        }

        // one-time conversion so the EM loop doesn't do it every iteration
        private static CielabColor[] ConvertInputToLab(Color[] pixels)
        {
            var lab = new CielabColor[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) lab[i] = CielabColor.RgbToLab(pixels[i]);
            return lab;
        }

        // pull the converged color mean; opaqueMap drives alpha so thin features survive downscale
        private static Color[] KernelsToOutput(Kernel[] kernels, bool[] opaqueMap)
        {
            var output = new Color[kernels.Length];
            for (int i = 0; i < kernels.Length; i++)
            {
                if (!opaqueMap[i]) { output[i] = Color.clear; continue; }
                var lab = new CielabColor(kernels[i].VL, kernels[i].VA, kernels[i].VB);
                var c = CielabColor.LabToRgb(lab);
                c.a = 1f;
                output[i] = c;
            }
            return output;
        }

        // max-pool: output pixel is opaque if ANY input pixel in its grid cell has alpha >= 0.5
        private static bool[] MaxPoolAlpha(float[] inputAlpha, int inW, int inH, int outW, int outH)
        {
            var result = new bool[outW * outH];
            float rx = (float)inW / outW;
            float ry = (float)inH / outH;

            for (int ky = 0; ky < outH; ky++)
            {
                int y0 = Mathf.FloorToInt(ky * ry);
                int y1 = Mathf.Min(Mathf.CeilToInt((ky + 1) * ry), inH);
                for (int kx = 0; kx < outW; kx++)
                {
                    int x0 = Mathf.FloorToInt(kx * rx);
                    int x1 = Mathf.Min(Mathf.CeilToInt((kx + 1) * rx), inW);
                    int idx = ky * outW + kx;
                    for (int py = y0; py < y1 && !result[idx]; py++)
                        for (int px = x0; px < x1 && !result[idx]; px++)
                            if (inputAlpha[py * inW + px] >= 0.5f)
                                result[idx] = true;
                }
            }
            return result;
        }

        #endregion
    }
}
