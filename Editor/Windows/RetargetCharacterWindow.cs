using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Prepares a character + clip FBX pair for Mecanim humanoid retargeting.
    public class RetargetCharacterWindow : EditorWindow
    {
        [SerializeField] private string _characterPath;
        [SerializeField] private string _clipPath;
        private string _status;
        private bool _statusIsError;

        [MenuItem("Smear Generator/Utilities/Retarget Character")]
        static void Open() => OpenWith(null);

        // Open the window and optionally pre-fill the character field.
        public static void OpenWith(GameObject characterPrefill)
        {
            var window = GetWindow<RetargetCharacterWindow>("Retarget Character");
            window.minSize = new Vector2(400, 280);
            if (characterPrefill != null)
            {
                string path = AssetDatabase.GetAssetPath(characterPrefill);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    window._characterPath = path;
            }
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Retarget Character", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Use this when your animation clip comes from a different skeleton than your character. " +
                "Both files must be raw .fbx assets inside your project.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            _characterPath = FbxDropField.Draw("Character FBX", _characterPath);
            if (EditorGUI.EndChangeCheck()) _status = null;

            DrawFbxStatus(_characterPath);
            EditorGUILayout.Space(8);

            EditorGUI.BeginChangeCheck();
            _clipPath = FbxDropField.Draw("Clip FBX", _clipPath);
            if (EditorGUI.EndChangeCheck()) _status = null;

            DrawFbxStatus(_clipPath);
            EditorGUILayout.Space(10);

            string readyHint = BuildReadyHint();
            if (!string.IsNullOrEmpty(readyHint))
                EditorGUILayout.HelpBox(readyHint, MessageType.Info);

            bool ready = !string.IsNullOrEmpty(_characterPath) && !string.IsNullOrEmpty(_clipPath);
            EditorGUI.BeginDisabledGroup(!ready);
            if (GUILayout.Button(new GUIContent("Prepare Retarget Pair",
                "Sets both FBX files to humanoid rig mode so Unity's Mecanim system can retarget the clip onto the character."),
                GUILayout.Height(32)))
                Apply();
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_status, _statusIsError ? MessageType.Warning : MessageType.Info);
            }
        }

        // Show per-field rig status for a valid FBX path.
        static void DrawFbxStatus(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            string status = FbxAvatarSetupUtility.DescribeForUser(path);
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, MessageType.None);
        }

        // Explain what's still missing when the button is disabled.
        string BuildReadyHint()
        {
            bool charOk = !string.IsNullOrEmpty(_characterPath);
            bool clipOk  = !string.IsNullOrEmpty(_clipPath);
            if (charOk && clipOk) return null;
            if (!charOk && !clipOk) return null; // drop zone hints are enough
            if (!charOk) return "Drop a Character FBX to continue.";
            return "Drop a Clip FBX to continue.";
        }

        // Reimport both FBX assets so Mecanim can retarget the clip onto the character.
        void Apply()
        {
            var result = FbxAvatarSetupUtility.PrepareHumanoidRetargetPair(_characterPath, _clipPath);
            _status = result.Message;
            _statusIsError = !result.Success;
            if (result.Success)
                Debug.Log("[SmearGenerator] " + result.Message);
            else
                Debug.LogWarning("[SmearGenerator] " + result.Message);
        }
    }
}
