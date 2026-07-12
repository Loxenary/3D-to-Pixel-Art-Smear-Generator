using UnityEngine;
using System.Collections.Generic;
using SmearFramework.DataTypes;
using SmearFramework.VelocityExtraction;

namespace SmearFramework.Stages
{
    // notes: rohmer-2021-velocity-skinning.md, basset-2024-smear-stylized-motion.md
    [InternalStage]
    public class VelocityExtractionStage : IPipelineStage
    {
        private string _name = "Velocity Extraction";

        public string Name => _name;

        public IReadOnlyList<ArtifactKey> InputKey => System.Array.Empty<ArtifactKey>();
        public IReadOnlyList<ArtifactKey> OutputKey => new[]
        {
            ArtifactKey.Of<MotionData>("motion"),
            ArtifactKey.Of<TrajectoryData>("trajectory"),
        };

        // Sample animation, compute velocities, propagate weights, and build trajectory splines
        public void Execute(PipelineContext ctx)
        {
            var sampler = new AnimationSampler();
            var motionData = sampler.Sample(ctx.Target, ctx.Clip, ctx.Config);

            var velComputer = new BoneVelocityComputer();
            velComputer.Compute(motionData);

            // scale velocities to simulate faster/slower playback -- higher speed = more frames exceed SpeedThreshold
            float playbackSpeed = ctx.Config.PlaybackSpeed;
            if (UnityEngine.Mathf.Abs(playbackSpeed - 1f) > 0.001f)
                velComputer.ScaleVelocities(motionData, playbackSpeed);

            var propagated = velComputer.ComputePropagatedWeights(
                motionData.SkinWeights,
                motionData.ParentBoneIndex,
                motionData.BoneCount,
                motionData.VertexCount);
            motionData.SetPropagatedWeights(propagated);

            var offsetComputer = new MotionOffsetComputer();
            offsetComputer.Compute(motionData, ctx.Config);

            var trajBuilder = new TrajectoryBuilder();
            var trajectoryData = trajBuilder.Build(motionData);

            ctx.Set("motion", motionData);
            ctx.Set("trajectory", trajectoryData);

            if (ctx.Diagnostics != null)
                PopulateDiagnostics(ctx, motionData);
        }

        // Record the peak bone velocity for each frame into diagnostics
        private void PopulateDiagnostics(PipelineContext ctx, MotionData data)
        {
            var diag = ctx.Diagnostics;

            diag.MaxVelocityPerFrame = new float[data.FrameCount];
            for (int f = 0; f < data.FrameCount; f++)
            {
                float max = 0f;
                for (int b = 0; b < data.BoneCount; b++)
                {
                    float mag = data.Bones[f][b].linearVelocity.magnitude;
                    if (mag > max) max = mag;
                }
                diag.MaxVelocityPerFrame[f] = max;
            }
        }
    }
}
