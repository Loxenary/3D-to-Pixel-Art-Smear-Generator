using System;
using System.Collections.Generic;
using UnityEngine;
using SmearFramework.DataTypes;
using System.IO;
using SmearFramework.PixelArtConversion;
using SmearFramework.Editor;

namespace SmearFramework.Stages
{
    [InternalStage]
    public class SpriteSheetExportStage : IPipelineStage
    {
        public string Name => "Sprite Sheet Export";

        public IReadOnlyList<ArtifactKey> InputKey => new[]
        {
            ArtifactKey.Of<RawFrameData>("frames_pixelized"),
        };
        public IReadOnlyList<ArtifactKey> OutputKey => new[]
        {
            ArtifactKey.Of<SpriteSheetResult>("sprite_sheet"),
        };

        // grid-pack frames and write PNG + JSON to disk
        public void Execute(PipelineContext ctx)
        {
            var raw = ctx.Get<RawFrameData>("frames_pixelized");
            if (raw == null || raw.FrameCount == 0) return;
            NormalizeClipHeight(ctx, raw);

            int fw = raw.Width;
            int fh = raw.Height;
            int count = raw.FrameCount;

            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / cols);

            var sheet = PackSpriteSheet(raw, fw, fh, count, cols, rows);

            string folder = ctx.Config != null ? ctx.Config.OutputDirectory : SmearFrameworkPaths.Output;
            string clipName = ctx.Clip != null ? ctx.Clip.name : "unnamed";
            string targetName = ctx.Target != null ? ctx.Target.name : "smear";
            string baseName = OutputNameUtility.BuildBaseName(targetName, clipName);
            var meta = SpriteSheetMetadataBuilder.Build(
                targetName,
                clipName,
                count,
                fw,
                fh,
                cols,
                rows,
                sheet.width,
                sheet.height,
                ctx.Config.TargetFps,
                ctx.Config.CaptureResolution,
                ctx.Has("smear_data") ? ctx.Get<SmearFrameData>("smear_data") : null,
                ctx.Config.EnableElongated || ctx.Config.EnableMultiples || ctx.Config.EnableMotionLines,
                ctx.Config.LoopPlayback,
                ctx.Config.PixelsPerUnit,
                ctx.Config.PivotNormalized.x,
                ctx.Config.PivotNormalized.y,
                "pixel",
                ctx.Config.Prefix,
                ctx.Config.Suffix);
            string pngPath = null;
            string jsonPath = null;
            PixelAnimationPackageExporter.Result package = null;

#if UNITY_EDITOR
            (pngPath, jsonPath) = SpriteSheetDiskWriter.Save(folder, baseName, sheet, meta);
            PixelAnimationPackageExporter.ConfigureSpriteTexture(pngPath, meta);
            package = PixelAnimationPackageExporter.Export(folder, targetName, clipName, sheet, meta);
#endif

            var result = new SpriteSheetResult
            {
                SpriteSheet = sheet,
                Columns = cols,
                Rows = rows,
                FrameWidth = fw,
                FrameHeight = fh,
                FrameCount = count,
                FrameDuration = 1f / ctx.Config.TargetFps,
                PngPath = pngPath,
                JsonPath = jsonPath,
                PackageFolder = package != null ? package.PackageFolder : null,
                AnimationJsonPath = package != null ? package.AnimationJsonPath : null,
                PackageJsonPath = package != null ? package.PackageJsonPath : null,
                ClipPath = package != null ? package.ClipPath : null,
                ControllerPath = package != null ? package.ControllerPath : null,
                PrefabPath = package != null ? package.PrefabPath : null
            };
            ctx.Set("sprite_sheet", result);
        }

        // scale every frame in the clip by one shared factor so the visible character height matches the reference pose height
        private void NormalizeClipHeight(PipelineContext ctx, RawFrameData raw)
        {
            if (!ctx.Has("capture_frame")) return;

            var capture = ctx.Get<CaptureFrame>("capture_frame");
            int targetHeight = Mathf.RoundToInt(capture.ReferencePixelHeight);
            if (targetHeight <= 0) return;

            int clipMaxHeight = 0;
            for (int i = 0; i < raw.FrameCount; i++)
            {
                if (raw.Frames[i] == null) continue;
                var bounds = FindOpaqueBounds(raw.Frames[i]);
                if (bounds.height > clipMaxHeight)
                    clipMaxHeight = bounds.height;
            }

            if (clipMaxHeight <= 0 || clipMaxHeight == targetHeight) return;

            float scale = (float)targetHeight / clipMaxHeight;
            for (int i = 0; i < raw.FrameCount; i++)
            {
                if (raw.Frames[i] == null) continue;
                raw.Frames[i] = ScaleFrameToClipHeight(raw.Frames[i], scale);
            }
        }

        // nearest-neighbor scale around the opaque bounds center, preserving the original foot line
        private Texture2D ScaleFrameToClipHeight(Texture2D src, float scale)
        {
            var bounds = FindOpaqueBounds(src);
            if (bounds.height <= 0 || Mathf.Approximately(scale, 1f))
                return src;

            int dstW = src.width;
            int dstH = src.height;
            var srcPx = src.GetPixels32();
            var dstPx = new Color32[dstW * dstH];

            float srcCenterX = (bounds.xMin + bounds.xMax) * 0.5f;
            float srcBottom = bounds.yMax;
            float dstCenterX = srcCenterX;
            float dstBottom = srcBottom;

            for (int y = 0; y < dstH; y++)
            {
                for (int x = 0; x < dstW; x++)
                {
                    float srcX = ((x - dstCenterX) / scale) + srcCenterX;
                    float srcY = ((y - dstBottom) / scale) + srcBottom;
                    int sx = Mathf.RoundToInt(srcX);
                    int sy = Mathf.RoundToInt(srcY);
                    if (sx < 0 || sx >= dstW || sy < 0 || sy >= dstH) continue;
                    dstPx[y * dstW + x] = srcPx[sy * dstW + sx];
                }
            }

            var dst = new Texture2D(dstW, dstH, TextureFormat.RGBA32, false);
            dst.filterMode = FilterMode.Point;
            dst.SetPixels32(dstPx);
            dst.Apply();
            return dst;
        }

        // opaque bounds in pixel space; used for clip-level height normalization
        private RectInt FindOpaqueBounds(Texture2D tex)
        {
            var px = tex.GetPixels32();
            int minX = tex.width;
            int maxX = -1;
            int minY = tex.height;
            int maxY = -1;
            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    if (px[y * tex.width + x].a <= 12) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
                return new RectInt(0, 0, 0, 0);
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        // blit each frame into its grid cell
        private Texture2D PackSpriteSheet(RawFrameData raw, int fw, int fh, int count, int cols, int rows)
        {
            int sheetW = cols * fw;
            int sheetH = rows * fh;

            var sheet = new Texture2D(sheetW, sheetH, TextureFormat.RGBA32, false);
            sheet.filterMode = FilterMode.Point;

            var clear = new Color32[sheetW * sheetH];
            sheet.SetPixels32(clear);

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = rows - 1 - (i / cols);
                int x = col * fw;
                int y = row * fh;

                if (raw.Frames[i] != null)
                    sheet.SetPixels(x, y, fw, fh, raw.Frames[i].GetPixels());
            }
            sheet.Apply();

            return sheet;
        }
    }

    public static class SpriteSheetMetadataBuilder
    {
        // Build the JSON payload shared by the stage exporter and the manual preview exporter.
        public static SpriteSheetMetadata Build(
            string characterName, string clipName, int count, int fw, int fh, int cols, int rows,
            int sheetW, int sheetH, int fps, int captureResolution, SmearFrameData smear, bool smearEnabled,
            bool loopPlayback = true, int pixelsPerUnit = 32, float pivotX = 0.5f, float pivotY = 0f,
            string outputMode = "pixel", string prefix = "", string suffix = "")
        {
            float frameDur = 1f / fps;

            var meta = new SpriteSheetMetadata
            {
                schema_version = 1,
                outputMode = outputMode,
                characterName = OutputNameUtility.SanitizeSegment(characterName, "unknown"),
                clipName = OutputNameUtility.SanitizeSegment(clipName, "unnamed"),
                sheetFile = "sprite_sheet.png",
                frameCount = count,
                frameWidth = fw,
                frameHeight = fh,
                columns = cols,
                rows = rows,
                fps = fps,
                loopPlayback = loopPlayback,
                pixelsPerUnit = Mathf.Max(1, pixelsPerUnit),
                pivotX = pivotX,
                pivotY = pivotY,
                frameDuration = frameDur,
                totalDuration = count * frameDur,
                sheetWidth = sheetW,
                sheetHeight = sheetH,
                captureResolution = captureResolution,
                smearEnabled = smearEnabled,
                frames = BuildFrames(cols, fw, fh, count, smear, prefix, suffix)
            };
            return meta;
        }

        // Build per-frame atlas positions plus smear flags.
        static SpriteSheetFrameMetadata[] BuildFrames(int cols, int fw, int fh, int count, SmearFrameData smear, string prefix = "", string suffix = "")
        {
            var frames = new SpriteSheetFrameMetadata[count];
            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                frames[i] = new SpriteSheetFrameMetadata
                {
                    index = i,
                    spriteName = (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix))
                        ? $"frame_{i:D3}"
                        : $"{prefix}{i:D3}{suffix}",
                    x = col * fw,
                    y = row * fh,
                    width = fw,
                    height = fh,
                    hasSmear = smear != null && smear.HasSmear[i],
                    smearIntensity = smear != null ? smear.SmearIntensity[i] : 0f
                };
            }
            return frames;
        }
    }

    public static class SpriteSheetDiskWriter
    {
        // Write the packed sheet plus metadata JSON to disk and return both paths.
        public static (string pngPath, string jsonPath) Save(
            string folder, string baseName, Texture2D sheet, SpriteSheetMetadata meta)
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string pngPath = Path.Combine(folder, baseName + "_sheet.png");
            string jsonPath = Path.Combine(folder, baseName + "_meta.json");

            File.WriteAllBytes(pngPath, sheet.EncodeToPNG());
            File.WriteAllText(jsonPath, JsonUtility.ToJson(meta, true));

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
            Debug.Log($"Sprite sheet exported: {pngPath} ({sheet.width}x{sheet.height}, {meta.frameCount} frames)\nMetadata: {jsonPath}");
            return (pngPath, jsonPath);
        }
    }
}
