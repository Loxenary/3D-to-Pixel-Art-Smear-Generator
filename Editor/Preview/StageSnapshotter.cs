using System;
using System.IO;
using System.Text;
using UnityEngine;
using SmearFramework.DataTypes;

namespace SmearFramework.Editor
{
    // Dumps stage outputs to disk for figure-making and debugging.
    public static class StageSnapshotter
    {
        // Dumps each available artifact in the requested key list.
        public static void DumpArtifacts(PipelineContext ctx, string[] artifactKeys, string folder)
        {
            if (ctx == null || artifactKeys == null) return;
            EnsureFolder(folder);

            foreach (var key in artifactKeys)
            {
                if (!ctx.Has(key)) continue;
                var value = ctx.GetRaw(key);
                DumpOne(value, key, folder);
            }
        }

        // Dispatches to the right writer for each artifact type.
        static void DumpOne(object value, string key, string folder)
        {
            switch (value)
            {
                case RawFrameData raw: DumpRawFrames(raw, key, folder); break;
                case SmearFrameData smear: DumpSmearMeta(smear, key, folder); break;
                case MotionData motion: DumpMotion(motion, key, folder); break;
                case TrajectoryData traj: DumpTrajectory(traj, key, folder); break;
                case SpriteSheetResult sheet: DumpSpriteSheet(sheet, key, folder); break;
                default: DumpFallback(value, key, folder); break;
            }
        }

        // Writes one PNG per frame plus a tiny sidecar.
        static void DumpRawFrames(RawFrameData raw, string key, string folder)
        {
            string sub = Path.Combine(folder, key);
            EnsureFolder(sub);

            for (int i = 0; i < raw.FrameCount; i++)
            {
                if (raw.Frames[i] == null) continue;
                File.WriteAllBytes(Path.Combine(sub, $"frame_{i:D3}.png"), raw.Frames[i].EncodeToPNG());
            }

            var sb = new StringBuilder();
            sb.Append("{\"frame_count\":").Append(raw.FrameCount);
            sb.Append(",\"width\":").Append(raw.Width);
            sb.Append(",\"height\":").Append(raw.Height).Append("}");
            File.WriteAllText(Path.Combine(sub, "meta.json"), sb.ToString());
        }

        // Writes per-frame smear flags, intensity, and extra-geometry counts.
        static void DumpSmearMeta(SmearFrameData smear, string key, string folder)
        {
            var sb = new StringBuilder();
            sb.Append("{\"frame_count\":").Append(smear.FrameCount).Append(",\"frames\":[");
            for (int f = 0; f < smear.FrameCount; f++)
            {
                if (f > 0) sb.Append(',');
                int ghostVerts = smear.AdditionalGeometry != null && smear.AdditionalGeometry[f] != null
                    ? smear.AdditionalGeometry[f].vertexCount : 0;
                int lineVerts = smear.MotionLineGeometry != null && smear.MotionLineGeometry[f] != null
                    ? smear.MotionLineGeometry[f].vertexCount : 0;
                sb.Append("{\"frame\":").Append(f);
                sb.Append(",\"has_smear\":").Append(smear.HasSmear[f] ? "true" : "false");
                sb.Append(",\"intensity\":").Append(smear.SmearIntensity[f].ToString("F4"));
                sb.Append(",\"ghost_vertices\":").Append(ghostVerts);
                sb.Append(",\"line_vertices\":").Append(lineVerts).Append('}');
            }
            sb.Append("]}");
            File.WriteAllText(Path.Combine(folder, key + ".json"), sb.ToString());

            // also dump the extra smear meshes as .asset siblings so the user can inspect them in Unity
            DumpMeshSeries(smear.AdditionalGeometry, key + "_ghost", folder, "smear_ghost");
            DumpMeshSeries(smear.MotionLineGeometry, key + "_lines", folder, "smear_lines");
        }

        static void DumpMeshSeries(Mesh[] meshes, string key, string folder, string assetPrefix)
        {
            if (meshes == null) return;
            if (!folder.StartsWith("Assets/")) return; // AssetDatabase rejects external paths

            string sub = Path.Combine(folder, key + "_geometry");
            EnsureFolder(sub);
            UnityEditor.AssetDatabase.Refresh();

            for (int f = 0; f < meshes.Length; f++)
            {
                var mesh = meshes[f];
                if (mesh == null || mesh.vertexCount == 0) continue;
                var copy = UnityEngine.Object.Instantiate(mesh);
                copy.name = $"{assetPrefix}_{f:D3}";
                UnityEditor.AssetDatabase.CreateAsset(copy, Path.Combine(sub, $"{assetPrefix}_{f:D3}.asset"));
            }
            UnityEditor.AssetDatabase.SaveAssets();
        }

        // Writes a small bone-motion dump capped for readability.
        static void DumpMotion(MotionData motion, string key, string folder)
        {
            int boneCap = Mathf.Min(motion.BoneCount, 32);
            var sb = new StringBuilder();
            sb.Append("{\"frame_count\":").Append(motion.FrameCount);
            sb.Append(",\"bone_count\":").Append(motion.BoneCount);
            sb.Append(",\"bones_dumped\":").Append(boneCap);
            sb.Append(",\"fps\":").Append(motion.Fps.ToString("F2"));
            sb.Append(",\"frames\":[");
            for (int f = 0; f < motion.FrameCount; f++)
            {
                if (f > 0) sb.Append(',');
                sb.Append('[');
                var bones = motion.Bones[f];
                for (int b = 0; b < boneCap; b++)
                {
                    if (b > 0) sb.Append(',');
                    AppendBoneRow(sb, bones[b]);
                }
                sb.Append(']');
            }
            sb.Append("]}");
            File.WriteAllText(Path.Combine(folder, key + ".json"), sb.ToString());
        }

        static void AppendBoneRow(StringBuilder sb, BoneSnapshot bone)
        {
            sb.Append("{\"p\":");
            AppendVec3(sb, bone.position);
            sb.Append(",\"v\":");
            AppendVec3(sb, bone.linearVelocity);
            sb.Append('}');
        }

        // Writes a small evenly spaced trajectory sample.
        static void DumpTrajectory(TrajectoryData traj, string key, string folder)
        {
            int sampleCount = Mathf.Min(traj.VertexCount, 16);
            var sb = new StringBuilder();
            sb.Append("{\"vertex_count\":").Append(traj.VertexCount);
            sb.Append(",\"frame_count\":").Append(traj.FrameCount);
            sb.Append(",\"sampled\":").Append(sampleCount);
            sb.Append(",\"polylines\":[");

            int step = Mathf.Max(1, traj.VertexCount / sampleCount);
            for (int v = 0, written = 0; v < traj.VertexCount; v += step, written++)
            {
                if (written > 0) sb.Append(',');
                var pts = traj.ControlPoints[v];
                sb.Append("{\"vertex\":").Append(v).Append(",\"points\":[");
                for (int p = 0; p < pts.Length; p++)
                {
                    if (p > 0) sb.Append(',');
                    AppendVec3(sb, pts[p]);
                }
                sb.Append("]}");
            }
            sb.Append("]}");
            File.WriteAllText(Path.Combine(folder, key + ".json"), sb.ToString());
        }

        static void DumpSpriteSheet(SpriteSheetResult sheet, string key, string folder)
        {
            if (sheet.SpriteSheet != null)
                File.WriteAllBytes(Path.Combine(folder, key + ".png"), sheet.SpriteSheet.EncodeToPNG());

            var sb = new StringBuilder();
            sb.Append("{\"columns\":").Append(sheet.Columns);
            sb.Append(",\"rows\":").Append(sheet.Rows);
            sb.Append(",\"frame_w\":").Append(sheet.FrameWidth);
            sb.Append(",\"frame_h\":").Append(sheet.FrameHeight);
            sb.Append(",\"frame_count\":").Append(sheet.FrameCount);
            sb.Append(",\"frame_duration\":").Append(sheet.FrameDuration.ToString("F4")).Append('}');
            File.WriteAllText(Path.Combine(folder, key + "_meta.json"), sb.ToString());
        }

        static void DumpFallback(object value, string key, string folder)
        {
            string text = value != null
                ? $"type: {value.GetType().FullName}\nvalue: {value}"
                : "null";
            File.WriteAllText(Path.Combine(folder, key + ".txt"), text);
        }

        static void AppendVec3(StringBuilder sb, Vector3 v)
        {
            sb.Append('[').Append(v.x.ToString("F4")).Append(',')
              .Append(v.y.ToString("F4")).Append(',')
              .Append(v.z.ToString("F4")).Append(']');
        }

        static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
