using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Draws pixelization/output parameter UI.
    internal sealed class PixelizationSection
    {
        private bool _showOutputDetails;
        private bool _showPostProcessDetails;

        // Draws pixel-art conversion controls and output naming controls, but does not run the pipeline.
        public void Draw(
            OutputConfig outputConfig,
            ref UnityEditor.Editor outputEditor,
            PostProcessConfig postProcessConfig,
            ref UnityEditor.Editor postProcessEditor,
            ref bool reusePalette,
            LayoutSection layout,
            ref bool showPivotLine,
            System.Action onChanged)
        {
            bool changed = false;

            // -- output config section (resolution, outline, pivot, etc.)
            if (outputConfig == null)
            {
                EditorGUILayout.HelpBox("Pixel parameters unavailable -- default config asset is missing.", MessageType.Warning);
            }
            else
            {
                _showOutputDetails = EditorGUILayout.Foldout(_showOutputDetails, "Pixel parameters");
                if (_showOutputDetails)
                {
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
                    changed |= EditorGUI.EndChangeCheck();

                    so.ApplyModifiedProperties();
                }
            }

            // -- post-process config section (palette LUT, flicker, EM iterations, etc.)
            layout.DrawGroupGap();
            _showPostProcessDetails = EditorGUILayout.Foldout(_showPostProcessDetails, "Post-process / palette");
            if (_showPostProcessDetails)
            {
                EditorGUI.BeginChangeCheck();
                var prev = postProcessConfig;
                postProcessConfig = (PostProcessConfig)EditorGUILayout.ObjectField(
                    new GUIContent("Post process config",
                        "Controls palette quantization, flicker suppression, and content-adaptive downscale. Leave empty to use built-in defaults."),
                    postProcessConfig, typeof(PostProcessConfig), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (postProcessConfig != prev)
                        postProcessEditor = null;
                    changed = true;
                }

                if (postProcessConfig != null)
                {
                    var so = new SerializedObject(postProcessConfig);
                    so.Update();

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(so.FindProperty("_paletteLUT"),
                        new GUIContent("Palette LUT",
                            "Lock to a fixed artist palette. Each output pixel snaps to the nearest color here. Leave empty to let IOKM generate the palette automatically."));
                    EditorGUILayout.PropertyField(so.FindProperty("_paletteSize"),
                        new GUIContent("Auto palette size",
                            "Number of colors IOKM generates when no fixed palette is set. Ignored if Palette LUT is non-empty."));
                    EditorGUILayout.PropertyField(so.FindProperty("_emIterations"),
                        new GUIContent("Edge refine passes",
                            "How many times the downscaler sharpens edge kernels before sampling. Higher = crisper outlines, slower bake. 5 is fine for most sprites."));
                    EditorGUILayout.PropertyField(so.FindProperty("_flickerSuppressOnDistance"),
                        new GUIContent("Flicker suppress",
                            "CIELAB distance a pixel must change before it updates between frames. Higher = more suppression, fewer updates."));
                    changed |= EditorGUI.EndChangeCheck();
                    so.ApplyModifiedProperties();
                }

                layout.DrawGroupGap();
                EditorGUI.BeginChangeCheck();
                reusePalette = EditorGUILayout.ToggleLeft(
                    new GUIContent("Reuse palette across frames",
                        "Build the IOKM palette once from a seed frame, then LUT-snap all other frames. Faster and more color-consistent. Disable to run full quantization per frame."),
                    reusePalette);
                changed |= EditorGUI.EndChangeCheck();
            }

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
