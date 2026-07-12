using System.Collections.Generic;
using SmearFramework.DataTypes;

namespace SmearFramework.Stages
{
    // Phase 1 of the framework. Produces the editable 3D smear data. Pixelization owns image capture,
    // so Smear Frame can stand alone as a 3D output instead of acting as a hidden PNG producer.
    /// <summary>Runs Velocity Extraction and Smear Generation as a single smear step.</summary>
    public class SmearBakePhase : IPipelineStage
    {
        public string Name => "Smear Frame";

        public IReadOnlyList<ArtifactKey> InputKey => System.Array.Empty<ArtifactKey>();

        // Keep all bake outputs visible to downstream stages.
        public IReadOnlyList<ArtifactKey> OutputKey => new[]
        {
            ArtifactKey.Of<MotionData>("motion"),
            ArtifactKey.Of<TrajectoryData>("trajectory"),
            ArtifactKey.Of<SmearFrameData>("smear_data"),
        };

        public void Execute(PipelineContext ctx)
        {
            // Only reuse cached motion when the cached buffers still look intact.
            if (!HasUsableMotion(ctx))
                new VelocityExtractionStage().Execute(ctx);

            new SmearGenerationStage().Execute(ctx);
        }

        private static bool HasUsableMotion(PipelineContext ctx)
        {
            if (!ctx.Has("motion") || !ctx.Has("trajectory")) return false;
            var m = ctx.Get<MotionData>("motion");
            var t = ctx.Get<TrajectoryData>("trajectory");
            if (m == null || t == null) return false;

            // Domain reloads can leave the outer buffer alive but clear inner arrays.
            try
            {
                return m.FrameCount > 0 && m.BoneCount > 0
                    && m.Bones != null && m.Bones[0] != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
