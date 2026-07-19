using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Draws smear effects and velocity config controls for the left panel.
    internal sealed class SmearConfigSection
    {
        private const float CompactButtonHeight = 24f;

        private SmearEffectsConfig _smearConfig;
        private VelocityConfig _velocityConfig;
        private bool _showSmearDetails;
        private bool _showVelocityDetails;

        public SmearEffectsConfig CurrentSmearConfig => _smearConfig;
        public VelocityConfig CurrentVelocityConfig => _velocityConfig;
        // set each Draw call: true when playbackSpeed or temporalSmoothingWindow changed (needs full rebake)
        public bool VelocityParamChanged { get; private set; }

        // Draws the smear-frame settings UI and reports changes through onChanged.
        public void Draw(
            SmearEffectsConfig smearConfig,
            VelocityConfig velocityConfig,
            ref UnityEditor.Editor smearEditor,
            ref UnityEditor.Editor velocityEditor,
            LayoutSection layout,
            System.Action onChanged)
        {
            _smearConfig = smearConfig;
            _velocityConfig = velocityConfig;
            VelocityParamChanged = false;

            bool narrow = EditorGUIUtility.currentViewWidth < 600f;
            bool changed = false;

            EditorGUI.BeginChangeCheck();
            _smearConfig = DrawObjectField(
                new GUIContent("Smear effects",
                    "ScriptableObject that controls smear type (elongated, multiples, motion lines) and per-type thresholds. Use the preset buttons below or assign your own asset."),
                _smearConfig);
            if (EditorGUI.EndChangeCheck())
            {
                smearEditor = null;
                changed = true;
            }

            EditorGUILayout.BeginHorizontal();
            changed |= PresetButton("Subtle", "SmearEffects_Subtle");
            changed |= PresetButton("Elong.", "SmearEffects_ElongatedOnly");
            changed |= PresetButton("Multi.", "SmearEffects_MultiplesOnly");
            changed |= PresetButton("Lines", "SmearEffects_MotionLinesOnly");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            changed |= PresetButton("Combined", "SmearEffects_AllCombined");
            changed |= PresetButton("Default", "SmearEffects_Default");
            changed |= PresetButton("Extreme", "SmearEffects_Extreme");
            EditorGUILayout.EndHorizontal();

            if (_smearConfig != null)
            {
                _showSmearDetails = EditorGUILayout.Foldout(_showSmearDetails, "Smear parameters");
                if (_showSmearDetails)
                {
                    UnityEditor.Editor.CreateCachedEditor(_smearConfig, null, ref smearEditor);
                    layout.DrawEmbeddedInspector(smearEditor, narrow);
                }
            }

            layout.DrawGroupGap();
            EditorGUI.BeginChangeCheck();
            _velocityConfig = DrawObjectField(
                new GUIContent("Velocity",
                    "Optional VelocityConfig asset. Tunes bone velocity extraction -- weight falloff, min/max thresholds, and which bones drive the smear intensity. Leave empty to use defaults."),
                _velocityConfig);
            if (EditorGUI.EndChangeCheck())
            {
                velocityEditor = null;
                changed = true;
            }

            if (_velocityConfig != null)
            {
                _showVelocityDetails = EditorGUILayout.Foldout(_showVelocityDetails, "Velocity parameters");
                if (_showVelocityDetails)
                {
                    var vso = new SerializedObject(_velocityConfig);
                    vso.Update();
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(vso.FindProperty("_playbackSpeed"));
                    EditorGUILayout.PropertyField(vso.FindProperty("_temporalSmoothingWindow"));
                    if (EditorGUI.EndChangeCheck())
                    {
                        vso.ApplyModifiedProperties();
                        changed = true;
                        VelocityParamChanged = true;
                    }
                    else
                    {
                        vso.ApplyModifiedProperties();
                    }
                }
            }

            if (changed)
                onChanged?.Invoke();
        }

        private T DrawObjectField<T>(GUIContent label, T value) where T : UnityEngine.Object
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.Space(2f);
            return (T)EditorGUILayout.ObjectField(value, typeof(T), false);
        }

        private bool PresetButton(string label, string assetName)
        {
            if (!GUILayout.Button(label, EditorStyles.miniButton, GUILayout.ExpandWidth(true), GUILayout.Height(CompactButtonHeight)))
                return false;
            string[] guids = AssetDatabase.FindAssets($"t:SmearEffectsConfig {assetName}");
            if (guids.Length == 0)
                return false;
            _smearConfig = AssetDatabase.LoadAssetAtPath<SmearEffectsConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return _smearConfig != null;
        }
    }
}
