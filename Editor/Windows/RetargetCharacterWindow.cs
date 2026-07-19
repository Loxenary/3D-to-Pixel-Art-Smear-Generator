using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Prepares a character + clip FBX pair for Mecanim humanoid retargeting.
    public class RetargetCharacterWindow : EditorWindow
    {
        [SerializeField] private GameObject _characterInput;
        [SerializeField] private GameObject _clipInput;
        private string _status;
        private bool _statusIsError;

        [MenuItem("Smear Generator/Utilities/Retarget Character")]
        static void Open() => OpenWith(null);

        // Open the window and optionally pre-fill the character field.
        public static void OpenWith(GameObject characterPrefill)
        {
            var window = GetWindow<RetargetCharacterWindow>("Retarget Character");
            window.minSize = new Vector2(380, 220);
            if (characterPrefill != null)
                window._characterInput = characterPrefill;
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Retarget Character", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Use this when your animation clip comes from a different skeleton than your character. Drop the raw .fbx files -- not prefabs -- into both fields below.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(10);

            string characterPath = DrawFbxField("Character FBX", ref _characterInput,
                "The character you want to animate.");
            EditorGUILayout.Space(6);
            string clipPath = DrawFbxField("Clip FBX", ref _clipInput,
                "The FBX that carries the animation clip. Can be from a different skeleton.");

            EditorGUILayout.Space(10);

            bool ready = !string.IsNullOrEmpty(characterPath) && !string.IsNullOrEmpty(clipPath);
            if (!ready)
            {
                string hint = BuildReadyHint(characterPath, clipPath);
                if (!string.IsNullOrEmpty(hint))
                    EditorGUILayout.HelpBox(hint, MessageType.Info);
            }

            EditorGUI.BeginDisabledGroup(!ready);
            if (GUILayout.Button(new GUIContent("Prepare Retarget Pair",
                "Sets both FBX files to humanoid rig mode so Unity's Mecanim system can retarget the clip onto the character."),
                GUILayout.Height(32)))
                Apply(characterPath, clipPath);
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_status, _statusIsError ? MessageType.Warning : MessageType.Info);
            }
        }

        // Draw an FBX object field with inline per-field status. Returns the resolved asset path or null.
        string DrawFbxField(string label, ref GameObject field, string tooltip)
        {
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), EditorStyles.miniLabel);
            field = (GameObject)EditorGUILayout.ObjectField(field, typeof(GameObject), false);

            if (field == null)
                return null;

            string path = FbxAvatarSetupUtility.ResolveFbxAssetPath(field);
            if (string.IsNullOrEmpty(path))
            {
                EditorGUILayout.HelpBox($"\"{field.name}\" is not a raw .fbx file. Select the .fbx from your Project panel, not a prefab or generated asset.", MessageType.Warning);
                return null;
            }

            string status = FbxAvatarSetupUtility.DescribeForUser(path);
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, MessageType.None);

            return path;
        }

        // Explain what's still missing when the button is disabled.
        static string BuildReadyHint(string characterPath, string clipPath)
        {
            bool charOk = !string.IsNullOrEmpty(characterPath);
            bool clipOk  = !string.IsNullOrEmpty(clipPath);
            if (!charOk && !clipOk) return null; // both empty -- field hints are enough
            if (!charOk) return "Drop a raw .fbx into Character FBX to continue.";
            if (!clipOk) return "Drop a raw .fbx into Clip FBX to continue.";
            return null;
        }

        // Reimport both FBX assets so Mecanim can retarget the clip onto the character.
        void Apply(string characterPath, string clipPath)
        {
            var result = FbxAvatarSetupUtility.PrepareHumanoidRetargetPair(characterPath, clipPath);
            _status = result.Message;
            _statusIsError = !result.Success;
            if (result.Success)
                Debug.Log("[SmearGenerator] " + result.Message);
            else
                Debug.LogWarning("[SmearGenerator] " + result.Message);
        }
    }
}
