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
            EditorGUILayout.LabelField("Reimports both FBXs as humanoid so Mecanim can retarget the animation clip onto the character.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Character FBX", EditorStyles.miniLabel);
            _characterInput = (GameObject)EditorGUILayout.ObjectField(_characterInput, typeof(GameObject), false);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Clip FBX", EditorStyles.miniLabel);
            _clipInput = (GameObject)EditorGUILayout.ObjectField(_clipInput, typeof(GameObject), false);

            EditorGUILayout.Space(8);
            string characterPath = FbxAvatarSetupUtility.ResolveFbxAssetPath(_characterInput);
            string clipPath = FbxAvatarSetupUtility.ResolveFbxAssetPath(_clipInput);
            string desc = $"Character: {FbxAvatarSetupUtility.Describe(characterPath)}\nClip: {FbxAvatarSetupUtility.Describe(clipPath)}";
            EditorGUILayout.HelpBox(desc, MessageType.None);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(characterPath) || string.IsNullOrEmpty(clipPath));
            if (GUILayout.Button("Prepare Retarget Pair", GUILayout.Height(30)))
                Apply(characterPath, clipPath);
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        // Reimport both FBX assets so Mecanim can retarget the clip onto the character.
        void Apply(string characterPath, string clipPath)
        {
            var result = FbxAvatarSetupUtility.PrepareHumanoidRetargetPair(characterPath, clipPath);
            _status = result.Message;
            if (result.Success)
                Debug.Log("[SmearGenerator] " + result.Message);
            else
                Debug.LogWarning("[SmearGenerator] " + result.Message);
        }
    }
}
