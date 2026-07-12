using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    /// <summary>Extracts embedded FBX PNG textures into the .fbm folder Unity expects.</summary>
    public static class FbxTextureFixer
    {
        private static readonly byte[] PngStart = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] PngEnd = { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };

        // Fix one Unity FBX asset by extracting embedded PNGs and refreshing the importer.
        public static FixResult Fix(string assetPath)
        {
            return Fix(assetPath, null, null);
        }

        // Fix one Unity FBX asset using an explicit texture folder name and parent path.
        public static FixResult Fix(string assetPath, string folderName, string folderPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                return FixResult.Fail("Select an FBX character asset first.");

            string fullPath = ToFullPath(assetPath);
            if (!File.Exists(fullPath))
                return FixResult.Fail("FBX file does not exist: " + assetPath);

            byte[] fbxBytes = File.ReadAllBytes(fullPath);
            var expectedNames = FindExpectedTextureNames(fbxBytes);
            var pngs = ExtractPngs(fbxBytes);
            if (pngs.Count == 0)
                return FixResult.Fail("No embedded PNG bytes found in this FBX. Export/copy the texture files beside the FBX.");

            string inferredFolderName = FindExpectedFbmFolderName(fbxBytes);
            if (string.IsNullOrEmpty(folderName))
                folderName = string.IsNullOrEmpty(inferredFolderName)
                    ? Path.GetFileNameWithoutExtension(assetPath) + ".fbm"
                    : inferredFolderName;

            string assetFolder = Path.GetDirectoryName(assetPath).Replace("\\", "/");
            if (string.IsNullOrEmpty(folderPath))
                folderPath = assetFolder;
            folderName = NormalizeFolderName(folderName);
            folderPath = NormalizeAssetFolder(folderPath);
            if (string.IsNullOrEmpty(folderName))
                return FixResult.Fail("Folder Name is empty.");
            if (string.IsNullOrEmpty(folderPath) || !folderPath.StartsWith("Assets", System.StringComparison.Ordinal))
                return FixResult.Fail("Folder Path must be inside Assets.");

            string textureAssetFolder = folderPath.TrimEnd('/') + "/" + folderName;
            string textureFullFolder = ToFullPath(textureAssetFolder);
            Directory.CreateDirectory(textureFullFolder);

            var classified = ClassifyPngs(pngs);
            int written = WriteNamedTextures(textureFullFolder, expectedNames, classified);
            WriteExtractedCopies(textureFullFolder, pngs);

            AssetDatabase.ImportAsset(textureAssetFolder, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            string message = $"FBX texture fix wrote {written} named texture files to {textureAssetFolder}.";
            return FixResult.Ok(message, textureAssetFolder, written);
        }

        // Guess the texture folder name from an FBX asset.
        public static string GuessFolderName(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            string fullPath = ToFullPath(assetPath);
            if (!File.Exists(fullPath)) return Path.GetFileNameWithoutExtension(assetPath) + ".fbm";
            string name = FindExpectedFbmFolderName(File.ReadAllBytes(fullPath));
            return string.IsNullOrEmpty(name) ? Path.GetFileNameWithoutExtension(assetPath) + ".fbm" : name;
        }

        // Return the Unity asset folder that contains the FBX.
        public static string GuessFolderPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return string.Empty;
            return Path.GetDirectoryName(assetPath).Replace("\\", "/");
        }

        // Resolve an object field selection to an FBX asset path when possible.
        public static string ResolveFbxAssetPath(Object selected)
        {
            if (selected == null) return null;
            string path = AssetDatabase.GetAssetPath(selected);
            if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                return path;
            return null;
        }

        // Convert a Unity project-relative path to an absolute filesystem path.
        static string ToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath);
        }

        // Keep only the leaf folder name, because Folder Path owns the parent path.
        static string NormalizeFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return string.Empty;
            folderName = folderName.Replace("\\", "/").Trim('/');
            if (folderName.Contains("/"))
                folderName = folderName.Substring(folderName.LastIndexOf('/') + 1);
            return folderName;
        }

        // Normalize a Unity project-relative asset folder path.
        static string NormalizeAssetFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return string.Empty;
            return folderPath.Replace("\\", "/").TrimEnd('/');
        }

        // Pull likely texture filenames from printable FBX strings.
        static List<string> FindExpectedTextureNames(byte[] bytes)
        {
            string text = Encoding.UTF8.GetString(bytes);
            var matches = Regex.Matches(text, @"[A-Za-z0-9_ @().\-]+?\.(?:png|jpg|jpeg)", RegexOptions.IgnoreCase);
            var names = new List<string>();
            foreach (Match match in matches)
            {
                string value = match.Value.Replace("\\", "/");
                string leaf = value.Contains("/") ? value.Substring(value.LastIndexOf('/') + 1) : value;
                if (!names.Any(n => string.Equals(n, leaf, System.StringComparison.OrdinalIgnoreCase)))
                    names.Add(leaf);
            }
            return names;
        }

        // Find the .fbm folder name referenced by a Blender-style FBX export.
        static string FindExpectedFbmFolderName(byte[] bytes)
        {
            string text = Encoding.UTF8.GetString(bytes);
            var match = Regex.Match(text, @"([A-Za-z0-9_ @().\-]+\.fbm)/", RegexOptions.IgnoreCase);
            return match.Success && !match.Value.Contains("..") ? match.Groups[1].Value : null;
        }

        // Extract complete embedded PNG byte ranges from the FBX.
        static List<byte[]> ExtractPngs(byte[] bytes)
        {
            var pngs = new List<byte[]>();
            int cursor = 0;
            while (cursor < bytes.Length)
            {
                int start = FindBytes(bytes, PngStart, cursor);
                if (start < 0) break;
                int end = FindBytes(bytes, PngEnd, start + PngStart.Length);
                if (end < 0) break;
                int length = end + PngEnd.Length - start;
                var png = new byte[length];
                System.Buffer.BlockCopy(bytes, start, png, 0, length);
                pngs.Add(png);
                cursor = end + PngEnd.Length;
            }
            return pngs;
        }

        // Locate a byte pattern in a larger byte array.
        static int FindBytes(byte[] bytes, byte[] pattern, int start)
        {
            for (int i = start; i <= bytes.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (bytes[i + j] == pattern[j]) continue;
                    match = false;
                    break;
                }
                if (match) return i;
            }
            return -1;
        }

        // Classify extracted PNGs by simple color statistics.
        static List<ClassifiedPng> ClassifyPngs(List<byte[]> pngs)
        {
            var result = new List<ClassifiedPng>();
            foreach (byte[] png in pngs)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                ImageConversion.LoadImage(texture, png);
                Color average = AverageColor(texture);
                string kind = GuessTextureKind(average);
                result.Add(new ClassifiedPng(kind, png, average));
                Object.DestroyImmediate(texture);
            }
            return result;
        }

        // Average every sampled pixel so the kind guess stays stable enough for imported test assets.
        static Color AverageColor(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            if (pixels == null || pixels.Length == 0) return Color.black;

            long r = 0;
            long g = 0;
            long b = 0;
            int step = Mathf.Max(1, pixels.Length / 4096);
            int count = 0;
            for (int i = 0; i < pixels.Length; i += step)
            {
                r += pixels[i].r;
                g += pixels[i].g;
                b += pixels[i].b;
                count++;
            }

            return new Color(r / (255f * count), g / (255f * count), b / (255f * count), 1f);
        }

        // Guess diffuse, normal, glow, or specular from channel balance.
        static string GuessTextureKind(Color average)
        {
            if (average.b > 0.75f && average.b > average.r + 0.35f && average.b > average.g + 0.35f)
                return "normal";
            float brightness = (average.r + average.g + average.b) / 3f;
            if (brightness < 0.08f)
                return "glow";
            float rg = Mathf.Abs(average.r - average.g);
            float gb = Mathf.Abs(average.g - average.b);
            if (rg < 0.05f && gb < 0.05f && brightness > 0.25f)
                return "specular";
            return "diffuse";
        }

        // Write expected texture filenames using the best classified embedded PNG.
        static int WriteNamedTextures(string folder, List<string> expectedNames, List<ClassifiedPng> classified)
        {
            int written = 0;
            foreach (string expected in expectedNames)
            {
                string kind = GuessKindFromName(expected);
                var match = classified.FirstOrDefault(p => p.Kind == kind) ?? classified.FirstOrDefault();
                if (match == null) continue;

                File.WriteAllBytes(Path.Combine(folder, expected), match.Bytes);
                written++;
            }
            return written;
        }

        // Keep raw extracted files for manual inspection when name mapping is imperfect.
        static void WriteExtractedCopies(string folder, List<byte[]> pngs)
        {
            for (int i = 0; i < pngs.Count; i++)
            {
                string path = Path.Combine(folder, $"extracted_{i + 1:00}.png");
                if (!File.Exists(path))
                    File.WriteAllBytes(path, pngs[i]);
            }
        }

        // Guess intended texture kind from the filename.
        static string GuessKindFromName(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("normal") || lower.Contains("bump")) return "normal";
            if (lower.Contains("glow") || lower.Contains("emission")) return "glow";
            if (lower.Contains("spec")) return "specular";
            return "diffuse";
        }

        public sealed class FixResult
        {
            public bool Success;
            public string Message;
            public string TextureFolder;
            public int WrittenFiles;

            // Create a successful result.
            public static FixResult Ok(string message, string textureFolder, int writtenFiles)
            {
                return new FixResult { Success = true, Message = message, TextureFolder = textureFolder, WrittenFiles = writtenFiles };
            }

            // Create a failed result.
            public static FixResult Fail(string message)
            {
                return new FixResult { Success = false, Message = message };
            }
        }

        sealed class ClassifiedPng
        {
            public string Kind;
            public byte[] Bytes;
            public Color Average;

            // Store one extracted PNG and its guessed texture kind.
            public ClassifiedPng(string kind, byte[] bytes, Color average)
            {
                Kind = kind;
                Bytes = bytes;
                Average = average;
            }
        }
    }
}
