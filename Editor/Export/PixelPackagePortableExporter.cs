using System.IO;
using UnityEditor;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.PixelArtConversion;

namespace SmearFramework.Editor
{
    // Copies the generated pixel animation package to a folder outside the Unity project.
    public static class PixelPackagePortableExporter
    {
        // Opens a folder picker, then copies the package into the chosen destination root.
        public static string ExportToExternalFolder(SpriteSheetResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.PackageFolder)) return null;
            if (!Directory.Exists(result.PackageFolder)) return null;

            string defaultName = BuildDefaultExportBaseName(result);
            string destRoot = EditorUtility.SaveFolderPanel(
                "Export pixel animation package to...",
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
                defaultName);

            return ExportToExternalFolder(result, destRoot, defaultName);
        }

        // Copy the package into a caller-supplied destination root. Returns the final package path.
        public static string ExportToExternalFolder(SpriteSheetResult result, string destRoot)
        {
            return ExportToExternalFolder(result, destRoot, BuildDefaultExportBaseName(result));
        }

        // Copy the package into a caller-supplied destination root and rewrite the exported names.
        public static string ExportToExternalFolder(SpriteSheetResult result, string destRoot, string exportFileName)
        {
            if (result == null || string.IsNullOrEmpty(result.PackageFolder)) return null;
            if (!Directory.Exists(result.PackageFolder)) return null;
            if (string.IsNullOrWhiteSpace(destRoot)) return null;

            string safeName = OutputNameUtility.SanitizeSegment(exportFileName, BuildDefaultExportBaseName(result));
            string destPackage = Path.Combine(destRoot, safeName);
            CopyDirectory(result.PackageFolder, destPackage);
            RewriteExportedPackage(destPackage, safeName);
            return destPackage;
        }

        // Reuse the generated base name so the export field starts from a sensible default.
        public static string BuildDefaultExportBaseName(SpriteSheetResult result)
        {
            if (result != null && !string.IsNullOrEmpty(result.PrefabPath))
            {
                string fileName = Path.GetFileNameWithoutExtension(result.PrefabPath);
                if (fileName.EndsWith("_2d"))
                    fileName = fileName.Substring(0, fileName.Length - 3);
                return OutputNameUtility.SanitizeSegment(fileName, "pixel");
            }

            string folderName = result != null && !string.IsNullOrEmpty(result.PackageFolder)
                ? Path.GetFileName(result.PackageFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : "pixel";
            return OutputNameUtility.SanitizeSegment(folderName, "pixel");
        }

        // Rename the exported files so the package matches the user-facing filename field.
        static void RewriteExportedPackage(string destPackage, string baseName)
        {
            string animationPath = Path.Combine(destPackage, "animation.json");
            if (File.Exists(animationPath))
            {
                var meta = JsonUtility.FromJson<SpriteSheetMetadata>(File.ReadAllText(animationPath));
                if (meta != null)
                {
                    string oldSheet = string.IsNullOrEmpty(meta.sheetFile) ? "sprite_sheet.png" : meta.sheetFile;
                    string newSheet = baseName + ".png";
                    RenameIfPresent(Path.Combine(destPackage, oldSheet), Path.Combine(destPackage, newSheet));
                    meta.sheetFile = newSheet;
                    if (meta.frames != null)
                    {
                        for (int i = 0; i < meta.frames.Length; i++)
                            meta.frames[i].spriteName = $"{baseName}_{i:D4}";
                    }
                    File.WriteAllText(animationPath, JsonUtility.ToJson(meta, true));
                }
            }

            string manifestPath = Path.Combine(destPackage, "package.json");
            if (!File.Exists(manifestPath))
                return;

            var manifest = JsonUtility.FromJson<PixelAnimationPackageManifest>(File.ReadAllText(manifestPath));
            if (manifest == null)
                return;

            string newClipFile = baseName + "_2d.anim";
            string newControllerFile = baseName + "_2d.controller";
            string newPrefabFile = baseName + "_2d.prefab";
            string oldPrefabStem = Path.GetFileNameWithoutExtension(manifest.prefabAssetFile);
            bool hasControllerAsset = !string.IsNullOrEmpty(manifest.controllerAssetFile);
            RenameIfPresent(Path.Combine(destPackage, manifest.clipAssetFile), Path.Combine(destPackage, newClipFile));
            if (hasControllerAsset)
                RenameIfPresent(Path.Combine(destPackage, manifest.controllerAssetFile), Path.Combine(destPackage, newControllerFile));
            RenameIfPresent(Path.Combine(destPackage, manifest.prefabAssetFile), Path.Combine(destPackage, newPrefabFile));
            PatchPrefabRootName(Path.Combine(destPackage, newPrefabFile), oldPrefabStem, baseName);
            manifest.spriteSheetFile = baseName + ".png";
            manifest.clipAssetFile = newClipFile;
            if (hasControllerAsset)
                manifest.controllerAssetFile = newControllerFile;
            manifest.prefabAssetFile = newPrefabFile;
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
        }

        // Fix the root GameObject name inside the prefab YAML after a file rename.
        static void PatchPrefabRootName(string prefabPath, string oldPrefabStem, string newBaseName)
        {
            if (!File.Exists(prefabPath)) return;
            string oldGoName = oldPrefabStem.EndsWith("_2d")
                ? oldPrefabStem.Substring(0, oldPrefabStem.Length - 3) + "_Pixel2D"
                : oldPrefabStem + "_Pixel2D";
            string newGoName = newBaseName + "_Pixel2D";
            if (oldGoName == newGoName) return;
            string yaml = File.ReadAllText(prefabPath);
            string patched = yaml.Replace("m_Name: " + oldGoName, "m_Name: " + newGoName);
            if (patched != yaml)
                File.WriteAllText(prefabPath, patched);
        }

        // Rename a file when it exists and the target path differs.
        static void RenameIfPresent(string srcPath, string dstPath)
        {
            if (string.IsNullOrEmpty(srcPath) || string.IsNullOrEmpty(dstPath)) return;
            if (!File.Exists(srcPath)) return;
            if (srcPath == dstPath) return;
            if (File.Exists(dstPath))
                File.Delete(dstPath);
            File.Move(srcPath, dstPath);
        }

        static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string file in Directory.GetFiles(src))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".meta")) continue; // skip Unity internal meta files
                File.Copy(file, Path.Combine(dst, name), overwrite: true);
            }
            foreach (string sub in Directory.GetDirectories(src))
                CopyDirectory(sub, Path.Combine(dst, Path.GetFileName(sub)));
        }
    }
}
