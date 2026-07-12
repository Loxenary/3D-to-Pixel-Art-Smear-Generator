using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    [CustomEditor(typeof(SmearOutputConfig))]
    public class SmearOutputConfigEditor : UnityEditor.Editor
    {
        SerializedProperty _outputDirectory;

        void OnEnable()
        {
            _outputDirectory = serializedObject.FindProperty("_outputDirectory");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool narrow = !EditorGUIUtility.wideMode;

            EditorGUILayout.Space(6);
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

            serializedObject.ApplyModifiedProperties();
        }

        void BrowseOutputDirectory()
        {
            string systemStart = ToSystemPath(_outputDirectory.stringValue);
            if (!Directory.Exists(systemStart))
                systemStart = Application.dataPath;

            string picked = EditorUtility.OpenFolderPanel("Select output folder", systemStart, "");
            if (!string.IsNullOrEmpty(picked))
                _outputDirectory.stringValue = ToAssetPath(picked);

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
    }
}
