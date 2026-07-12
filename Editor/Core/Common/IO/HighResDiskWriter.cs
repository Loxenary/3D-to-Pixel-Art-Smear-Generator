using System;
using System.IO;
using System.Text;
using UnityEngine;
using SmearFramework.DataTypes;

namespace SmearFramework.PixelArtConversion
{
    public static class HighResDiskWriter
    {
        // write sheet + json to folder; returns both paths for caller convenience
        public static (string pngPath, string jsonPath) Save(
            string folder, string baseName, RawFrameData raw, HighResMetadata meta)
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            int fw = raw.Width;
            int fh = raw.Height;
            int count = raw.FrameCount;

            // square-ish grid; rows rounds up so last row can be partial
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / cols);

            var sheet = BuildAtlas(raw, fw, fh, count, cols, rows);

            string pngPath = Path.Combine(folder, baseName + "_highres.png");
            string jsonPath = Path.Combine(folder, baseName + "_highres.json");

            File.WriteAllBytes(pngPath, sheet.EncodeToPNG());

            meta.sheet_width = sheet.width;
            meta.sheet_height = sheet.height;
            meta.cell_width = fw;
            meta.cell_height = fh;
            meta.cols = cols;
            meta.rows = rows;
            meta.baked_at = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

            File.WriteAllText(jsonPath, JsonUtility.ToJson(meta, true));

            UnityEngine.Object.DestroyImmediate(sheet);
            return (pngPath, jsonPath);
        }

        // stitch frames into one big texture, row 0 at the top
        private static Texture2D BuildAtlas(RawFrameData raw, int fw, int fh, int count, int cols, int rows)
        {
            int sheetW = cols * fw;
            int sheetH = rows * fh;

            var sheet = new Texture2D(sheetW, sheetH, TextureFormat.RGBA32, false);
            sheet.filterMode = FilterMode.Point;
            sheet.SetPixels32(new Color32[sheetW * sheetH]);

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = rows - 1 - (i / cols); // flip Y -- Unity origin is bottom-left
                int x = col * fw;
                int y = row * fh;
                if (raw.Frames[i] != null)
                    sheet.SetPixels(x, y, fw, fh, raw.Frames[i].GetPixels());
            }
            sheet.Apply();
            return sheet;
        }
    }

    // Builds stable output names so debug instance labels do not leak into deliverables.
    public static class OutputNameUtility
    {
        // Join the character and clip names into one filesystem-safe base name.
        public static string BuildBaseName(string characterName, string clipName)
        {
            string characterPart = SanitizeSegment(characterName, "smear");
            string clipPart = SanitizeSegment(clipName, "unnamed");
            return $"{characterPart}_{clipPart}";
        }

        // Strip temp/debug naming and collapse punctuation to underscores.
        public static string SanitizeSegment(string raw, string fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            string cleaned = raw.Trim().Replace("(Clone)", string.Empty).Trim();
            if (cleaned == "_BakeTemp" || cleaned == "_PreviewTemp" || cleaned == "_ExperimentTemp")
                return fallback;

            var sb = new StringBuilder(cleaned.Length);
            bool lastWasUnderscore = false;

            for (int i = 0; i < cleaned.Length; i++)
            {
                char c = cleaned[i];
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                    lastWasUnderscore = false;
                    continue;
                }

                if (!lastWasUnderscore)
                {
                    sb.Append('_');
                    lastWasUnderscore = true;
                }
            }

            string result = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(result) ? fallback : result;
        }
    }
}
