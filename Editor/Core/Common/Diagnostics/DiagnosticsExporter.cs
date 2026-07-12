using UnityEngine;
using System.IO;
using System.Text;
using SmearFramework.DataTypes;

namespace SmearFramework
{
    // Dumps all collected diagnostics data to disk (PNGs + CSV/JSON).
    public static class DiagnosticsExporter
    {
        // Write all collected diagnostics to subfolders organized by pipeline stage
        public static void Flush(DiagnosticsData data)
        {
            if (string.IsNullOrEmpty(data.OutputFolder))
                return;

            Directory.CreateDirectory(data.OutputFolder);

            FlushStage1(data);
            FlushStage2(data);
            FlushStage3(data);
            FlushStage4(data);
        }

        // Export velocity heatmaps and per-frame max velocity CSV
        private static void FlushStage1(DiagnosticsData data)
        {
            var dir = Path.Combine(data.OutputFolder, "stage1_velocity");
            Directory.CreateDirectory(dir);

            SaveFrames(data.MotionOffsetHeatmaps, dir, "motion_offset_f");

            if (data.MaxVelocityPerFrame != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("frame,max_velocity");
                for (int i = 0; i < data.MaxVelocityPerFrame.Length; i++)
                    sb.AppendLine(i + "," + data.MaxVelocityPerFrame[i]);
                File.WriteAllText(Path.Combine(dir, "velocity_graph.csv"), sb.ToString());
            }
        }

        // Export base-vs-smeared comparison frames and smear activation JSON
        private static void FlushStage2(DiagnosticsData data)
        {
            var dir = Path.Combine(data.OutputFolder, "stage2_smear");
            Directory.CreateDirectory(dir);

            SaveFrames(data.BaseVsSmeared, dir, "base_vs_smeared_f");

            if (data.SmearTypePerFrame != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("[");
                for (int i = 0; i < data.SmearTypePerFrame.Length; i++)
                {
                    sb.Append("  { \"frame\": " + i);
                    sb.Append(", \"type\": \"" + data.SmearTypePerFrame[i] + "\"");
                    sb.Append(", \"intensity\": " + data.SmearIntensityPerFrame[i] + " }");
                    if (i < data.SmearTypePerFrame.Length - 1)
                        sb.Append(",");
                    sb.AppendLine();
                }
                sb.AppendLine("]");
                File.WriteAllText(Path.Combine(dir, "smear_activation.json"), sb.ToString());
            }
        }

        // Export high-res captures, outlined frames, and downscaled frames
        private static void FlushStage3(DiagnosticsData data)
        {
            var dir = Path.Combine(data.OutputFolder, "stage3_conversion");
            Directory.CreateDirectory(dir);

            SaveFrames(data.HighResCaptures, dir, "highres_f");
            SaveFrames(data.AfterOutline, dir, "outlined_f");
            SaveFrames(data.AfterDownscale, dir, "downscaled_f");
        }

        // Export quantization before/after frames, flicker maps, and palette swatch
        private static void FlushStage4(DiagnosticsData data)
        {
            var dir = Path.Combine(data.OutputFolder, "stage4_postprocess");
            Directory.CreateDirectory(dir);

            SaveFrames(data.PreQuantize, dir, "pre_quantize_f");
            SaveFrames(data.PostQuantize, dir, "post_quantize_f");
            SaveFrames(data.FlickerMaps, dir, "flicker_map_f");

            if (data.PaletteSwatch != null)
                SavePng(data.PaletteSwatch, Path.Combine(dir, "palette.png"));
        }

        // Save an array of textures as numbered PNGs in the given directory
        private static void SaveFrames(Texture2D[] frames, string dir, string prefix)
        {
            if (frames == null) return;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null) continue;
                SavePng(frames[i], Path.Combine(dir, prefix + i.ToString("D3") + ".png"));
            }
        }

        // Encode a single texture to PNG and write to disk
        private static void SavePng(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }
    }
}
