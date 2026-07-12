using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    /// <summary>Standalone editor window for switching FBX rigs to humanoid/avatar mode.</summary>
    public class FbxAvatarSetupWindow : EditorWindow
    {
        [SerializeField] private GameObject _fbxInput;
        [SerializeField] private GameObject _avatarSource;
        [SerializeField] private GameObject _characterInput;
        [SerializeField] private GameObject _clipInput;
        private string _status;

        [MenuItem("Smear Generator/FBX Avatar Setup")]
        static void Open()
        {
            var window = GetWindow<FbxAvatarSetupWindow>("FBX Avatar Setup");
            window.minSize = new Vector2(380, 220);
        }

        // Draw the simple import helper UI.
        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("FBX Avatar Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Single Fbx Input", EditorStyles.miniLabel);
            _fbxInput = (GameObject)EditorGUILayout.ObjectField(_fbxInput, typeof(GameObject), false);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Avatar Source", EditorStyles.miniLabel);
            _avatarSource = (GameObject)EditorGUILayout.ObjectField(_avatarSource, typeof(GameObject), false);

            EditorGUILayout.Space(10);
            string fbxPath = FbxAvatarSetupUtility.ResolveFbxAssetPath(_fbxInput);
            EditorGUILayout.HelpBox(FbxAvatarSetupUtility.Describe(fbxPath), MessageType.None);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(fbxPath));
            if (GUILayout.Button("Create Humanoid Avatar", GUILayout.Height(30)))
                ApplyCreateFromModel(fbxPath);

            EditorGUI.BeginDisabledGroup(_avatarSource == null);
            if (GUILayout.Button("Copy Avatar From Source", GUILayout.Height(30)))
                ApplyCopyFromSource(fbxPath);
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Set Back To Generic", GUILayout.Height(24)))
                ApplyGeneric(fbxPath);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Retarget Pair Setup", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Character Fbx", EditorStyles.miniLabel);
            _characterInput = (GameObject)EditorGUILayout.ObjectField(_characterInput, typeof(GameObject), false);
            EditorGUILayout.LabelField("Clip Fbx", EditorStyles.miniLabel);
            _clipInput = (GameObject)EditorGUILayout.ObjectField(_clipInput, typeof(GameObject), false);

            string characterPath = FbxAvatarSetupUtility.ResolveFbxAssetPath(_characterInput);
            string clipPath = FbxAvatarSetupUtility.ResolveFbxAssetPath(_clipInput);
            string pairDescription = $"Character: {FbxAvatarSetupUtility.Describe(characterPath)}\nClip: {FbxAvatarSetupUtility.Describe(clipPath)}";
            EditorGUILayout.HelpBox(pairDescription, MessageType.None);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(characterPath) || string.IsNullOrEmpty(clipPath));
            if (GUILayout.Button("Prepare Retarget Pair", GUILayout.Height(30)))
                ApplyPrepareRetargetPair(characterPath, clipPath);
            EditorGUI.EndDisabledGroup();

            if (string.IsNullOrEmpty(fbxPath))
                EditorGUILayout.HelpBox("Drop a raw .fbx asset into Fbx Input.", MessageType.Info);

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        // Reimport the selected FBX as a humanoid character model.
        void ApplyCreateFromModel(string fbxPath)
        {
            var result = FbxAvatarSetupUtility.MakeHumanoidFromModel(fbxPath);
            ShowResult(result);
        }

        // Reimport the selected FBX as a humanoid asset that copies another avatar.
        void ApplyCopyFromSource(string fbxPath)
        {
            var result = FbxAvatarSetupUtility.CopyHumanoidAvatar(fbxPath, _avatarSource);
            ShowResult(result);
        }

        // Reimport the selected FBX as a plain generic rig again.
        void ApplyGeneric(string fbxPath)
        {
            var result = FbxAvatarSetupUtility.MakeGeneric(fbxPath);
            ShowResult(result);
        }

        // Reimport both FBX assets so Mecanim can retarget the clip onto the character.
        void ApplyPrepareRetargetPair(string characterPath, string clipPath)
        {
            var result = FbxAvatarSetupUtility.PrepareHumanoidRetargetPair(characterPath, clipPath);
            ShowResult(result);
        }

        // Mirror the result into the UI and console.
        void ShowResult(FbxAvatarSetupUtility.SetupResult result)
        {
            _status = result.Message;
            if (result.Success)
                Debug.Log("[SmearGenerator] " + result.Message);
            else
                Debug.LogWarning("[SmearGenerator] " + result.Message);
        }
    }
}
