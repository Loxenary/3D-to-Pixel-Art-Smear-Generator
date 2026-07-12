using System.IO;
using UnityEditor;
using UnityEngine;
using SmearFramework.DataTypes;

namespace SmearFramework.Editor
{
    // Popup window shown after a successful bake. Keeps the post-bake action focused on external export.
    public class SmearResultPopup : EditorWindow
    {
        private SpriteSheetResult _pixelResult;
        private SmearScene3DExporter.Result _smear3DResult;
        private float _bakeTimeMs;
        private string _status = "";
        private string _exportFolder;
        private string _exportFolderName;
        private bool _smear3DResultIsTemporary;
        private string _smear3DExportFolder;
        private string _smear3DExportBaseName;
        private System.Func<string, string, SmearScene3DExporter.Result> _exportSmear3D;
        private const string ExportFolderPrefKey = "SmearFramework.ExternalExportFolder";
        internal static void Show(SmearResultPopupModel model)
        {
            if (model == null) return;
            if (model.PixelResult == null && model.Smear3DResult == null) return;
            var win = CreateInstance<SmearResultPopup>();
            win.titleContent = new GUIContent("Pipeline complete");
            win._pixelResult = model.PixelResult;
            win._smear3DResult = model.Smear3DResult;
            win._bakeTimeMs = model.BakeTimeMs;
            win._smear3DResultIsTemporary = model.Smear3DResultIsTemporary;
            win._smear3DExportFolder = model.Smear3DExportFolder;
            win._smear3DExportBaseName = model.Smear3DExportBaseName;
            win._exportSmear3D = model.ExportSmear3D;
            win._exportFolder = !string.IsNullOrWhiteSpace(model.PixelExportFolder)
                ? model.PixelExportFolder
                : EditorPrefs.GetString(
                    ExportFolderPrefKey,
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop));
            win._exportFolderName = !string.IsNullOrWhiteSpace(model.PixelExportFolderName)
                ? model.PixelExportFolderName
                : PixelPackagePortableExporter.BuildDefaultExportBaseName(model.PixelResult);
            win.minSize = new Vector2(360, 210);
            win.ShowUtility();
            win.CenterOnMainWin();
        }

        void CenterOnMainWin()
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            var pos = position;
            pos.x = main.x + (main.width  - pos.width)  * 0.5f;
            pos.y = main.y + (main.height - pos.height) * 0.5f;
            position = pos;
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10);

            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            EditorGUILayout.LabelField("Done!", headerStyle, GUILayout.Height(22));

            var subStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.LabelField($"Finished in {_bakeTimeMs / 1000f:F1}s", subStyle);

            EditorGUILayout.Space(10);

            bool canExportFolder = _pixelResult != null && !string.IsNullOrEmpty(_pixelResult.PackageFolder);
            bool hasSmear3DResult = _smear3DResult != null && !string.IsNullOrEmpty(_smear3DResult.PrefabPath);
            if (canExportFolder)
            {
                string oldFolder = _exportFolder;
                string oldFolderName = _exportFolderName;
                EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);
                DrawExportFolderNameField();
                EditorGUILayout.Space(6);
                DrawExportFolderField();
                if (_exportFolder != oldFolder)
                    EditorPrefs.SetString(ExportFolderPrefKey, _exportFolder);
                EditorGUILayout.Space(6);

                EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(_exportFolder) || string.IsNullOrWhiteSpace(_exportFolderName));
                if (GUILayout.Button("Export Folder", GUILayout.Height(28)))
                {
                    string dest = PixelPackagePortableExporter.ExportToExternalFolder(_pixelResult, _exportFolder, _exportFolderName);
                    if (!string.IsNullOrEmpty(dest))
                    {
                        EditorPrefs.SetString(ExportFolderPrefKey, _exportFolder);
                        EditorUtility.RevealInFinder(dest);
                        Close();
                    }
                }
                EditorGUI.EndDisabledGroup();
            }


            if (hasSmear3DResult)
            {
                if (canExportFolder)
                    EditorGUILayout.Space(8);

                string smear3DHeading = _smear3DResultIsTemporary ? "Temporary 3D preview" : "3D output";
                EditorGUILayout.LabelField(smear3DHeading, EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                if (_smear3DResultIsTemporary)
                {
                    EditorGUILayout.LabelField(new GUIContent("Folder name", "Base name for the exported folder containing the prefab, clip, and mesh assets."), EditorStyles.miniBoldLabel);
                    _smear3DExportBaseName = EditorGUILayout.TextField(_smear3DExportBaseName);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField(new GUIContent("Export folder", "Unity project folder where the 3D smear prefab, animation clip, and mesh assets will be saved."), EditorStyles.miniBoldLabel);
                    EditorGUILayout.BeginHorizontal();
                    _smear3DExportFolder = EditorGUILayout.TextField(_smear3DExportFolder);
                    if (GUILayout.Button("Browse", GUILayout.Width(62), GUILayout.Height(24)))
                        BrowseSmear3DExportFolder();
                    EditorGUILayout.EndHorizontal();
                    if (!string.IsNullOrWhiteSpace(_smear3DExportFolder) || !string.IsNullOrWhiteSpace(_smear3DExportBaseName))
                    {
                        string folder = string.IsNullOrWhiteSpace(_smear3DExportFolder) ? "..." : _smear3DExportFolder.TrimEnd('/');
                        string name   = string.IsNullOrWhiteSpace(_smear3DExportBaseName) ? "..." : _smear3DExportBaseName.Trim();
                        var previewStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { fontStyle = FontStyle.Italic };
                        EditorGUILayout.LabelField($"→  {folder}/{name}/", previewStyle);
                    }
                    EditorGUILayout.Space(4);
                    bool canExport = _exportSmear3D != null
                        && IsValidProjectAssetFolder(_smear3DExportFolder)
                        && !string.IsNullOrWhiteSpace(_smear3DExportBaseName);
                    EditorGUI.BeginDisabledGroup(!canExport);
                    if (GUILayout.Button(new GUIContent("Export Folder", "Save the temporary 3D smear result into the selected Unity project folder."), GUILayout.Height(28)))
                    {
                        var finalResult = _exportSmear3D(_smear3DExportFolder, _smear3DExportBaseName);
                        if (finalResult != null)
                        {
                            _smear3DResult = finalResult;
                            _smear3DResultIsTemporary = false;
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    EditorGUILayout.LabelField(_smear3DResult.PrefabPath, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Select Prefab", EditorStyles.miniButton, GUILayout.Height(24)))
                        SelectProjectAsset(_smear3DResult.PrefabPath);
                    if (GUILayout.Button("Select Clip", EditorStyles.miniButton, GUILayout.Height(24)))
                        SelectProjectAsset(_smear3DResult.ClipPath);
                    if (GUILayout.Button("Select Meshes", EditorStyles.miniButton, GUILayout.Height(24)))
                        SelectProjectAsset(_smear3DResult.MeshFolder);
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (!canExportFolder && !hasSmear3DResult)
                EditorGUILayout.HelpBox("No result actions are available for this run.", MessageType.None);

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Close", GUILayout.Height(26)))
                Close();
        }


        // Mirror the main window folder name field so export naming stays explicit.
        void DrawExportFolderNameField()
        {
            EditorGUILayout.LabelField(new GUIContent("Folder name", "Name of the exported folder. Pixel frames go inside as filename_0000, filename_0001, ..."), EditorStyles.miniBoldLabel);
            _exportFolderName = EditorGUILayout.TextField(_exportFolderName);
        }

        // Keep the popup aligned with the main window -- destination first, internal assets hidden.
        void DrawExportFolderField()
        {
            EditorGUILayout.LabelField(new GUIContent("Export folder", "Destination outside the Unity project for the portable pixel package."), EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            _exportFolder = EditorGUILayout.TextField(_exportFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(62), GUILayout.Height(24)))
                BrowseExportFolder();
            EditorGUILayout.EndHorizontal();

            // show the resolved export path so the user knows exactly where the folder will land
            if (!string.IsNullOrWhiteSpace(_exportFolder) || !string.IsNullOrWhiteSpace(_exportFolderName))
            {
                string folder = string.IsNullOrWhiteSpace(_exportFolder) ? "..." : _exportFolder.TrimEnd('/');
                string name   = string.IsNullOrWhiteSpace(_exportFolderName) ? "..." : _exportFolderName.Trim();
                var previewStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { fontStyle = FontStyle.Italic };
                EditorGUILayout.LabelField($"→  {folder}/{name}/", previewStyle);
            }
        }

        // Pick the destination folder for the exported package copy.
        void BrowseExportFolder()
        {
            string start = Directory.Exists(_exportFolder)
                ? _exportFolder
                : EditorPrefs.GetString(
                    ExportFolderPrefKey,
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop));
            string picked = EditorUtility.OpenFolderPanel("Select export folder", start, "");
            if (!string.IsNullOrEmpty(picked))
            {
                _exportFolder = picked;
                EditorPrefs.SetString(ExportFolderPrefKey, picked);
            }
        }

        // Open a Unity project folder picker for the popup's 3D export destination.
        void BrowseSmear3DExportFolder()
        {
            string start = Directory.Exists(ToSystemPath(_smear3DExportFolder))
                ? ToSystemPath(_smear3DExportFolder)
                : Application.dataPath;
            string picked = EditorUtility.OpenFolderPanel("Select 3D output folder", start, "");
            if (string.IsNullOrEmpty(picked)) return;
            string assetPath = ToAssetPath(picked);
            if (!IsValidProjectAssetFolder(assetPath))
            {
                _status = "3D export folder must be inside Assets.";
                return;
            }
            _smear3DExportFolder = assetPath;
        }

        static bool IsValidProjectAssetFolder(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Assets");
        }

        static string ToSystemPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return Application.dataPath;
            string trimmed = assetPath.Replace('\\', '/').TrimEnd('/');
            if (trimmed.StartsWith("Assets"))
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", trimmed));
            return trimmed;
        }

        static string ToAssetPath(string systemPath)
        {
            string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            string normal   = Path.GetFullPath(systemPath).Replace('\\', '/');
            if (normal.StartsWith(dataPath))
                return "Assets" + normal.Substring(dataPath.Length);
            return normal;
        }

        void SelectProjectAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
