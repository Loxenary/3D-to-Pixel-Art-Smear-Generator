// note: rohmer-2021-velocity-skinning.md (velocity data source)
using UnityEngine;
using SmearFramework.DataTypes;

namespace SmearFramework.Editor
{
    // Builds a heatmap texture from per-vertex motionOffset values for the editor preview overlay.
    public static class VelocityHeatmapRenderer
    {
        // Returns just the velocity dot overlay with a transparent background.
        // Draw cleanFrame first, then this on top -- GUI.DrawTexture alpha-blends them correctly.
        public static Texture2D BuildOverlay(MotionData motion, int frame, CaptureFrame captureFrame, Texture2D baseFrame)
        {
            if (motion == null || captureFrame == null) return null;

            int w = baseFrame != null ? baseFrame.width : 256;
            int h = baseFrame != null ? baseFrame.height : 256;

            var dots = BuildHeatPixels(motion, frame, captureFrame, w, h);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels(dots);
            tex.Apply();
            return tex;
        }

        // project each vertex's motionOffset onto the capture plane and splat colored dots
        private static Color[] BuildHeatPixels(MotionData motion, int frame, CaptureFrame cap, int w, int h)
        {
            var pixels = new Color[w * h];
            if (motion.Vertices == null || frame < 0 || frame >= motion.Vertices.FrameCount) return pixels;
            var verts = motion.Vertices[frame];
            if (verts == null) return pixels;

            float maxMag = 0f;
            for (int v = 0; v < verts.Length; v++)
                maxMag = Mathf.Max(maxMag, Mathf.Abs(verts[v].motionOffset));
            if (maxMag < 0.0001f) return pixels; // nothing to show

            var invRot = Quaternion.Inverse(cap.Rotation);
            float halfSize = cap.OrthoSize;

            for (int v = 0; v < verts.Length; v++)
            {
                float t = Mathf.Abs(verts[v].motionOffset) / maxMag;
                if (t < 0.01f) continue;

                Vector3 local = invRot * (verts[v].position - cap.Center);
                float u = local.x / (2f * halfSize) + 0.5f;
                float vv = local.y / (2f * halfSize) + 0.5f;

                int px = Mathf.Clamp(Mathf.RoundToInt(u * w), 0, w - 1);
                int py = Mathf.Clamp(Mathf.RoundToInt(vv * h), 0, h - 1);

                Color heat = HeatColor(t);
                int idx = py * w + px;
                if (heat.a > pixels[idx].a)
                    pixels[idx] = heat;
            }
            return pixels;
        }

        // Returns the viewport Y for the world ground plane and clamps it into view.
        // Clamping keeps the guide visible even when the ground falls just outside the capture.
        public static float GetGroundLineViewportY(CaptureFrame cap)
        {
            if (cap == null || cap.OrthoSize <= 0f) return 0.5f;
            var invRot = Quaternion.Inverse(cap.Rotation);
            Vector3 groundLocal = invRot * (new Vector3(cap.Center.x, 0f, cap.Center.z) - cap.Center);
            float vv = groundLocal.y / (2f * cap.OrthoSize) + 0.5f;
            return Mathf.Clamp01(vv);
        }

        private static Color HeatColor(float t)
        {
            Color c;
            if (t < 0.25f)      c = Color.Lerp(Color.blue, Color.cyan,   t / 0.25f);
            else if (t < 0.5f)  c = Color.Lerp(Color.cyan, Color.green,  (t - 0.25f) / 0.25f);
            else if (t < 0.75f) c = Color.Lerp(Color.green, Color.yellow, (t - 0.5f)  / 0.25f);
            else                c = Color.Lerp(Color.yellow, Color.red,   (t - 0.75f) / 0.25f);
            c.a = 0.65f + t * 0.35f;
            return c;
        }
    }
}
