using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Draws pixelization and post-process config controls for the left panel.
    internal sealed class PixelizationSection
    {
        private OutputConfig _outputConfig;
        private PostProcessConfig _postProcessConfig;
        private bool _showOutputDetails;
        private bool _showPostProcessDetails;

        public OutputConfig CurrentOutputConfig       => _outputConfig;
        public PostProcessConfig CurrentPostProcessConfig => _postProcessConfig;

        // Draws the pixelization and post-process config UI and reports changes through onChanged.
        public void Draw(
            OutputConfig outputConfig,
            PostProcessConfig postProcessConfig,
            ref bool reusePalette,
            LayoutSection layout,
            ref bool showPivotLine,
            System.Action onChanged)
        {
            _outputConfig       = outputConfig;
            _postProcessConfig  = postProcessConfig;

            bool changed = false;
            bool narrow  = EditorGUIUtility.currentViewWidth < 600f;

            // -- pixelization config
            EditorGUI.BeginChangeCheck();
            _outputConfig = DrawObjectField(
                new GUIContent("Pixelization",
                    "Controls output resolution, capture resolution, palette size, pivot, outline, and sprite sheet settings."),
                _outputConfig);
            if (EditorGUI.EndChangeCheck())
                changed = true;

            if (_outputConfig != null)
            {
                _showOutputDetails = EditorGUILayout.Foldout(_showOutputDetails, "Pixel parameters");
                if (_showOutputDetails)
                {
                    var so = new SerializedObject(_outputConfig);
                    so.Update();
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.PropertyField(so.FindProperty("_outputResolution"));
                    EditorGUILayout.PropertyField(so.FindProperty("_captureResolution"));
                    EditorGUILayout.PropertyField(so.FindProperty("_paletteSize"));

                    var enableOutline = so.FindProperty("_enableOutline");
                    EditorGUILayout.PropertyField(enableOutline);
                    if (enableOutline.boolValue)
                        EditorGUILayout.PropertyField(so.FindProperty("_outlineColor"));

                    EditorGUILayout.PropertyField(so.FindProperty("_pixelsPerUnit"));
                    EditorGUILayout.PropertyField(so.FindProperty("_loopPlayback"));
                    DrawPivotNormalizedProperty(so.FindProperty("_pivotNormalized"), narrow);
                    showPivotLine = EditorGUILayout.ToggleLeft("Show Pivot Line", showPivotLine);
                    EditorGUILayout.PropertyField(so.FindProperty("_saveHighResToDisk"));

                    changed |= EditorGUI.EndChangeCheck();
                    so.ApplyModifiedProperties();
                }
            }

            layout.DrawGroupGap();

            // -- post-process config
            EditorGUI.BeginChangeCheck();
            _postProcessConfig = DrawObjectField(
                new GUIContent("Post-process / palette",
                    "Fine-tune how colors and edges look in the final pixel art."),
                _postProcessConfig);
            if (EditorGUI.EndChangeCheck())
                changed = true;

            if (_postProcessConfig != null)
            {
                _showPostProcessDetails = EditorGUILayout.Foldout(_showPostProcessDetails, "Post-process parameters");
                if (_showPostProcessDetails)
                {
                    var so = new SerializedObject(_postProcessConfig);
                    so.Update();
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.PropertyField(so.FindProperty("_paletteSize"),
                        new GUIContent("Color Count",
                            "How many colors the output can use when no fixed palette is set. Lower = more limited and stylized. 8 is typical for retro pixel art, 16-32 for richer sprites."));
                    EditorGUILayout.PropertyField(so.FindProperty("_emIterations"),
                        new GUIContent("Edge Sharpness",
                            "How many passes the pixelizer runs to sharpen color boundaries before finalizing each frame. Higher = crisper outlines, slower bake. 5 is a safe default for most characters."));
                    EditorGUILayout.PropertyField(so.FindProperty("_flickerSuppressOnDistance"),
                        new GUIContent("Single-pixel Noise",
                            "Stops individual pixels from blinking between two nearly identical shades on consecutive frames. Useful when you see a single pixel that keeps switching color while the rest of the image is stable. Raise to suppress more. Set to 0 to disable."));

                    changed |= EditorGUI.EndChangeCheck();
                    so.ApplyModifiedProperties();

                    layout.DrawGroupGap();
                    EditorGUI.BeginChangeCheck();
                    reusePalette = EditorGUILayout.ToggleLeft(
                        new GUIContent("Lock color set for whole animation",
                            "When on, all frames share one color set instead of each frame picking its own. Prevents the overall color scheme from shifting as the animation plays. The Single-pixel Noise option above handles individual blinking pixels -- this handles the whole color scheme."),
                        reusePalette);
                    changed |= EditorGUI.EndChangeCheck();
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
