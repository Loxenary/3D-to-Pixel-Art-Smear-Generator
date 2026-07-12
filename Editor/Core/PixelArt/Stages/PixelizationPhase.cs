using System.Collections.Generic;
using SmearFramework.DataTypes;

namespace SmearFramework.Stages
{
    // Phase 2 of the framework. Owns high-res capture, content-adaptive downscale + IOKM palette
    // quantize, and the sprite sheet writer so the artist sees one pixel-art conversion step.
    /// <summary>Captures frames when needed, then runs Pixelization and Sprite Sheet Export.</summary>
    public class PixelizationPhase : IPipelineStage
    {
        public string Name => "Pixelization";

        public IReadOnlyList<ArtifactKey> InputKey => System.Array.Empty<ArtifactKey>();

        public IReadOnlyList<ArtifactKey> OutputKey => new[]
        {
            ArtifactKey.Of<RawFrameData>("frames_pixelized"),
            ArtifactKey.Of<SpriteSheetResult>("sprite_sheet"),
        };

        public void Execute(PipelineContext ctx)
        {
            if (!HasUsableHighResFrames(ctx))
                new HighResCaptureStage().Execute(ctx);

            new PixelizationStage().Execute(ctx);
            new SpriteSheetExportStage().Execute(ctx);
        }

        // Check whether a loaded or upstream high-res frame set can feed PixelizationStage.
        private static bool HasUsableHighResFrames(PipelineContext ctx)
        {
            if (!ctx.Has("frames_highres")) return false;
            var frames = ctx.Get<RawFrameData>("frames_highres");
            return frames != null && frames.FrameCount > 0 && frames.Frames != null;
        }
    }
}
