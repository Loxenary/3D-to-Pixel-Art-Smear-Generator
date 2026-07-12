using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.VelocityExtraction;

namespace SmearFramework.SmearGeneration
{
    // notes: tremolieres-2025-trajectory-aware-smears.md, basset-2024-smear-stylized-motion.md
    public class ElongatedSmearStrategy : ISmearStrategy
    {
        private TrajectoryBuilder _trajEval = new TrajectoryBuilder();

        public bool IsEnabled(PipelineConfig config) => config.EnableElongated;

        public void Apply(PipelineContext ctx, SmearFrameData output, int frame)
        {
            var motion = ctx.Get<MotionData>("motion");
            var traj = ctx.Get<TrajectoryData>("trajectory");
            var config = ctx.Config;

            var speeds = ComputeVertexSpeeds(motion, frame);
            if (speeds.max < 0.001f) return;

            for (int v = 0; v < motion.VertexCount; v++)
                DisplaceAlongSpline(v, frame, speeds, motion, traj, config, output);
        }

        private (float[] perVertex, float max) ComputeVertexSpeeds(MotionData motion, int frame)
        {
            int prev = Mathf.Max(frame - 1, 0);
            float maxSpeed = 0f;
            var speeds = new float[motion.VertexCount];

            for (int v = 0; v < motion.VertexCount; v++)
            {
                // distance between current and previous frame position, multiplied by fps to get units/sec
                float spd = Vector3.Distance(
                    motion.Vertices[frame][v].position,
                    motion.Vertices[prev][v].position) * motion.Fps;
                speeds[v] = spd;
                if (spd > maxSpeed) maxSpeed = spd;
            }

            return (speeds, maxSpeed);
        }

        // shifts a vertex along its trajectory spline based on motion offset and speed
        private void DisplaceAlongSpline(
            int v, int frame,
            (float[] perVertex, float max) speeds,
            MotionData motion, TrajectoryData traj,
            PipelineConfig config, SmearFrameData output)
        {
            float offset = motion.Vertices[frame][v].motionOffset;
            if (Mathf.Abs(offset) < 0.001f) return;

            // normalize speed to 0..1 range so slower vertices get less displacement
            float normSpeed = speeds.perVertex[v] / speeds.max;
            float beta = offset * config.ElongationMax * normSpeed * config.SmearStrength;

            if (config.ElongatedUseNoise)
            {
                if (offset > 0) return; // noise only on trailing side
                float noise = Mathf.PerlinNoise(v * config.NoiseScale, frame * 0.1f);
                beta *= noise;
            }

            // sample the trajectory spline at a shifted time to get the displaced position
            output.DeformedPositions[frame][v] = _trajEval.Evaluate(traj, v, frame + beta);
        }
    }
}
