using System.Collections.Generic;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.SmearGeneration;

namespace SmearFramework.Stages
{
    // notes: tremolieres-2025-trajectory-aware-smears.md, basset-2024-smear-stylized-motion.md
    /// <summary>
    /// Runs all enabled smear strategies on each frame, then marks the frame as smeared only if the output actually changed. Outputs SmearFrameData with displaced vertex positions and per-frame intensity.
    /// </summary>
    [InternalStage]
    public class SmearGenerationStage : IPipelineStage
    {
        private string _name = "Smear Generation";
        private List<ISmearStrategy> _strategies;

        public string Name => _name;
        public IReadOnlyList<ArtifactKey> InputKey => new[]
        {
            ArtifactKey.Of<MotionData>("motion"),
            ArtifactKey.Of<TrajectoryData>("trajectory"),
        };
        public IReadOnlyList<ArtifactKey> OutputKey => new[]
        {
            ArtifactKey.Of<SmearFrameData>("smear_data"),
        };

        // Registers the smear passes in execution order.
        public SmearGenerationStage()
        {
            _strategies = new List<ISmearStrategy>
            {
                new ElongatedSmearStrategy(),
                new MultipleSmearStrategy(),
                new MotionLineStrategy()
            };
        }

        // Runs the enabled smear passes across the whole clip.
        public void Execute(PipelineContext ctx)
        {
            var motion = ctx.Get<MotionData>("motion");
            SmearFrameData output = new SmearFrameData(motion.FrameCount, motion.VertexCount);

            for (int f = 0; f < motion.FrameCount; f++)
                ProcessFrame(ctx, motion, output, f);

            ctx.Set("smear_data", output);

            if (ctx.Diagnostics != null)
                WriteDiagnostics(ctx, output);
        }

        // Copy the base pose, run the strategies, then check whether the frame actually changed
        private void ProcessFrame(PipelineContext ctx, MotionData motion, SmearFrameData output, int f)
        {
            CopyBasePositions(motion, output, f);
            ApplyStrategies(ctx, output, f);

            bool hasSmear = FrameHasSmear(motion, output, f);
            output.HasSmear[f] = hasSmear;
            output.SmearIntensity[f] = hasSmear ? ComputeSmearIntensity(motion, f, ctx.Config.SmearStrength) : 0f;
        }

        // Seeds the deformed buffer with the base pose.
        private void CopyBasePositions(MotionData motion, SmearFrameData output, int f)
        {
            for (int v = 0; v < motion.VertexCount; v++)
                output.DeformedPositions[f][v] = motion.Vertices[f][v].position;
        }

        // Runs each enabled smear strategy for this frame.
        private void ApplyStrategies(PipelineContext ctx, SmearFrameData output, int f)
        {
            foreach (var strat in _strategies)
            {
                if (strat.IsEnabled(ctx.Config))
                    strat.Apply(ctx, output, f);
            }
        }

        // A frame counts as smeared if verts moved or extra geometry was generated.
        private bool FrameHasSmear(MotionData motion, SmearFrameData output, int f)
        {
            var ghosts = output.AdditionalGeometry[f];
            if (ghosts != null && ghosts.vertexCount > 0)
                return true;

            var lines = output.MotionLineGeometry[f];
            if (lines != null && lines.vertexCount > 0)
                return true;

            for (int v = 0; v < motion.VertexCount; v++)
            {
                if ((output.DeformedPositions[f][v] - motion.Vertices[f][v].position).sqrMagnitude > 0.000001f)
                    return true;
            }

            return false;
        }

        // Average motion offset across vertices that actually moved, then scale it for the artist-facing strength meter.
        private float ComputeSmearIntensity(MotionData motion, int f, float smearStrength)
        {
            float sum = 0f;
            int count = 0;
            for (int v = 0; v < motion.VertexCount; v++)
            {
                float d = Mathf.Abs(motion.Vertices[f][v].motionOffset);
                if (d > 0.01f) { sum += d; count++; }
            }
            float baseIntensity = count > 0 ? sum / count : 0f;
            return Mathf.Clamp01(baseIntensity * smearStrength);
        }



        // Writes per-frame smear diagnostics.
        private void WriteDiagnostics(PipelineContext ctx, SmearFrameData output)
        {
            var diag = ctx.Diagnostics;
            diag.SmearTypePerFrame = new string[output.FrameCount];
            diag.SmearIntensityPerFrame = new float[output.FrameCount];

            for (int f = 0; f < output.FrameCount; f++)
            {
                diag.SmearIntensityPerFrame[f] = output.SmearIntensity[f];
                if (!output.HasSmear[f])
                {
                    diag.SmearTypePerFrame[f] = "none";
                    continue;
                }

                var types = new List<string>();
                if (ctx.Config.EnableElongated) types.Add("elongated");
                if (ctx.Config.EnableMultiples) types.Add("multiple");
                if (ctx.Config.EnableMotionLines) types.Add("motionline");
                diag.SmearTypePerFrame[f] = types.Count > 0 ? string.Join("+", types) : "none";
            }
        }
    }
}
