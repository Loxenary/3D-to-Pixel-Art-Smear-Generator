using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Draws the Workflow 2 high-res source area in the left panel.
    internal sealed class PreloadSection
    {
        private const float SecondaryButtonHeight = 28f;

        // Draws the high-res source card body.
        // All workflow actions (load, clear, bake, pixelize) are callbacks kept in the window.
        public void Draw(
            SmearFrameworkEditorState state,
            bool hasValidLiveInput,
            System.Action loadFromDisk,
            System.Action clearPreload,
            System.Action bake,
            System.Action pixelize)
        {
            bool hasFrames = state.HasHighResSource;

            DrawDescription(BuildHighResSourceHint(hasFrames, hasValidLiveInput, state.HighResSourceLabel));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load from disk...", GUILayout.Height(SecondaryButtonHeight)))
                loadFromDisk();
            if (hasFrames && GUILayout.Button("Clear", GUILayout.Width(54), GUILayout.Height(SecondaryButtonHeight)))
                clearPreload();
            EditorGUILayout.EndHorizontal();

            if (hasFrames && state.AvailableHighRes != null)
                DrawDescription(
                    $"{state.AvailableHighRes.FrameCount} frames at {state.AvailableHighRes.Width}x{state.AvailableHighRes.Height}");
        }

        // Explains where Pixelization will get frames in the current state.
        private string BuildHighResSourceHint(bool hasFrames, bool hasCharClip, string sourceLabel)
        {
            if (hasFrames)
                return $"Source: {sourceLabel}";
            if (hasCharClip)
                return "Will be generated from the current character + clip at bake time, or load a *_highres.png from disk.";
            return "Load a *_highres.png from disk, or provide a character + clip so the bake can generate it.";
        }

        private void DrawDescription(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);
        }
    }
}
