using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SmearFramework.DataTypes;
using SmearFramework.VelocityExtraction;

namespace SmearFramework.SmearGeneration
{
    // notes: tremolieres-2025-trajectory-aware-smears.md, basset-2024-smear-stylized-motion.md
    /// <summary>
    /// Creates ghost copies of the character mesh at past and future trajectory positions. Fast-motion weighting still shapes the copy opacity, but each copy keeps a visible silhouette so the effect reads like a classic multiple smear.
    /// </summary>
    public class MultipleSmearStrategy : ISmearStrategy
    {
        private TrajectoryBuilder _trajEval = new TrajectoryBuilder();

        public bool IsEnabled(PipelineConfig config) => config.EnableMultiples;

        // Build one ghost copy per (past/future offset, source SMR), then fold into AdditionalGeometry
        public void Apply(PipelineContext ctx, SmearFrameData output, int frame)
        {
            var motion = ctx.Get<MotionData>("motion");
            var traj = ctx.Get<TrajectoryData>("trajectory");
            var config = ctx.Config;

            int past = config.PastCopies;
            int future = config.FutureCopies;
            int totalCopies = past + future;
            if (totalCopies == 0) return;

            // bake source meshes once -- we need their triangle topology and per-SMR vertex counts
            var smrs = ctx.Target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var sourceMeshes = BakeSourceMeshes(smrs);

            try
            {
                var allGhosts = BuildAllGhostCopies(motion, traj, config, sourceMeshes, frame, past, future);
                MergeIntoOutput(output, frame, allGhosts);
            }
            finally
            {
                foreach (var m in sourceMeshes)
                    if (m != null) Object.DestroyImmediate(m);
            }
        }

        // Bake current-pose SMRs so each ghost can copy their triangle topology
        private Mesh[] BakeSourceMeshes(SkinnedMeshRenderer[] smrs)
        {
            var meshes = new Mesh[smrs.Length];
            for (int i = 0; i < smrs.Length; i++)
            {
                meshes[i] = new Mesh();
                smrs[i].BakeMesh(meshes[i]);
            }
            return meshes;
        }

        // Build a list of CombineInstances covering every (copy index, SMR) pair
        private List<CombineInstance> BuildAllGhostCopies(
            MotionData motion, TrajectoryData traj, PipelineConfig config,
            Mesh[] sourceMeshes, int frame, int past, int future)
        {
            int totalCopies = past + future;
            int mMax = Mathf.Max(past, future);
            float spacing = ComputeCopySpacing(config, totalCopies);

            var combineList = new List<CombineInstance>();
            for (int m = -past; m <= future; m++)
            {
                if (m == 0) continue;
                float beta = m * spacing * GetDisplacementScale(config, m) * config.SmearStrength;
                float offsetThreshold = (Mathf.Abs(m) - 1f) / mMax;
                AppendOneCopy(motion, traj, config, ComputeVertexSpeeds(motion, frame), sourceMeshes, frame, beta, m, offsetThreshold, combineList);
            }
            return combineList;
        }

        // For one copy index, walk all SMRs and add one ghost mesh each
        private void AppendOneCopy(
            MotionData motion, TrajectoryData traj, PipelineConfig config, float[] vertexSpeeds, Mesh[] sourceMeshes,
            int frame, float beta, int copyIndex, float offsetThreshold,
            List<CombineInstance> sink)
        {
            int vertOffset = 0;
            for (int s = 0; s < sourceMeshes.Length; s++)
            {
                var ghost = BuildGhostCopy(motion, traj, config, vertexSpeeds, frame, beta, copyIndex, offsetThreshold,
                    sourceMeshes[s], vertOffset);
                sink.Add(new CombineInstance { mesh = ghost, transform = Matrix4x4.identity });
                vertOffset += sourceMeshes[s].vertexCount;
            }
        }

        // Build a single displaced+colored mesh whose topology matches the source SMR
        // Eq 12 can legitimately drive an entire copy to zero alpha when no vertex passes the direction gate.
        private Mesh BuildGhostCopy(
            MotionData motion, TrajectoryData traj, PipelineConfig config, float[] vertexSpeeds, int frame,
            float beta, int copyIndex, float offsetThreshold,
            Mesh sourceMesh, int vertOffset)
        {
            int vCount = sourceMesh.vertexCount;
            var positions = new Vector3[vCount];
            var colors = new Color[vCount];
            float copyOpacity = ComputeCopyOpacity(config, copyIndex);

            for (int v = 0; v < vCount; v++)
            {
                int globalV = vertOffset + v;
                positions[v] = _trajEval.Evaluate(traj, globalV, frame + beta);

                float alpha = ComputeWeightedAlpha(motion, vertexSpeeds, config, frame, globalV, copyIndex, offsetThreshold);
                colors[v] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha * copyOpacity * config.SmearStrength));
            }

            var mesh = new Mesh();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = positions;
            mesh.colors = colors;
            mesh.triangles = sourceMesh.triangles; // reuse source topology, vertex layout matches
            if (sourceMesh.uv != null && sourceMesh.uv.Length == vCount)
                mesh.uv = sourceMesh.uv; // carry UVs so the ghost shader can sample the real texture
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Combine new ghosts with whatever a prior strategy may have already put on the frame
        private void MergeIntoOutput(SmearFrameData output, int frame, List<CombineInstance> ghosts)
        {
            if (ghosts.Count == 0) return;

            var combined = new Mesh();
            combined.indexFormat = IndexFormat.UInt32;
            combined.CombineMeshes(ghosts.ToArray(), true, false);

            if (output.AdditionalGeometry[frame] == null)
            {
                output.AdditionalGeometry[frame] = combined;
                return;
            }

            var pair = new[]
            {
                new CombineInstance { mesh = output.AdditionalGeometry[frame], transform = Matrix4x4.identity },
                new CombineInstance { mesh = combined, transform = Matrix4x4.identity },
            };
            var merged = new Mesh();
            merged.indexFormat = IndexFormat.UInt32;
            merged.CombineMeshes(pair, true, false);
            output.AdditionalGeometry[frame] = merged;
        }

        // Paper spacing keeps copies inside neighboring sub-frame trajectory samples.
        // This reads as tight overlap, not wide Tom-and-Jerry-style separation.
        private float ComputeCopySpacing(PipelineConfig config, int totalCopies)
        {
            return 1f / (totalCopies + 1 - config.OverlapCount);
        }

        // Add-on-style distance control on top of the paper spacing.
        private float GetDisplacementScale(PipelineConfig config, int copyIndex)
        {
            return copyIndex < 0 ? config.PastDisplacement : config.FutureDisplacement;
        }

        // Fade farther copies so the sequence dies off instead of hitting a hard stop.
        private float ComputeCopyOpacity(PipelineConfig config, int copyIndex)
        {
            float factor = copyIndex < 0 ? config.PastOpacityFactor : config.FutureOpacityFactor;
            return Mathf.Pow(factor, Mathf.Max(0, Mathf.Abs(copyIndex) - 1));
        }

        private float[] ComputeVertexSpeeds(MotionData motion, int frame)
        {
            int prev = Mathf.Max(frame - 1, 0);
            var speeds = new float[motion.VertexCount];
            for (int v = 0; v < motion.VertexCount; v++)
            {
                speeds[v] = Vector3.Distance(
                    motion.Vertices[frame][v].position,
                    motion.Vertices[prev][v].position) * motion.Fps;
            }
            return speeds;
        }

        // Smoothstep alpha based on how far this vertex moved relative to the copy's threshold
        private float ComputeWeightedAlpha(
            MotionData motion, float[] vertexSpeeds, PipelineConfig config, int frame, int v, int copyIndex, float offsetThreshold)
        {
            if (vertexSpeeds[v] < config.MultipleSpeedThreshold)
                return 0f;

            float offset = motion.Vertices[frame][v].motionOffset;
            bool visible = (copyIndex < 0) ? offset < 0f : offset > 0f; // Basset 2024 Eq 12: H(delta_i * sign(m))
            if (!visible) return 0f;

            float absOffset = Mathf.Abs(offset);
            if (absOffset <= offsetThreshold || offsetThreshold >= 1f)
                return 0f;

            float t = (absOffset - offsetThreshold) / (1f - offsetThreshold);
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t); // smoothstep
        }
    }
}
