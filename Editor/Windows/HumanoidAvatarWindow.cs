using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Sets a single FBX to humanoid rig mode and creates its avatar.
    public class HumanoidAvatarWindow : EditorWindow
    {
        [SerializeField] private string _fbxPath;
        private string _status;
        private bool _statusIsError;

        [MenuItem("Smear Generator/Utilities/Humanoid Avatar Setup")]
        static void Open() => OpenWith(null);

        // Open the window and optionally pre-fill the FBX field from a character prefab.
        public static void OpenWith(GameObject characterPrefill)
        {
            var window = GetWindow<HumanoidAvatarWindow>("Humanoid Avatar Setup");
            window.minSize = new Vector2(400, 220);
            if (characterPrefill != null)
            {
                string path = AssetDatabase.GetAssetPath(characterPrefill);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    window._fbxPath = path;
            }
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Humanoid Avatar Setup", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Use this when the Smear Generator says your character has no humanoid avatar. " +
                "This reimports the .fbx so Unity maps the skeleton to a standard humanoid rig.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            _fbxPath = FbxDropField.Draw("Character FBX", _fbxPath);
            if (EditorGUI.EndChangeCheck())
                _status = null; // clear stale result when input changes

            EditorGUILayout.Space(6);
            DrawFieldStatus();

            EditorGUILayout.Space(8);
            bool alreadyDone = !string.IsNullOrEmpty(_fbxPath) && IsAlreadyHumanoid(_fbxPath);
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_fbxPath));
            string btnLabel = alreadyDone ? "Re-create Humanoid Avatar" : "Create Humanoid Avatar";
            if (GUILayout.Button(new GUIContent(btnLabel,
                "Reimports the FBX with humanoid rig settings and generates an avatar from the skeleton."),
                GUILayout.Height(32)))
                Apply();
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_status, _statusIsError ? MessageType.Warning : MessageType.Info);
            }
        }

        // Show per-field status for the current FBX path.
        void DrawFieldStatus()
        {
            if (string.IsNullOrEmpty(_fbxPath))
                return;

            string userStatus = FbxAvatarSetupUtility.DescribeForUser(_fbxPath);
            if (string.IsNullOrEmpty(userStatus))
                return;

            bool done = IsAlreadyHumanoid(_fbxPath);
            string suffix = done ? " Go back to Smear Generator -- this character should accept humanoid clips now." : "";
            EditorGUILayout.HelpBox(userStatus + suffix, done ? MessageType.Info : MessageType.None);
        }

        // Returns true when the FBX already has a valid humanoid avatar.
        static bool IsAlreadyHumanoid(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return false;
            return importer.animationType == ModelImporterAnimationType.Human &&
                   FbxAvatarSetupUtility.HasValidHumanoidAvatarPublic(assetPath);
        }

        // Reimport the FBX as a humanoid character that creates its own avatar.
        void Apply()
        {
            var result = FbxAvatarSetupUtility.MakeHumanoidFromModel(_fbxPath);
            _status = result.Success
                ? result.Message + " Go back to Smear Generator and try your clip again."
                : result.Message;
            _statusIsError = !result.Success;
            if (result.Success)
                Debug.Log("[SmearGenerator] " + result.Message);
            else
                Debug.LogWarning("[SmearGenerator] " + result.Message);
        }
    }
}
