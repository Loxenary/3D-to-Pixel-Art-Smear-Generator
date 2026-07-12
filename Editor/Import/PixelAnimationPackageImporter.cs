using System;
using System.IO;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.PixelArtConversion;

namespace SmearFramework.Editor
{
    // Rebuilds Unity assets from a portable pixel animation data folder.
    public static class PixelAnimationPackageImporter
    {
        public static PixelAnimationPackageExporter.Result ImportExternalFolder(
            string sourceFolder, string outputRoot, string packageName = null)
        {
            if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
                throw new InvalidOperationException("pixel package folder not found: " + sourceFolder);

            string manifestPath = Path.Combine(sourceFolder, "package.json");
            if (!File.Exists(manifestPath))
                throw new InvalidOperationException("package.json not found: " + sourceFolder);

            var manifest = JsonUtility.FromJson<PixelAnimationPackageManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.schema_version != 1 || manifest.packageType != "smear_pixel_animation")
                throw new InvalidOperationException("unsupported pixel package: " + sourceFolder);

            string animationPath = Path.Combine(sourceFolder, "animation.json");
            if (!File.Exists(animationPath))
                throw new InvalidOperationException("animation.json not found: " + sourceFolder);

            var meta = JsonUtility.FromJson<SpriteSheetMetadata>(File.ReadAllText(animationPath));
            string sheetPath = Path.Combine(sourceFolder, manifest.spriteSheetFile);
            if (!File.Exists(sheetPath))
                throw new InvalidOperationException("sprite sheet not found: " + sheetPath);

            byte[] pngBytes = File.ReadAllBytes(sheetPath);
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!sheet.LoadImage(pngBytes))
                    throw new InvalidOperationException("could not read sprite sheet: " + sheetPath);

                string defaultName = Path.GetFileName(
                    sourceFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string safeName = OutputNameUtility.SanitizeSegment(
                    string.IsNullOrWhiteSpace(packageName) ? defaultName : packageName,
                    "pixel");
                meta.sheetFile = safeName + ".png";
                if (meta.frames != null)
                {
                    for (int i = 0; i < meta.frames.Length; i++)
                        meta.frames[i].spriteName = safeName + "_" + i.ToString("D4");
                }

                return PixelAnimationPackageExporter.ExportNamedPackage(
                    outputRoot, safeName, safeName, sheet, meta);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sheet);
            }
        }
    }
}
