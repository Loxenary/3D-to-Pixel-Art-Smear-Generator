using System.IO;
using UnityEditor;

namespace SmearFramework.Editor
{
    internal static class AssetFolderUtility
    {
        // Creates nested folders under Assets, preserving Unity's asset database view.
        public static void EnsureAssetFolder(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return;
            assetPath = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            if (!assetPath.StartsWith("Assets"))
            {
                Directory.CreateDirectory(assetPath);
                return;
            }

            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
