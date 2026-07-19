using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Sets a single FBX to humanoid rig mode and creates its avatar.
    public class HumanoidAvatarWindow : EditorWindow
    {
        [SerializeField] private GameObject _fbxInput;
        private string _status;

        [MenuItem("Smear Generator/Utilities/Humanoid Avatar Setup")]
        static void Open() => OpenWith(null);

        // Open the window and optionally pre-fill the FBX field.
        public static void OpenWith(GameObject prefill)
        {
            var window = GetWindow<HumanoidAvatarWindow>("Humanoid Avatar Setup");
            window.minSize = new Vector2(380, 170);
            if (prefill != null)
                window._fbxInput = prefill;
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Humanoid Avatar Setup", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Reimports the FBX so Unity generates a humanoid avatar from the skeleton.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("FBX Input", EditorStyles.miniLabel);
            _fbxInput = (GameObject)EditorGUILayout.ObjectField(_fbxInput, typeof(GameObject), false);

            EditorGUILayout.Space(8);
            string fbxPath = FbxAvatarSetupUtility.ResolveFbxAssetPath(_fbxInput);
            EditorGUILayout.HelpBox(FbxAvatarSetupUtility.Describe(fbxPath), MessageType.None);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(fbxPath));
            if (GUILayout.Button("Create Humanoid Avatar", GUILayout.Height(30)))
                Apply(fbxPath);
            EditorGUI.EndDisabledGroup();

            if (string.IsNullOrEmpty(fbxPath))
                EditorGUILayout.HelpBox("Drop a raw .fbx asset into FBX Input.", MessageType.Info);

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        // Reimport the FBX as a humanoid character that creates its own avatar.
        void Apply(string fbxPath)
        {
            var result = FbxAvatarSetupUtility.MakeHumanoidFromModel(fbxPath);
            _status = result.Message;
            if (result.Success)
                Debug.Log("[SmearGenerator] " + result.Message);
            else
                Debug.LogWarning("[SmearGenerator] " + result.Message);
        }
    }
}
