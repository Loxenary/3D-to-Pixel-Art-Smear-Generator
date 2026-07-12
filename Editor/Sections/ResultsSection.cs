using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Draws the Results card: external export fields, 3D smear section, and popup trigger.
    // Owns the editable folder/filename fields; the window syncs [SerializeField] backups via public getters.
    internal sealed class ResultsSection
    {
        private const float CompactButtonHeight = 24f;

        // Folder name fields owned here so Draw() can render them without ref params.
        // The window reads these back via public getters to persist them across domain reloads.
        private string _externalExportFolder;
        private string _externalExportFolderName;
        private string _smear3DExportFolder;
        private string _smear3DExportBaseName;

        public string ExternalExportFolder   => _externalExportFolder;
        public string ExternalExportFolderName => _externalExportFolderName;
        public string Smear3DExportFolder    => _smear3DExportFolder;
        public string Smear3DExportBaseName  => _smear3DExportBaseName;

        // Called from the window's OnEnable to restore [SerializeField]-backed values after domain reload.
        public void SeedDefaults(string folder, string folderName, string smear3DFolder, string smear3DBase)
        {
            if (string.IsNullOrWhiteSpace(_externalExportFolder))   _externalExportFolder   = folder;
            if (string.IsNullOrWhiteSpace(_externalExportFolderName)) _externalExportFolderName = folderName;
            if (string.IsNullOrWhiteSpace(_smear3DExportFolder))    _smear3DExportFolder    = smear3DFolder;
            if (string.IsNullOrWhiteSpace(_smear3DExportBaseName))  _smear3DExportBaseName  = smear3DBase;
        }

        // Overwrites the pixel export fields when another UI surface becomes the source of truth.
        public void SetPixelExport(string folder, string folderName)
        {
            if (!string.IsNullOrWhiteSpace(folder))
                _externalExportFolder = folder;
            if (!string.IsNullOrWhiteSpace(folderName))
                _externalExportFolderName = folderName;
        }

        // Draws the Results card body using the plan-exact callback signature.
        public void Draw(
            SmearFrameworkEditorState state,
            LayoutSection layout,
            System.Func<bool> canExportPixelPackage,
            System.Action exportPixelPackage,
            System.Func<bool> canExportSmear3D,
            System.Func<string, string, SmearScene3DExporter.Result> exportSmear3D)
        {
            bool hasPixelResult = state.LastPixelResult != null &&
                !string.IsNullOrEmpty(state.LastPixelResult.PackageFolder);
            bool hasSmear3DResult = state.LastSmear3DResult != null &&
                !string.IsNullOrEmpty(state.LastSmear3DResult.PrefabPath);

            if (hasPixelResult)
            {
                DrawExternalExportFolderNameField(state);
                layout.DrawGroupGap();
                DrawExternalExportFolderField();

                layout.DrawGroupGap();
                EditorGUI.BeginDisabledGroup(!canExportPixelPackage());
                if (GUILayout.Button(
                    new GUIContent("Export Folder", "Copy the latest pixel package to the chosen external folder."),
                    EditorStyles.miniButton, GUILayout.ExpandWidth(true), GUILayout.Height(CompactButtonHeight)))
                    exportPixelPackage();
                EditorGUI.EndDisabledGroup();
            }

            if (hasSmear3DResult)
            {
                if (hasPixelResult) layout.DrawGroupGap();
                DrawSmear3DResultSection(state, canExportSmear3D, exportSmear3D);
            }
        }

        // Seeds the folder name field from the latest pixel result when it is blank.
        private void DrawExternalExportFolderNameField(SmearFrameworkEditorState state)
        {
            if (string.IsNullOrWhiteSpace(_externalExportFolderName))
                _externalExportFolderName = PixelPackagePortableExporter.BuildDefaultExportBaseName(state.LastPixelResult);
            EditorGUILayout.LabelField(
                new GUIContent("Folder name",
                    "Name of the exported folder. Contents will be named foldername_0000, foldername_0001, ..."),
                EditorStyles.miniBoldLabel);
            _externalExportFolderName = EditorGUILayout.TextField(_externalExportFolderName);
        }

        // Draws the system folder picker for the external pixel package export.
        private void DrawExternalExportFolderField()
        {
            if (string.IsNullOrWhiteSpace(_externalExportFolder))
                _externalExportFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            EditorGUILayout.LabelField(
                new GUIContent("Export folder",
                    "Destination outside the Unity project for the portable pixel package."),
                EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            _externalExportFolder = EditorGUILayout.TextField(_externalExportFolder);
            if (GUILayout.Button("Browse", EditorStyles.miniButton,
                GUILayout.Width(62), GUILayout.Height(CompactButtonHeight)))
                _externalExportFolder = BrowseSystemFolder("Select export folder", _externalExportFolder);
            EditorGUILayout.EndHorizontal();
        }

        // Draws the 3D smear result section including temporary-preview export or asset selection buttons.
        private void DrawSmear3DResultSection(
            SmearFrameworkEditorState state,
            System.Func<bool> canExportSmear3D,
            System.Func<string, string, SmearScene3DExporter.Result> exportSmear3D)
        {
            string heading = state.LastSmear3DResultIsTemporary ? "Temporary 3D preview" : "3D output";
            EditorGUILayout.LabelField(heading, EditorStyles.miniBoldLabel);

            if (state.LastSmear3DResultIsTemporary)
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(
                    new GUIContent("Folder name",
                        "Name of the exported folder containing the prefab, clip, and mesh assets."),
                    EditorStyles.miniBoldLabel);
                _smear3DExportBaseName = EditorGUILayout.TextField(_smear3DExportBaseName);
                EditorGUILayout.Space(2f);
                DrawSmear3DExportFolderField();
                EditorGUILayout.Space(2f);
                EditorGUI.BeginDisabledGroup(!canExportSmear3D());
                if (GUILayout.Button(
                    new GUIContent("Export Folder",
                        "Save the temporary 3D smear result into the selected Unity project folder."),
                    EditorStyles.miniButton, GUILayout.ExpandWidth(true), GUILayout.Height(CompactButtonHeight)))
                {
                    var result = exportSmear3D(_smear3DExportFolder, _smear3DExportBaseName);
                    if (result != null)
                    {
                        // state.LastSmear3DResult is updated by the window callback; just repaint
                        UnityEditor.EditorWindow.focusedWindow?.Repaint();
                    }
                }
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                var result = state.LastSmear3DResult;
                EditorGUILayout.LabelField(result.PrefabPath, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select Prefab", EditorStyles.miniButton,
                    GUILayout.ExpandWidth(true), GUILayout.Height(CompactButtonHeight)))
                    SelectProjectAsset(result.PrefabPath);
                if (GUILayout.Button("Select Clip", EditorStyles.miniButton,
                    GUILayout.ExpandWidth(true), GUILayout.Height(CompactButtonHeight)))
                    SelectProjectAsset(result.ClipPath);
                if (GUILayout.Button("Select Meshes", EditorStyles.miniButton,
                    GUILayout.ExpandWidth(true), GUILayout.Height(CompactButtonHeight)))
                    SelectProjectAsset(result.MeshFolder);
                EditorGUILayout.EndHorizontal();
            }
        }

        // Draws the Unity project folder picker for the 3D smear export destination.
        private void DrawSmear3DExportFolderField()
        {
            if (string.IsNullOrWhiteSpace(_smear3DExportFolder))
                _smear3DExportFolder = SmearFrameworkPaths.Output;
            EditorGUILayout.LabelField(
                new GUIContent("Export folder",
                    "Unity project folder where the 3D smear prefab, animation clip, and mesh assets will be saved."),
                EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            _smear3DExportFolder = EditorGUILayout.TextField(_smear3DExportFolder);
            if (GUILayout.Button("Browse", EditorStyles.miniButton,
                GUILayout.Width(62), GUILayout.Height(CompactButtonHeight)))
                _smear3DExportFolder = BrowseProjectFolder("Select 3D output folder", _smear3DExportFolder);
            EditorGUILayout.EndHorizontal();
        }

        // Pings and selects a project asset by asset path.
        private void SelectProjectAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null) return;
            UnityEditor.Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        // Opens a system-level folder picker and returns the result (unchanged on cancel).
        private string BrowseSystemFolder(string title, string current)
        {
            string start = Directory.Exists(current) ? current
                : System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            string picked = EditorUtility.OpenFolderPanel(title, start, "");
            return string.IsNullOrEmpty(picked) ? current : picked;
        }

        // Opens a Unity project folder picker; returns asset path or original on cancel/outside Assets.
        private string BrowseProjectFolder(string title, string current)
        {
            string dataPath = System.IO.Path.GetFullPath(UnityEngine.Application.dataPath);
            string start = string.IsNullOrEmpty(current)
                ? dataPath
                : System.IO.Path.GetFullPath(System.IO.Path.Combine(dataPath, "..", current));
            string picked = EditorUtility.OpenFolderPanel(title, start, "");
            if (string.IsNullOrEmpty(picked)) return current;
            string normal = System.IO.Path.GetFullPath(picked).Replace('\\', '/');
            string data   = dataPath.Replace('\\', '/');
            return normal.StartsWith(data)
                ? "Assets" + normal.Substring(data.Length)
                : current;
        }
    }
}
