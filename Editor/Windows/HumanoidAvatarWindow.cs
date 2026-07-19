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
            EditorGUILayout.LabelField(
                "Use this when the Smear Generator says your character has no humanoid avatar. " +
                "This reimports the .fbx so Unity maps the skeleton to a standard humanoid rig.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Character FBX", EditorStyles.miniLabel);
            _fbxInput = (GameObject)EditorGUILayout.ObjectField(_fbxInput, typeof(GameObject), false);
            EditorGUILayout.Space(4);

            string fbxPath = DrawFieldStatus();

            EditorGUILayout.Space(8);
            bool alreadyDone = !string.IsNullOrEmpty(fbxPath) && IsAlreadyHumanoid(fbxPath);
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(fbxPath));
            string btnLabel = alreadyDone ? "Re-create Humanoid Avatar" : "Create Humanoid Avatar";
            if (GUILayout.Button(new GUIContent(btnLabel,
                "Reimports the FBX with humanoid rig settings and generates an avatar from the skeleton."),
                GUILayout.Height(32)))
                Apply(fbxPath);
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_status, _statusIsError ? MessageType.Warning : MessageType.Info);
            }
        }

        // Draw per-field status and return the resolved FBX path, or null if invalid.
        string DrawFieldStatus()
        {
            if (_fbxInput == null)
            {
                EditorGUILayout.HelpBox(
                    "Drag the character's .fbx file here from the Project panel. " +
                    "Look for files with a 3D model icon -- not a prefab (blue cube) or generated asset.",
                    MessageType.Info);
                return null;
            }

            string assetPath = AssetDatabase.GetAssetPath(_fbxInput);
            bool isFbxFile = !string.IsNullOrEmpty(assetPath) &&
                assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

            if (!isFbxFile)
            {
                string hint = assetPath != null && assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)
                    ? $"\"{_fbxInput.name}\" is a Prefab, not an .fbx source file. " +
                      "In the Project panel, find the original .fbx it was created from -- " +
                      "it will have the same or a similar name and show a 3D model icon instead of a blue cube."
                    : $"\"{_fbxInput.name}\" is not a raw .fbx file. " +
                      "Drop the .fbx directly from the Project panel. " +
                      "If you only see prefabs, look for the source file with an .fbx extension.";
                EditorGUILayout.HelpBox(hint, MessageType.Warning);
                return null;
            }

            if (!AssetDatabase.IsMainAsset(_fbxInput))
            {
                EditorGUILayout.HelpBox(
                    "This is a child object inside the FBX, not the file itself. " +
                    "In the Project panel, click the .fbx file directly -- do not expand it and drag a child.",
                    MessageType.Warning);
                return null;
            }

            // Valid FBX -- show current rig state.
            string status = FbxAvatarSetupUtility.DescribeForUser(assetPath);
            if (!string.IsNullOrEmpty(status))
            {
                bool done = IsAlreadyHumanoid(assetPath);
                MessageType msgType = done ? MessageType.Info : MessageType.None;
                string suffix = done
                    ? " Go back to Smear Generator -- this character should accept humanoid clips now."
                    : "";
                EditorGUILayout.HelpBox(status + suffix, msgType);
            }

            return assetPath;
        }

        // Returns true when the FBX already has a valid humanoid avatar -- used to change button label.
        static bool IsAlreadyHumanoid(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return false;
            return importer.animationType == ModelImporterAnimationType.Human &&
                   FbxAvatarSetupUtility.HasValidHumanoidAvatarPublic(assetPath);
        }

        // Reimport the FBX as a humanoid character that creates its own avatar.
        void Apply(string fbxPath)
        {
            var result = FbxAvatarSetupUtility.MakeHumanoidFromModel(fbxPath);
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
