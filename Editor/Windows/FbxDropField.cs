using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmearFramework.Editor
{
    // Drop zone + browse button that only accepts raw .fbx assets.
    // Replaces ObjectField for FBX inputs so non-.fbx assets are rejected before they land.
    public static class FbxDropField
    {
        static readonly Color DropZoneBorder   = new Color(0.4f, 0.4f, 0.4f, 1f);
        static readonly Color DropZoneActive   = new Color(0.3f, 0.6f, 1f, 0.25f);
        static readonly Color DropZoneNeutral  = new Color(0f, 0f, 0f, 0.15f);
        static readonly Color DropZoneReject   = new Color(1f, 0.25f, 0.25f, 0.18f);

        // Draw the drop zone and browse button. Returns the resolved FBX asset path (or null).
        // current is the currently held path; label is the field label shown above the zone.
        public static string Draw(string label, string currentPath)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);

            string newPath = DrawDropZone(currentPath);
            newPath = DrawBrowseRow(newPath);
            return newPath;
        }

        // Draw the rectangular drop zone.
        static string DrawDropZone(string currentPath)
        {
            float height = 36f;
            Rect zone = GUILayoutUtility.GetRect(0f, height, GUILayout.ExpandWidth(true));

            Event e = Event.current;
            bool hovering = zone.Contains(e.mousePosition);

            // Decide overlay color based on drag state.
            Color bg = DropZoneNeutral;
            if (hovering && (e.type == EventType.DragUpdated || e.type == EventType.DragExited))
            {
                bool acceptable = DragContainsFbx();
                DragAndDrop.visualMode = acceptable ? DragAndDropVisualMode.Generic : DragAndDropVisualMode.Rejected;
                bg = acceptable ? DropZoneActive : DropZoneReject;
                e.Use();
            }

            if (hovering && e.type == EventType.DragPerform && DragContainsFbx())
            {
                DragAndDrop.AcceptDrag();
                string dropped = FirstFbxPath();
                e.Use();
                GUI.changed = true;
                DrawZoneBox(zone, bg, dropped);
                return dropped;
            }

            string display = string.IsNullOrEmpty(currentPath)
                ? "Drop .fbx here"
                : Path.GetFileName(currentPath);

            DrawZoneBox(zone, bg, display);
            return currentPath;
        }

        // Draw browse + clear buttons.
        static string DrawBrowseRow(string currentPath)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Browse for .fbx...", EditorStyles.miniButton, GUILayout.ExpandWidth(true)))
            {
                string picked = EditorUtility.OpenFilePanelWithFilters(
                    "Select FBX", Application.dataPath, new[] { "FBX files", "fbx" });
                if (!string.IsNullOrEmpty(picked))
                {
                    string assetPath = AbsoluteToAssetPath(picked);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        GUI.changed = true;
                        EditorGUILayout.EndHorizontal();
                        return assetPath;
                    }
                    // Picked file is outside the project.
                    Debug.LogWarning("[SmearGenerator] Selected .fbx is outside the Assets folder -- move it into the project first.");
                }
            }

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(currentPath));
            if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(44f)))
            {
                GUI.changed = true;
                EditorGUILayout.EndHorizontal();
                return null;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
            return currentPath;
        }

        // Draw the zone background, border, and centered label.
        static void DrawZoneBox(Rect zone, Color bg, string text)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(zone, bg);
            // border
            float b = 1f;
            EditorGUI.DrawRect(new Rect(zone.x, zone.y, zone.width, b), DropZoneBorder);
            EditorGUI.DrawRect(new Rect(zone.x, zone.yMax - b, zone.width, b), DropZoneBorder);
            EditorGUI.DrawRect(new Rect(zone.x, zone.y, b, zone.height), DropZoneBorder);
            EditorGUI.DrawRect(new Rect(zone.xMax - b, zone.y, b, zone.height), DropZoneBorder);

            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                alignment  = TextAnchor.MiddleCenter,
                wordWrap   = false,
                fontSize   = 11,
            };
            style.normal.textColor = string.IsNullOrEmpty(text) || text == "Drop .fbx here"
                ? new Color(0.5f, 0.5f, 0.5f)
                : EditorStyles.label.normal.textColor;

            GUI.Label(zone, text, style);
        }

        // Check whether any dragged object resolves to an .fbx asset.
        static bool DragContainsFbx()
        {
            foreach (var path in DragAndDrop.paths)
            {
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            foreach (var obj in DragAndDrop.objectReferences)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // Return the asset path of the first .fbx in the current drag.
        static string FirstFbxPath()
        {
            foreach (var path in DragAndDrop.paths)
            {
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            foreach (var obj in DragAndDrop.objectReferences)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            return null;
        }

        // Convert an absolute system path to an Assets-relative path.
        static string AbsoluteToAssetPath(string absolute)
        {
            string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            string normal   = Path.GetFullPath(absolute).Replace('\\', '/');
            if (normal.StartsWith(dataPath + "/") || normal == dataPath)
                return "Assets" + normal.Substring(dataPath.Length);
            return null;
        }
    }
}
