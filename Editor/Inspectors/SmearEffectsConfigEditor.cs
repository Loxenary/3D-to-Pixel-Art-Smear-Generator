using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    [CustomEditor(typeof(SmearEffectsConfig))]
    public class SmearEffectsConfigEditor : UnityEditor.Editor
    {
        SerializedProperty _smearStrength;
        SerializedProperty _multipleSpeedThreshold;
        SerializedProperty _enableElongated;
        SerializedProperty _elongationMax;
        SerializedProperty _elongatedUseNoise;
        SerializedProperty _noiseScale;
        SerializedProperty _enableMultiples;
        SerializedProperty _pastCopies;
        SerializedProperty _futureCopies;
        SerializedProperty _overlapCount;
        SerializedProperty _pastDisplacement;
        SerializedProperty _futureDisplacement;
        SerializedProperty _pastOpacityFactor;
        SerializedProperty _futureOpacityFactor;
        SerializedProperty _enableMotionLines;
        SerializedProperty _motionLineSpeedThreshold;
        SerializedProperty _motionLineSeeds;
        SerializedProperty _motionLineMaxLength;
        SerializedProperty _motionLineThickness;
        SerializedProperty _motionLineColor;

        void OnEnable()
        {
            _smearStrength = serializedObject.FindProperty("_smearStrength");
            _multipleSpeedThreshold = serializedObject.FindProperty("_speedThreshold");
            _enableElongated = serializedObject.FindProperty("_enableElongated");
            _elongationMax = serializedObject.FindProperty("_elongationMax");
            _elongatedUseNoise = serializedObject.FindProperty("_elongatedUseNoise");
            _noiseScale = serializedObject.FindProperty("_noiseScale");
            _enableMultiples = serializedObject.FindProperty("_enableMultiples");
            _pastCopies = serializedObject.FindProperty("_pastCopies");
            _futureCopies = serializedObject.FindProperty("_futureCopies");
            _overlapCount = serializedObject.FindProperty("_overlapCount");
            _pastDisplacement = serializedObject.FindProperty("_pastDisplacement");
            _futureDisplacement = serializedObject.FindProperty("_futureDisplacement");
            _pastOpacityFactor = serializedObject.FindProperty("_pastOpacityFactor");
            _futureOpacityFactor = serializedObject.FindProperty("_futureOpacityFactor");
            _enableMotionLines = serializedObject.FindProperty("_enableMotionLines");
            _motionLineSpeedThreshold = serializedObject.FindProperty("_motionLineSpeedThreshold");
            _motionLineSeeds = serializedObject.FindProperty("_motionLineSeeds");
            _motionLineMaxLength = serializedObject.FindProperty("_motionLineMaxLength");
            _motionLineThickness = serializedObject.FindProperty("_motionLineThickness");
            _motionLineColor = serializedObject.FindProperty("_motionLineColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawGlobal();
            EditorGUILayout.Space(6);
            DrawElongated();
            EditorGUILayout.Space(6);
            DrawMultiple();
            EditorGUILayout.Space(6);
            DrawMotionLines();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawGlobal()
        {
            EditorGUILayout.LabelField("Global Strength", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_smearStrength, new GUIContent("Smear Strength"));
        }

        void DrawElongated()
        {
            EditorGUILayout.LabelField("Elongated In-Betweens", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enableElongated, new GUIContent("Enable Elongated"));
            if (!_enableElongated.boolValue) return;

            EditorGUILayout.PropertyField(_elongationMax, new GUIContent("Elongation Max"));
            EditorGUILayout.PropertyField(_elongatedUseNoise, new GUIContent("Elongated Use Noise"));
            if (_elongatedUseNoise.boolValue)
                EditorGUILayout.PropertyField(_noiseScale, new GUIContent("Noise Scale"));
        }

        void DrawMultiple()
        {
            EditorGUILayout.LabelField("Multiple In-Betweens", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enableMultiples, new GUIContent("Enable Multiples"));
            if (!_enableMultiples.boolValue) return;

            EditorGUILayout.PropertyField(_multipleSpeedThreshold, new GUIContent("Speed Threshold"));
            EditorGUILayout.PropertyField(_pastCopies, new GUIContent("Past Copies"));
            EditorGUILayout.PropertyField(_futureCopies, new GUIContent("Future Copies"));
            EditorGUILayout.PropertyField(_overlapCount, new GUIContent("Overlap Count"));
            EditorGUILayout.PropertyField(_pastDisplacement, new GUIContent("Past Displacement"));
            EditorGUILayout.PropertyField(_futureDisplacement, new GUIContent("Future Displacement"));
            EditorGUILayout.PropertyField(_pastOpacityFactor, new GUIContent("Past Opacity Factor"));
            EditorGUILayout.PropertyField(_futureOpacityFactor, new GUIContent("Future Opacity Factor"));
        }

        void DrawMotionLines()
        {
            EditorGUILayout.LabelField("Motion Lines", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enableMotionLines, new GUIContent("Enable Motion Lines"));
            if (!_enableMotionLines.boolValue) return;

            EditorGUILayout.PropertyField(_motionLineSpeedThreshold, new GUIContent("Speed Threshold"));
            EditorGUILayout.PropertyField(_motionLineSeeds, new GUIContent("Motion Line Seeds"));
            EditorGUILayout.PropertyField(_motionLineMaxLength, new GUIContent("Motion Line Max Length"));
            EditorGUILayout.PropertyField(_motionLineThickness, new GUIContent("Motion Line Thickness"));
            EditorGUILayout.PropertyField(_motionLineColor, new GUIContent("Motion Line Color"));
        }
    }
}
