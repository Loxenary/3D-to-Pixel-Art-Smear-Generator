using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    [CustomEditor(typeof(OutputConfig))]
    public class OutputConfigEditor : UnityEditor.Editor
    {
        SerializedProperty _outputResolution;
        SerializedProperty _captureResolution;
        SerializedProperty _paletteSize;
        SerializedProperty _enableOutline;
        SerializedProperty _outlineColor;
        SerializedProperty _saveHighResToDisk;
        SerializedProperty _pixelsPerUnit;
        SerializedProperty _loopPlayback;
        SerializedProperty _pivotNormalized;
        SerializedProperty _outputDirectory;
        SerializedProperty _folderName;
        SerializedProperty _prefix;
        SerializedProperty _suffix;

        bool _showFolderName = true;

        void OnEnable()
        {
            _outputResolution  = serializedObject.FindProperty("_outputResolution");
            _captureResolution = serializedObject.FindProperty("_captureResolution");
            _paletteSize       = serializedObject.FindProperty("_paletteSize");
            _enableOutline     = serializedObject.FindProperty("_enableOutline");
            _outlineColor      = serializedObject.FindProperty("_outlineColor");
            _saveHighResToDisk = serializedObject.FindProperty("_saveHighResToDisk");
            _pixelsPerUnit     = serializedObject.FindProperty("_pixelsPerUnit");
            _loopPlayback      = serializedObject.FindProperty("_loopPlayback");
            _pivotNormalized   = serializedObject.FindProperty("_pivotNormalized");
            _outputDirectory   = serializedObject.FindProperty("_outputDirectory");
            _folderName          = serializedObject.FindProperty("_folderName");
            _prefix            = serializedObject.FindProperty("_prefix");
            _suffix            = serializedObject.FindProperty("_suffix");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool narrow = !EditorGUIUtility.wideMode;

            // Render settings 
            EditorGUILayout.PropertyField(_outputResolution);
            EditorGUILayout.PropertyField(_captureResolution);
            EditorGUILayout.PropertyField(_paletteSize);
            EditorGUILayout.PropertyField(_enableOutline);
            if (_enableOutline.boolValue)
                EditorGUILayout.PropertyField(_outlineColor);
            EditorGUILayout.PropertyField(_pixelsPerUnit);
            EditorGUILayout.PropertyField(_loopPlayback);
            DrawPivotNormalized(narrow);
            EditorGUILayout.PropertyField(_saveHighResToDisk);

            EditorGUILayout.Space(10);

            // --- Output directory with browse button ---
            EditorGUILayout.LabelField("Output Directory", EditorStyles.boldLabel);
            if (narrow)
            {
                EditorGUI.BeginChangeCheck();
                string edited = EditorGUILayout.TextField(_outputDirectory.stringValue);
                if (EditorGUI.EndChangeCheck())
                    _outputDirectory.stringValue = edited;

                if (GUILayout.Button("Browse", GUILayout.ExpandWidth(true)))
                    BrowseOutputDirectory();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                string edited = EditorGUILayout.TextField(_outputDirectory.stringValue);
                if (EditorGUI.EndChangeCheck())
                    _outputDirectory.stringValue = edited;

                if (GUILayout.Button("Browse", GUILayout.Width(62)))
                    BrowseOutputDirectory();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);

            // --- Folder name section ---
            _showFolderName = EditorGUILayout.BeginFoldoutHeaderGroup(_showFolderName, "Folder name");
            if (_showFolderName)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(_folderName,
                    new GUIContent("Name",
                        "Subfolder created inside the output directory. Also used as the base identifier " +
                        "in sprite names. Leave empty to auto-name from character + clip."));

                if (narrow)
                {
                    EditorGUILayout.PropertyField(_prefix,
                        new GUIContent("Prefix", "Prepended before the frame number. E.g. 'run-' gives run-000, run-001 ..."));
                    EditorGUILayout.PropertyField(_suffix,
                        new GUIContent("Suffix", "Appended after the frame number. E.g. '-v2' gives 000-v2, 001-v2 ..."));
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(_prefix,
                        new GUIContent("Prefix", "Prepended before the frame number. E.g. 'run-' gives run-000, run-001 ..."));
                    EditorGUILayout.LabelField("…000…", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(42));
                    EditorGUILayout.PropertyField(_suffix,
                        new GUIContent("Suffix", "Appended after the frame number. E.g. '-v2' gives 000-v2, 001-v2 ..."));
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Space(4);
                DrawPreview();

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();
        }

        // show a read-only preview of what the save path and sprite names will look like
        void DrawPreview()
        {
            string baseDir = _outputDirectory.stringValue.TrimEnd('/');
            string name    = _folderName.stringValue != null ? _folderName.stringValue.Trim() : "";
            string prefix  = _prefix.stringValue ?? "";
            string suffix  = _suffix.stringValue ?? "";

            string resolvedDir  = string.IsNullOrEmpty(name) ? baseDir : baseDir + "/" + name;
            string spriteSample = (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix))
                ? "frame_000, frame_001, ..."
                : $"{prefix}000{suffix}, {prefix}001{suffix}, ...";

            EditorGUILayout.HelpBox(
                $"Save folder:   {resolvedDir}/\n" +
                $"Sprite names:  {spriteSample}",
                MessageType.None);
        }

        // Draw pivot fields without forcing Unity's vector2 row in narrow embedded layout.
        void DrawPivotNormalized(bool narrow)
        {
            if (!narrow)
            {
                EditorGUILayout.PropertyField(_pivotNormalized);
                return;
            }

            EditorGUILayout.LabelField(_pivotNormalized.displayName);
            EditorGUI.indentLevel++;
            var value = _pivotNormalized.vector2Value;
            value.x = EditorGUILayout.FloatField("X", value.x);
            value.y = EditorGUILayout.FloatField("Y", value.y);
            _pivotNormalized.vector2Value = value;
            EditorGUI.indentLevel--;
        }

        // Browse for an output directory and store it as an Assets-relative path when possible.
        void BrowseOutputDirectory()
        {
            string systemStart = ToSystemPath(_outputDirectory.stringValue);
            if (!Directory.Exists(systemStart))
                systemStart = Application.dataPath;

            string picked = EditorUtility.OpenFolderPanel("Select output folder", systemStart, "");
            if (!string.IsNullOrEmpty(picked))
                _outputDirectory.stringValue = ToAssetPath(picked);
        }

        // convert an Assets-relative path to an absolute system path
        static string ToSystemPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return Application.dataPath;
            string trimmed = assetPath.Replace('\\', '/').TrimEnd('/');
            if (trimmed.StartsWith("Assets"))
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", trimmed));
            return trimmed;
        }

        // convert an absolute system path back to an Assets/ relative path
        static string ToAssetPath(string systemPath)
        {
            string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            string normal   = Path.GetFullPath(systemPath).Replace('\\', '/');
            if (normal.StartsWith(dataPath))
                return "Assets" + normal.Substring(dataPath.Length);
            return normal;
        }
    }
}
