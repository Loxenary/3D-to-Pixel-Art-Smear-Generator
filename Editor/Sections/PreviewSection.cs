using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Draws the viewport pane areas: backgrounds, pan/zoom input, and textures.
    // Transport row and overlay controls (which depend on window-owned _frame/_playing/_cameraAngleDirty)
    // stay in SmearFrameworkWindow and call the public helpers on this class directly.
    internal sealed class PreviewSection
    {
        // Left-pane source toggle state -- section owns this display choice.
        public enum LeftSource { Source3D, Pixelated }
        private LeftSource _leftSource = LeftSource.Pixelated;
        public  LeftSource CurrentLeftSource => _leftSource;

        // Absolute screen rect of the left/right viewport panes after the last Draw call.
        // Window uses LeftPaneRect to position DrawViewportOverlay and heatmap overlays.
        public Rect LeftPaneRect  { get; private set; }
        public Rect RightPaneRect { get; private set; }
        private static GUIStyle _groundLabelStyle;

        // Draws viewport pane backgrounds, handles pan/zoom input, and draws textures.
        //
        // getPreviewFrameCount()     -- total frame count (slider range / status line).
        // getLeftPreviewTexture(f)   -- window captures _frame in closure; f is passed but the
        //                              window implementation may ignore it and always return the
        //                              current frame's texture.
        // getRightPreviewTexture(f)  -- same pattern.
        //
        // DrawTransportRow and DrawViewportOverlay stay in the window because they access
        // _frame/_playing/_cameraAngleDirty which are window-owned [SerializeField] state.
        // The window calls DrawTransportRow() BEFORE this, and DrawViewportOverlay(LeftPaneRect)
        // AFTER this, to overlay camera controls on top of the drawn textures.
        // Draw() only draws -- it no longer handles viewport input.
        // The window calls HandleViewportInput itself after Draw() so it can pass
        // the current frame's overlay rect as the blocked zone.
        public void Draw(
            Rect availableRect,
            SmearFrameworkEditorState state,
            ref PreviewViewState leftView,
            ref PreviewViewState rightView,
            System.Func<int> getPreviewFrameCount,
            System.Func<int, Texture2D> getLeftPreviewTexture,
            System.Func<int, Texture2D> getRightPreviewTexture)
        {
            int totalFrames = Mathf.Max(1, getPreviewFrameCount());

            float gap   = 4f;
            float h     = Mathf.Clamp(availableRect.height, 120f, availableRect.width * 0.55f);
            Rect area   = GUILayoutUtility.GetRect(availableRect.width, h);

            float halfW = Mathf.Max((area.width - gap) * 0.5f, 60f);
            Rect left   = new Rect(area.x, area.y, halfW, h);
            Rect right  = new Rect(area.x + halfW + gap, area.y, halfW, h);

            LeftPaneRect  = left;
            RightPaneRect = right;

            EditorGUI.DrawRect(left,  new Color(0.12f, 0.12f, 0.12f));
            EditorGUI.DrawRect(right, new Color(0.12f, 0.12f, 0.12f));
            EditorGUIUtility.AddCursorRect(right, MouseCursor.Pan);

            Texture2D leftTex  = getLeftPreviewTexture(0);
            Texture2D rightTex = getRightPreviewTexture(0);
            if (leftTex  != null) DrawZoomedTexture(left,  leftTex,  leftView);
            if (rightTex != null) DrawZoomedTexture(right, rightTex, rightView);
        }

        // Draws pane header labels and the 3D/Pixel toggle for the left pane.
        // Called by the window's DrawRightPanel BEFORE Draw() so the header sits above the panes.
        public void DrawPaneHeaders(
            bool allowLeftSourceToggle, bool has3DLeft, bool hasPixelLeft,
            bool smearMode, bool pixelMode)
        {
            Rect headerRow = EditorGUILayout.GetControlRect(false, 22f);
            float headerGap  = 4f;
            float headerHalf = (headerRow.width - headerGap) * 0.5f;
            Rect leftHdr  = new Rect(headerRow.x, headerRow.y, headerHalf, headerRow.height);
            Rect rightHdr = new Rect(headerRow.x + headerHalf + headerGap, headerRow.y, headerHalf, headerRow.height);

            if (allowLeftSourceToggle)
            {
                float segW  = Mathf.Min(120f, leftHdr.width);
                float segX  = leftHdr.x + Mathf.Max(0f, (leftHdr.width - segW) * 0.5f);
                float halfS = segW * 0.5f;
                Rect r3D  = new Rect(segX,         leftHdr.y, halfS, leftHdr.height);
                Rect rPx  = new Rect(segX + halfS,  leftHdr.y, halfS, leftHdr.height);
                bool show3D = _leftSource == LeftSource.Source3D;
                bool showPx = _leftSource == LeftSource.Pixelated;
                if (GUI.Toggle(r3D, show3D, "3D",    EditorStyles.miniButtonLeft)  != show3D) _leftSource = LeftSource.Source3D;
                if (GUI.Toggle(rPx, showPx, "Pixel", EditorStyles.miniButtonRight) != showPx) _leftSource = LeftSource.Pixelated;
            }
            else
            {
                string lbl = ResolveLeftPaneLabel(allowLeftSourceToggle, has3DLeft, hasPixelLeft, smearMode, pixelMode);
                GUI.Label(leftHdr, lbl ?? "", EditorStyles.centeredGreyMiniLabel);
            }

            string rightLabel = smearMode ? "Smeared" : pixelMode ? "Pixel art" : "Preview";
            GUI.Label(rightHdr, rightLabel, EditorStyles.centeredGreyMiniLabel);
            Rect gutter = new Rect(leftHdr.xMax, headerRow.y, rightHdr.x - leftHdr.xMax, headerRow.height);
            GUI.Label(gutter, "→", EditorStyles.centeredGreyMiniLabel);
        }

        // Returns the left pane label when the toggle is not shown.
        public string ResolveLeftPaneLabel(bool allowToggle, bool has3D, bool hasPixel, bool smear, bool pixel)
        {
            if (allowToggle)     return null;
            if (pixel && !smear) return "Input";
            return has3D ? "3D" : hasPixel ? "Pixel" : "Input";
        }

        // Draws a texture inside a clip rect with pan/zoom/roll from the view state.
        // Called by Draw() internally and also by the window for heatmap overlay.
        public void DrawZoomedTexture(Rect clipRect, Texture2D tex, PreviewViewState view)
        {
            if (tex == null) return;
            float imgW = clipRect.width  * view.Zoom;
            float imgH = clipRect.height * view.Zoom;
            float cx = clipRect.x + clipRect.width  * 0.5f + view.Offset.x * clipRect.width;
            float cy = clipRect.y + clipRect.height * 0.5f - view.Offset.y * clipRect.height;

            GUI.BeginClip(clipRect);
            Rect local = new Rect(
                cx - imgW * 0.5f - clipRect.x,
                cy - imgH * 0.5f - clipRect.y,
                imgW, imgH);
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(
                view.RollDeg,
                new Vector2(clipRect.width * 0.5f, clipRect.height * 0.5f));
            GUI.DrawTexture(local, tex, ScaleMode.ScaleToFit, true);
            GUI.matrix = old;
            GUI.EndClip();
        }

        // Draws a dashed horizontal guide inside the same zoomed view transform as the preview image.
        public void DrawZoomedHorizontalGuide(Rect clipRect, float normalizedY, PreviewViewState view, Color solid, Color gap)
        {
            float imgW = clipRect.width  * view.Zoom;
            float imgH = clipRect.height * view.Zoom;
            float cx = clipRect.x + clipRect.width  * 0.5f + view.Offset.x * clipRect.width;
            float cy = clipRect.y + clipRect.height * 0.5f - view.Offset.y * clipRect.height;

            GUI.BeginClip(clipRect);
            Rect local = new Rect(
                cx - imgW * 0.5f - clipRect.x,
                cy - imgH * 0.5f - clipRect.y,
                imgW, imgH);
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(
                view.RollDeg,
                new Vector2(clipRect.width * 0.5f, clipRect.height * 0.5f));

            float thickness = Mathf.Max(5f, local.height * 0.014f);
            float y = local.y + (1f - Mathf.Clamp01(normalizedY)) * local.height - thickness * 0.5f;
            Color oldColor = GUI.color;
            GUI.color = solid;
            GUI.DrawTexture(
                new Rect(local.x, y, local.width, thickness),
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill,
                true);

            if (_groundLabelStyle == null)
            {
                _groundLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.UpperCenter,
                    normal = { textColor = new Color(0.2f, 1f, 0.35f, 1f) }
                };
            }
            Rect labelRect = new Rect(local.x, y + thickness + 4f, local.width, 18f);
            GUI.Label(labelRect, "Ground", _groundLabelStyle);
            GUI.color = oldColor;

            GUI.matrix = old;
            GUI.EndClip();
        }

        // Draws a vertical guide inside the same zoomed view transform as the preview image.
        public void DrawZoomedVerticalGuide(Rect clipRect, float normalizedX, PreviewViewState view, Color solid)
        {
            float imgW = clipRect.width  * view.Zoom;
            float imgH = clipRect.height * view.Zoom;
            float cx = clipRect.x + clipRect.width  * 0.5f + view.Offset.x * clipRect.width;
            float cy = clipRect.y + clipRect.height * 0.5f - view.Offset.y * clipRect.height;

            GUI.BeginClip(clipRect);
            Rect local = new Rect(
                cx - imgW * 0.5f - clipRect.x,
                cy - imgH * 0.5f - clipRect.y,
                imgW, imgH);
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(
                view.RollDeg,
                new Vector2(clipRect.width * 0.5f, clipRect.height * 0.5f));

            float thickness = Mathf.Max(5f, local.width * 0.014f);
            float x = local.x + Mathf.Clamp01(normalizedX) * local.width - thickness * 0.5f;
            Color oldColor = GUI.color;
            GUI.color = solid;
            GUI.DrawTexture(
                new Rect(x, local.y, thickness, local.height),
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill,
                true);
            GUI.color = oldColor;

            if (_groundLabelStyle == null)
            {
                _groundLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.UpperCenter,
                    normal = { textColor = new Color(0.2f, 1f, 0.35f, 1f) }
                };
            }
            Rect labelRect = new Rect(x + thickness + 4f, local.y + 4f, 56f, 18f);
            GUI.Label(labelRect, "Pivot", _groundLabelStyle);

            GUI.matrix = old;
            GUI.EndClip();
        }

        // Draws a pivot cross marker so the output pane shows the actual pivot point, not two unrelated labels.
        public void DrawZoomedPivotCross(Rect clipRect, float normalizedX, float normalizedY, PreviewViewState view, Color solid)
        {
            float imgW = clipRect.width  * view.Zoom;
            float imgH = clipRect.height * view.Zoom;
            float cx = clipRect.x + clipRect.width  * 0.5f + view.Offset.x * clipRect.width;
            float cy = clipRect.y + clipRect.height * 0.5f - view.Offset.y * clipRect.height;

            GUI.BeginClip(clipRect);
            Rect local = new Rect(
                cx - imgW * 0.5f - clipRect.x,
                cy - imgH * 0.5f - clipRect.y,
                imgW, imgH);
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(
                view.RollDeg,
                new Vector2(clipRect.width * 0.5f, clipRect.height * 0.5f));

            float hThickness = Mathf.Max(5f, local.height * 0.014f);
            float vThickness = Mathf.Max(5f, local.width * 0.014f);
            float y = local.y + (1f - Mathf.Clamp01(normalizedY)) * local.height - hThickness * 0.5f;
            float x = local.x + Mathf.Clamp01(normalizedX) * local.width - vThickness * 0.5f;
            Color oldColor = GUI.color;
            var frameColor = new Color(solid.r, solid.g, solid.b, 0.35f);
            GUI.color = frameColor;
            GUI.DrawTexture(new Rect(local.x, local.y, local.width, 2f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(new Rect(local.x, local.yMax - 2f, local.width, 2f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(new Rect(local.x, local.y, 2f, local.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(new Rect(local.xMax - 2f, local.y, 2f, local.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true);

            GUI.color = solid;
            GUI.DrawTexture(new Rect(local.x, y, local.width, hThickness), Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(new Rect(x, local.y, vThickness, local.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
            float dot = Mathf.Max(10f, Mathf.Min(local.width, local.height) * 0.03f);
            GUI.DrawTexture(new Rect(x - dot * 0.5f + vThickness * 0.5f, y - dot * 0.5f + hThickness * 0.5f, dot, dot), Texture2D.whiteTexture, ScaleMode.StretchToFill, true);

            if (_groundLabelStyle == null)
            {
                _groundLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.UpperCenter,
                    normal = { textColor = new Color(0.2f, 1f, 0.35f, 1f) }
                };
            }
            Rect labelRect = new Rect(x + vThickness + 6f, y - 18f, 64f, 18f);
            GUI.Label(labelRect, "Pivot", _groundLabelStyle);
            GUI.color = oldColor;

            GUI.matrix = old;
            GUI.EndClip();
        }

        // Handles pan and zoom mouse events for a viewport rect. Returns true when the view changed.
        // Called by Draw() internally and can also be called by the window for custom panes.
        public bool HandleViewportInput(Rect viewport, Rect blockedRect, ref PreviewViewState view)
        {
            int       cid = GUIUtility.GetControlID(FocusType.Passive);
            EventType et  = Event.current.GetTypeForControl(cid);
            bool blocked  = blockedRect.width > 0 && blockedRect.Contains(Event.current.mousePosition);

            switch (et)
            {
                case EventType.MouseDown:
                    if (viewport.Contains(Event.current.mousePosition) && !blocked
                        && (Event.current.button == 0 || Event.current.button == 2))
                    { GUIUtility.hotControl = cid; Event.current.Use(); }
                    return false;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == cid)
                    {
                        Vector2 d = Event.current.delta;
                        view.Offset += new Vector2(d.x / viewport.width, -d.y / viewport.height);
                        view.Offset.x = Mathf.Clamp(view.Offset.x, -2f, 2f);
                        view.Offset.y = Mathf.Clamp(view.Offset.y, -2f, 2f);
                        Event.current.Use(); return true;
                    }
                    return false;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == cid) { GUIUtility.hotControl = 0; Event.current.Use(); }
                    return false;

                case EventType.ScrollWheel:
                    if (viewport.Contains(Event.current.mousePosition) && !blocked)
                    {
                        view.Zoom *= 1f - Event.current.delta.y * 0.03f;
                        view.Zoom  = Mathf.Clamp(view.Zoom, 0.2f, 5f);
                        Event.current.Use(); return true;
                    }
                    return false;
            }
            return false;
        }
    }
}
