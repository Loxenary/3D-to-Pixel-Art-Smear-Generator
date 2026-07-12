using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Draws section cards, splitter, embedded inspectors, and repeated layout helpers.
    internal sealed class LayoutSection
    {
        private const float CardGap = 8f;
        private const float CardInnerTopGap = 4f;
        private const float CardInnerBottomGap = 6f;
        private const float GroupGap = 10f;
        private const float FieldGap = 2f;

        // Draws a titled section card and returns control to the caller for body content.
        public void BeginSectionCard(string title, Color tint)
        {
            EditorGUILayout.Space(CardGap);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            var stripe = GUILayoutUtility.GetRect(1f, 4f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(stripe, tint);
            EditorGUILayout.Space(3);
            DrawSectionTitle(title, tint);
            EditorGUILayout.Space(CardInnerTopGap);
        }

        // Ends the current section card.
        public void EndSectionCard()
        {
            EditorGUILayout.Space(CardInnerBottomGap);
            EditorGUILayout.EndVertical();
        }

        // Draws a stronger section header with a muted color accent.
        public void DrawSectionTitle(string title, Color tint)
        {
            EditorGUILayout.BeginHorizontal();
            var mark = GUILayoutUtility.GetRect(5f, 18f, GUILayout.Width(5f));
            EditorGUI.DrawRect(mark, tint);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        // Draws a miniBoldLabel and a small gap for a field label.
        public void DrawFieldLabel(GUIContent label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.Space(FieldGap);
        }

        // String overload.
        public void DrawFieldLabel(string label)
        {
            DrawFieldLabel(new GUIContent(label));
        }

        // Draws word-wrapped mini text, skipped when empty.
        public void DrawDescription(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);
        }

        // Inserts a standard group gap between related control groups.
        public void DrawGroupGap()
        {
            EditorGUILayout.Space(GroupGap);
        }

        // Draws an embedded inspector using the existing narrow-mode behavior.
        public void DrawEmbeddedInspector(UnityEditor.Editor editor)
        {
            if (editor == null) return;
            bool oldWideMode = EditorGUIUtility.wideMode;
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            try { editor.OnInspectorGUI(); }
            finally
            {
                EditorGUIUtility.wideMode = oldWideMode;
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }
        }

        // Overload that applies narrow-mode label width when the left panel is narrow.
        public void DrawEmbeddedInspector(UnityEditor.Editor editor, bool narrow)
        {
            if (editor == null) return;
            bool oldWideMode = EditorGUIUtility.wideMode;
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.wideMode = !narrow;
            EditorGUIUtility.labelWidth = narrow ? 150f : oldLabelWidth;
            try { editor.OnInspectorGUI(); }
            finally
            {
                EditorGUIUtility.wideMode = oldWideMode;
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }
        }

        // Draws the splitter bar between left and right panels.
        public void DrawSplitter(float splitterWidth)
        {
            var r = GUILayoutUtility.GetRect(splitterWidth, 0f, GUILayout.Width(splitterWidth), GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(r, MouseCursor.ResizeHorizontal);
            EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.12f, 1f));
        }

        // Handles splitter drag events and updates leftRatio.
        public void HandleSplitterDrag(float totalWidth, ref float leftRatio, ref bool draggingSplitter,
            float splitterWidth, float leftRatioMin, float leftRatioMax)
        {
            var e = Event.current;
            float splitterX = totalWidth * leftRatio;
            if (e.type == EventType.MouseDown && e.button == 0 &&
                e.mousePosition.x >= splitterX - 2f && e.mousePosition.x <= splitterX + splitterWidth + 2f)
            {
                draggingSplitter = true;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && draggingSplitter)
            {
                leftRatio = Mathf.Clamp(e.mousePosition.x / totalWidth, leftRatioMin, leftRatioMax);
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                draggingSplitter = false;
            }
        }
    }
}
