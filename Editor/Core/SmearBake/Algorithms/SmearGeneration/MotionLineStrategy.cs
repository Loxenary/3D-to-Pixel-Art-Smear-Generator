using UnityEngine;
using System.Collections.Generic;
using SmearFramework.DataTypes;
using SmearFramework.VelocityExtraction;
using Random = System.Random;

namespace SmearFramework.SmearGeneration
{
    // notes: tremolieres-2025-trajectory-aware-smears.md, basset-2024-smear-stylized-motion.md
    public class MotionLineStrategy : ISmearStrategy
    {
        private TrajectoryBuilder _trajEval = new TrajectoryBuilder();

        // shared across methods during a single Apply call
        private List<Vector3> _verts;
        private List<int> _tris;
        private List<Color> _colors;

        public bool IsEnabled(PipelineConfig config) => config.EnableMotionLines;

        // Pick random seed vertices and build trailing quad strips behind each one
        public void Apply(PipelineContext ctx, SmearFrameData output, int frame)
        {
            var motion = ctx.Get<MotionData>("motion");
            var traj = ctx.Get<TrajectoryData>("trajectory");
            var config = ctx.Config;

            var speeds = ComputeVertexSpeeds(motion, frame);
            float maxSpeed = 0f;
            for (int v = 0; v < speeds.Length; v++)
            {
                if (speeds[v] >= config.MotionLineSpeedThreshold && speeds[v] > maxSpeed)
                    maxSpeed = speeds[v];
            }
            if (maxSpeed <= 0.0001f) return;

            _verts = new List<Vector3>();
            _tris = new List<int>();
            _colors = new List<Color>();

            var seeds = PickSeedVertices(config, motion, speeds, frame);
            foreach (int seedIdx in seeds)
                GenerateLineForVertex(seedIdx, frame, motion, traj, config, speeds[seedIdx], maxSpeed);

            if (_verts.Count == 0) return;

            var lineMesh = BuildLineMesh();
            MergeIntoOutput(output, frame, lineMesh);
        }


        // Deterministically select random vertices to spawn motion lines from
        private List<int> PickSeedVertices(PipelineConfig config, MotionData motion, float[] speeds, int frame)
        {
            var rng = new Random(frame * 31337);
            var seeds = new List<int>();
            int attempts = 0;
            int maxAttempts = Mathf.Max(config.MotionLineSeeds * 8, motion.VertexCount * 2);
            while (seeds.Count < config.MotionLineSeeds && attempts < maxAttempts)
            {
                int candidate = rng.Next(motion.VertexCount);
                if (speeds[candidate] >= config.MotionLineSpeedThreshold)
                    seeds.Add(candidate);
                attempts++;
            }
            return seeds;
        }

        // Derive per-vertex speed from the bone linearVelocity already computed during extraction,
        // propagated through skinning weights -- same source the velocity heatmap uses.
        private float[] ComputeVertexSpeeds(MotionData motion, int frame)
        {
            var speeds  = new float[motion.VertexCount];
            var weights = motion.PropagatedWeights;
            if (weights == null) return speeds;
            for (int v = 0; v < motion.VertexCount; v++)
            {
                float speed = 0f;
                for (int b = 0; b < motion.BoneCount; b++)
                    speed += weights[v][b] * motion.Bones[frame][b].linearVelocity.magnitude;
                speeds[v] = speed;
            }
            return speeds;
        }

        // Build a quad strip trailing behind this vertex based on its speed
        private void GenerateLineForVertex(
            int seedIdx, int frame,
            MotionData motion, TrajectoryData traj, PipelineConfig config,
            float speed, float maxSpeed)
        {
            // threshold=0 means "show everywhere" -- all seeds get full-length lines
            // threshold>0 means length scales by how far above the threshold this vertex is,
            // not by how fast it is relative to the fastest bone
            float delta;
            float threshold = config.MotionLineSpeedThreshold;
            if (threshold <= 0.0001f)
            {
                delta = 1f;
            }
            else
            {
                float range = maxSpeed - threshold;
                delta = range > 0.0001f ? Mathf.Clamp01((speed - threshold) / range) : 1f;
            }
            float lineLen = config.MotionLineMaxLength * delta;
            if (lineLen < 0.01f) return;

            int segments = Mathf.Max(2, Mathf.CeilToInt(lineLen * 4));
            float step = lineLen / segments;

            Vector3 prevPos = _trajEval.Evaluate(traj, seedIdx, frame);

            for (int s = 1; s <= segments; s++)
                prevPos = AppendSegmentQuad(prevPos, seedIdx, frame, s, segments, step, traj, config);
        }

        // Add one quad segment between the previous position and the next spline sample
        private Vector3 AppendSegmentQuad(
            Vector3 prevPos, int seedIdx, int frame,
            int segIndex, int segCount, float step,
            TrajectoryData traj, PipelineConfig config)
        {
            float t = frame - segIndex * step;
            Vector3 pos = _trajEval.Evaluate(traj, seedIdx, t);
            Vector3 dir = (pos - prevPos).normalized;

            // Keep the strip in the camera plane so the quads do not go edge-on.
            Vector3 side = Vector3.Cross(dir, Vector3.forward).normalized * config.MotionLineThickness * 0.5f;
            if (side.sqrMagnitude < 0.00001f)
                side = Vector3.Cross(dir, Vector3.right).normalized * config.MotionLineThickness * 0.5f;

            int baseIdx = _verts.Count;

            float alpha = 1f - ((float)segIndex / segCount);
            var col = config.MotionLineColor;
            col.a = Mathf.Clamp01(col.a * alpha);

            _verts.Add(prevPos + side);
            _verts.Add(prevPos - side);
            _verts.Add(pos + side);
            _verts.Add(pos - side);

            _colors.Add(col);
            _colors.Add(col);
            _colors.Add(col);
            _colors.Add(col);

            _tris.Add(baseIdx);
            _tris.Add(baseIdx + 2);
            _tris.Add(baseIdx + 1);
            _tris.Add(baseIdx + 1);
            _tris.Add(baseIdx + 2);
            _tris.Add(baseIdx + 3);

            return pos;
        }

        // Assemble accumulated verts, tris, and colors into a final mesh
        private Mesh BuildLineMesh()
        {
            var mesh = new Mesh();
            mesh.SetVertices(_verts);
            mesh.SetTriangles(_tris, 0);
            mesh.SetColors(_colors);
            return mesh;
        }

        // Combine the line mesh with any existing line geometry already on this frame
        private void MergeIntoOutput(SmearFrameData output, int frame, Mesh lineMesh)
        {
            if (output.MotionLineGeometry[frame] != null)
            {
                var combine = new CombineInstance[2];
                combine[0].mesh = output.MotionLineGeometry[frame];
                combine[0].transform = Matrix4x4.identity;
                combine[1].mesh = lineMesh;
                combine[1].transform = Matrix4x4.identity;
                var merged = new Mesh();
                merged.CombineMeshes(combine, true, false);
                output.MotionLineGeometry[frame] = merged;
            }
            else
            {
                output.MotionLineGeometry[frame] = lineMesh;
            }
        }
    }
}
