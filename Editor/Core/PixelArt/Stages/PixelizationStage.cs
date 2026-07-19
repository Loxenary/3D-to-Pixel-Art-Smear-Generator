using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.PixelArtConversion;
using SmearFramework.PostProcessing;

namespace SmearFramework.Stages
{
    // notes: kopf-2013-content-adaptive-downscaling.md, abernathy-2022-incremental-online-kmeans.md, kuo-2016-feature-aware-pixel-animation.md
    [InternalStage]
    public class PixelizationStage : IPipelineStage
    {
        public string Name => "Pixelization";

        public IReadOnlyList<ArtifactKey> InputKey => new[]
        {
            ArtifactKey.Of<RawFrameData>("frames_highres"),
        };
        public IReadOnlyList<ArtifactKey> OutputKey => new[]
        {
            ArtifactKey.Of<RawFrameData>("frames_pixelized"),
        };

        public void Execute(PipelineContext ctx)
        {
            var raw = ctx.Get<RawFrameData>("frames_highres");
            var cfg = ctx.Config;
            int outW = cfg.PixelWidth;
            int outH = cfg.PixelHeight;
            int count = raw.FrameCount;
            bool hasMasks = raw.SmearMasks != null;
            bool overlayEnabled = cfg.SmearOverlayColor.a > 0.01f;

            // -- stage 1: read all texture data on the main thread (Texture2D is not thread-safe)
            var inputPixels = new Color[count][];
            var maskPixels = new Color[count][];
            for (int f = 0; f < count; f++)
            {
                inputPixels[f] = raw.Frames[f].GetPixels();
                if (hasMasks && raw.SmearMasks[f] != null)
                    maskPixels[f] = raw.SmearMasks[f].GetPixels();
            }

            // -- stage 2: build palette (main thread; IOKM is deterministic but not thread-safe due to List)
            Color[] sharedLUT = null;
            if (cfg.PaletteLUT != null && cfg.PaletteLUT.Length > 0)
            {
                sharedLUT = cfg.PaletteLUT; // user-fixed palette overrides everything
            }
            else if (cfg.ReusePaletteAcrossFrames && count > 1)
            {
                // build once from frame 0 (or a midpoint frame for slightly better coverage)
                int seedFrame = Mathf.Clamp(count / 3, 0, count - 1);
                var seedDownscaled = ContentAdaptiveDownscaler.Downscale(
                    inputPixels[seedFrame],
                    raw.Width, raw.Height,
                    outW, outH,
                    cfg.EmIterations);
                sharedLUT = IOKMQuantizer.BuildLUT(seedDownscaled, cfg.PostProcessPaletteSize);
                if (sharedLUT.Length == 0) sharedLUT = null; // seed frame was fully transparent -- fall back to per-frame quantize
            }

            // stage 3: per-frame downscale + quantize in parallel (pure Color[] math, no Unity API)
            var quantizedColors = new Color[count][];
            var smearMasks = new bool[count][];

            Parallel.For(0, count, f =>
            {
                var downscaled = ContentAdaptiveDownscaler.Downscale(
                    inputPixels[f],
                    raw.Width, raw.Height,
                    outW, outH,
                    cfg.EmIterations);

                Color[] quantized;
                if (sharedLUT != null)
                    quantized = IOKMQuantizer.QuantizeWithLUT(downscaled, sharedLUT);
                else
                    quantized = IOKMQuantizer.Quantize(downscaled, cfg.PostProcessPaletteSize).Output;

                // apply smear overlay: pixels covered by the mask get the reserved color
                bool[] mask = null;
                if (hasMasks && maskPixels[f] != null)
                {
                    mask = DownscaleMaskMaxPool(maskPixels[f], raw.Width, raw.Height, outW, outH);
                    if (overlayEnabled)
                    {
                        Color overlayCol = cfg.SmearOverlayColor;
                        for (int i = 0; i < quantized.Length; i++)
                            if (mask[i]) quantized[i] = overlayCol;
                    }
                }

                quantizedColors[f] = quantized;
                smearMasks[f] = mask;
            });

            // -- stage 4: flicker suppression -- sequential because each frame depends on the prev
            var output = new RawFrameData(count, outW, outH);
            Texture2D prev = null;
            for (int f = 0; f < count; f++)
            {
                var quantizedTex = BuildTexture(outW, outH, quantizedColors[f]);
                var stabilized = FlickerSuppressor.Suppress(quantizedTex, prev, cfg.FlickerThreshold, smearMasks[f]);
                Object.DestroyImmediate(quantizedTex);
                output.Frames[f] = stabilized;
                output.SmearMasks[f] = smearMasks[f] != null ? BuildMaskTexture(outW, outH, smearMasks[f]) : null;
                prev = stabilized;
            }

            ctx.Set("frames_pixelized", output);
        }

        // max-pool a high-res mask down to output resolution -- any non-transparent input pixel covers the output pixel
        private static bool[] DownscaleMaskMaxPool(Color[] maskPx, int inW, int inH, int outW, int outH)
        {
            var result = new bool[outW * outH];
            float scaleX = (float)inW / outW;
            float scaleY = (float)inH / outH;
            for (int oy = 0; oy < outH; oy++)
            {
                int y0 = Mathf.FloorToInt(oy * scaleY);
                int y1 = Mathf.Min(Mathf.CeilToInt((oy + 1) * scaleY), inH);
                for (int ox = 0; ox < outW; ox++)
                {
                    int x0 = Mathf.FloorToInt(ox * scaleX);
                    int x1 = Mathf.Min(Mathf.CeilToInt((ox + 1) * scaleX), inW);
                    bool covered = false;
                    for (int sy = y0; sy < y1 && !covered; sy++)
                        for (int sx = x0; sx < x1 && !covered; sx++)
                            if (maskPx[sy * inW + sx].a > 0.05f) covered = true;
                    result[oy * outW + ox] = covered;
                }
            }
            return result;
        }

        // pack pixels into a point-filtered texture
        private static Texture2D BuildTexture(int w, int h, Color[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // Convert a downscaled smear coverage map back into a texture artifact for downstream checks.
        private static Texture2D BuildMaskTexture(int w, int h, bool[] mask)
        {
            var pixels = new Color[w * h];
            for (int i = 0; i < mask.Length; i++)
                pixels[i] = mask[i] ? new Color(1f, 1f, 1f, 1f) : Color.clear;
            return BuildTexture(w, h, pixels);
        }
    }
}
