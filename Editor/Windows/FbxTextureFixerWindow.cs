using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    /// <summary>Standalone editor window for repairing FBX texture folders.</summary>
    public class FbxTextureFixerWindow : EditorWindow
    {
        [SerializeField] private GameObject _fbxInput;
        [SerializeField] private string _folderName;
        [SerializeField] private string _folderPath;
        private string _status;

        [MenuItem("Smear Generator/FBX Texture Fixer")]
        static void Open()
        {
            var window = GetWindow<FbxTextureFixerWindow>("FBX Texture Fixer");
            window.minSize = new Vector2(360, 290);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("FBX Texture Fixer", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("Fbx Input", EditorStyles.miniLabel);
            _fbxInput = (GameObject)EditorGUILayout.ObjectField(_fbxInput, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
                ResetDefaults();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Extracted textures will be saved into a folder next to the FBX. You can rename the folder or change its path before running Fix Texture.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Texture Folder Name", EditorStyles.miniLabel);
            _folderName = EditorGUILayout.TextField(_folderName);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Texture Folder Path", EditorStyles.miniLabel);
            _folderPath = EditorGUILayout.TextField(_folderPath);

            EditorGUILayout.Space(10);
            string fbxPath = FbxTextureFixer.ResolveFbxAssetPath(_fbxInput);
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(fbxPath));
            if (GUILayout.Button("Fix Texture", GUILayout.Height(32)))
                FixTexture(fbxPath);
            EditorGUI.EndDisabledGroup();

            if (string.IsNullOrEmpty(fbxPath))
                EditorGUILayout.HelpBox("Drop a raw .fbx asset into Fbx Input.", MessageType.Info);

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        // Reset folder fields from the selected FBX asset.
        void ResetDefaults()
        {
            string fbxPath = FbxTextureFixer.ResolveFbxAssetPath(_fbxInput);
            if (string.IsNullOrEmpty(fbxPath))
            {
                _folderName = string.Empty;
                _folderPath = string.Empty;
                return;
            }

            _folderName = FbxTextureFixer.GuessFolderName(fbxPath);
            _folderPath = FbxTextureFixer.GuessFolderPath(fbxPath);
        }

        // Run the texture fixer and show the result.
        void FixTexture(string fbxPath)
        {
            var result = FbxTextureFixer.Fix(fbxPath, _folderName, _folderPath);
            _status = result.Message;
            if (result.Success)
                Debug.Log("[SmearGenerator] " + result.Message);
            else
                Debug.LogWarning("[SmearGenerator] " + result.Message);
        }
    }
}
