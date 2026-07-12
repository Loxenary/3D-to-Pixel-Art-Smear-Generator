using System;
using System.IO;
using UnityEngine;
using SmearFramework.DataTypes;

namespace SmearFramework.PixelArtConversion
{
    // Loads a baked high-res atlas plus its JSON sidecar.
    public static class HighResDiskLoader
    {
        public static (RawFrameData frames, HighResMetadata meta) Load(string pngPath)
        {
            if (!File.Exists(pngPath))
                throw new FileNotFoundException("PNG not found", pngPath);

            // Reject sprite-sheet exports that do not use the high-res bake schema.
            if (!pngPath.EndsWith("_highres.png"))
                throw new InvalidOperationException(
                    $"expected a *_highres.png file, got '{Path.GetFileName(pngPath)}'. Run a Smear Bake first to produce one.");

            string jsonPath = pngPath.Replace("_highres.png", "_highres.json");
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("sidecar JSON missing", jsonPath);

            var meta = JsonUtility.FromJson<HighResMetadata>(File.ReadAllText(jsonPath));

            if (meta.schema_version != 1)
                throw new InvalidOperationException(
                    $"unsupported HighResMetadata schema_version {meta.schema_version}; loader expects 1");

            // JsonUtility falls back to defaults on parse failure.
            if (meta.frame_count <= 0 || meta.cell_width <= 0 || meta.cell_height <= 0)
                throw new InvalidOperationException(
                    $"metadata in '{Path.GetFileName(jsonPath)}' is empty or malformed (frame_count={meta.frame_count}, cell={meta.cell_width}x{meta.cell_height})");

            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!atlas.LoadImage(File.ReadAllBytes(pngPath)))
                throw new InvalidOperationException("PNG load failed: " + pngPath);

            if (atlas.width < meta.cols * meta.cell_width || atlas.height < meta.rows * meta.cell_height)
                throw new InvalidOperationException(
                    $"atlas/metadata size mismatch: atlas {atlas.width}x{atlas.height}, meta expects {meta.cols * meta.cell_width}x{meta.rows * meta.cell_height}");

            if (meta.cols * meta.rows < meta.frame_count)
                throw new InvalidOperationException(
                    $"grid too small for frame_count: {meta.cols}x{meta.rows} < {meta.frame_count}");

            var raw = SliceAtlas(atlas, meta);
            UnityEngine.Object.DestroyImmediate(atlas);
            return (raw, meta);
        }

        private static RawFrameData SliceAtlas(Texture2D atlas, HighResMetadata meta)
        {
            var raw = new RawFrameData(meta.frame_count, meta.cell_width, meta.cell_height);
            for (int i = 0; i < meta.frame_count; i++)
            {
                int col = i % meta.cols;
                int row = meta.rows - 1 - (i / meta.cols);
                int x = col * meta.cell_width;
                int y = row * meta.cell_height;

                var pixels = atlas.GetPixels(x, y, meta.cell_width, meta.cell_height);
                var frame = new Texture2D(meta.cell_width, meta.cell_height, TextureFormat.RGBA32, false);
                frame.filterMode = FilterMode.Point;
                frame.SetPixels(pixels);
                frame.Apply();
                raw.Frames[i] = frame;
            }
            return raw;
        }
    }
}
