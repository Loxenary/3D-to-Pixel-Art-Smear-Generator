using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Draws pixelization/output parameter UI.
    internal sealed class PixelizationSection
    {
        private bool _showOutputDetails;

        // Draws pixel-art conversion controls and output naming controls, but does not run the pipeline.
        public void Draw(
            OutputConfig outputConfig,
            ref UnityEditor.Editor outputEditor,
            LayoutSection layout,
            ref bool showPivotLine,
            System.Action onChanged)
        {
            if (outputConfig == null)
            {
                EditorGUILayout.HelpBox("Pixel parameters are unavailable because the default config asset is missing.", MessageType.Warning);
                return;
            }

            _showOutputDetails = EditorGUILayout.Foldout(_showOutputDetails, "Pixel parameters");
            if (!_showOutputDetails)
                return;

            bool narrow = EditorGUIUtility.currentViewWidth < 600f;
            var so = new SerializedObject(outputConfig);
            so.Update();

            var enableOutline = so.FindProperty("_enableOutline");
            var pivotNormalized = so.FindProperty("_pivotNormalized");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("_outputResolution"));
            EditorGUILayout.PropertyField(so.FindProperty("_captureResolution"));
            EditorGUILayout.PropertyField(so.FindProperty("_paletteSize"));
            EditorGUILayout.PropertyField(enableOutline);
            if (enableOutline.boolValue)
                EditorGUILayout.PropertyField(so.FindProperty("_outlineColor"));
            EditorGUILayout.PropertyField(so.FindProperty("_pixelsPerUnit"));
            EditorGUILayout.PropertyField(so.FindProperty("_loopPlayback"));
            DrawPivotNormalizedProperty(pivotNormalized, narrow);
            showPivotLine = EditorGUILayout.ToggleLeft("Show Pivot Line", showPivotLine);
            EditorGUILayout.PropertyField(so.FindProperty("_saveHighResToDisk"));
            bool changed = EditorGUI.EndChangeCheck();

            so.ApplyModifiedProperties();
            if (changed)
                onChanged?.Invoke();
        }

        // Draws pivot as stacked X/Y rows in narrow mode so the left panel keeps wrapping cleanly.
        private void DrawPivotNormalizedProperty(SerializedProperty pivotNormalized, bool narrow)
        {
            if (!narrow)
            {
                EditorGUILayout.PropertyField(pivotNormalized);
                return;
            }

            EditorGUILayout.LabelField("Pivot Normalized");
            EditorGUI.indentLevel++;
            var value = pivotNormalized.vector2Value;
            value.x = EditorGUILayout.FloatField("X", value.x);
            value.y = EditorGUILayout.FloatField("Y", value.y);
            pivotNormalized.vector2Value = value;
            EditorGUI.indentLevel--;
        }
    }
}
