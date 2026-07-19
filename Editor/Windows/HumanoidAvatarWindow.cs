using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Sets a single FBX to humanoid rig mode and creates its avatar.
    public class HumanoidAvatarWindow : EditorWindow
    {
        [SerializeField] private GameObject _fbxInput;
        private string _status;
        private bool _statusIsError;

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
            EditorGUILayout.LabelField("Sets an FBX to humanoid rig mode so Unity generates an avatar from its skeleton. Required before the Smear Generator can use humanoid clips on this character.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("FBX Input", EditorStyles.miniLabel);
            _fbxInput = (GameObject)EditorGUILayout.ObjectField(_fbxInput, typeof(GameObject), false);

            string fbxPath = null;
            if (_fbxInput != null)
            {
                fbxPath = FbxAvatarSetupUtility.ResolveFbxAssetPath(_fbxInput);
                if (string.IsNullOrEmpty(fbxPath))
                    EditorGUILayout.HelpBox($"\"{_fbxInput.name}\" is not a raw .fbx file. Select the .fbx from your Project panel, not a prefab or generated asset.", MessageType.Warning);
                else
                {
                    string fieldStatus = FbxAvatarSetupUtility.DescribeForUser(fbxPath);
                    if (!string.IsNullOrEmpty(fieldStatus))
                        EditorGUILayout.HelpBox(fieldStatus, MessageType.None);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Drop a raw .fbx from your Project panel.", MessageType.Info);
            }

            EditorGUILayout.Space(8);
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(fbxPath));
            if (GUILayout.Button(new GUIContent("Create Humanoid Avatar",
                "Reimports the FBX with humanoid rig settings and generates an avatar from its skeleton."),
                GUILayout.Height(32)))
                Apply(fbxPath);
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_status, _statusIsError ? MessageType.Warning : MessageType.Info);
            }
        }

        // Reimport the FBX as a humanoid character that creates its own avatar.
        void Apply(string fbxPath)
        {
            var result = FbxAvatarSetupUtility.MakeHumanoidFromModel(fbxPath);
            _status = result.Message;
            _statusIsError = !result.Success;
            if (result.Success)
                Debug.Log("[SmearGenerator] " + result.Message);
            else
                Debug.LogWarning("[SmearGenerator] " + result.Message);
        }
    }
}
