using System;
using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    public static class PixelAnimationPackageImportMenu
    {
        [MenuItem("Smear Generator/Utilities/Import Exported Pixel Art Animation")]
        static void ImportPixelAnimationPackage()
        {
            string folder = EditorUtility.OpenFolderPanel(
                "Import pixel animation package",
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "");
            if (string.IsNullOrEmpty(folder))
                return;

            try
            {
                var result = PixelAnimationPackageImporter.ImportExternalFolder(
                    folder, SmearFrameworkPaths.ImportedPackages);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            catch (InvalidOperationException ex)
            {
                EditorUtility.DisplayDialog("Import failed", ex.Message, "OK");
            }
        }
    }
}
