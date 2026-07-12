using System.Collections.Generic;
using SmearFramework.Stages;

namespace SmearFramework
{
    // Three canned pipelines the window can run.
    //   Full         -- bake from a 3D character all the way to a pixel-art sheet
    //   SmearBakeOnly -- stop after smear generation, write the 3D smear prefab from the editor
    //   PixelArtOnly  -- capture from 3D or use preloaded frames, then run pixelization + export
    public enum PipelineMode
    {
        Full,
        SmearBakeOnly,
        PixelArtOnly,
    }

    public static class PipelinePresets
    {
        // build the stage list for a mode.
        public static List<IPipelineStage> Build(PipelineMode mode)
        {
            switch (mode)
            {
                case PipelineMode.Full:
                    return new List<IPipelineStage>
                    {
                        new SmearBakePhase(),
                        new PixelizationPhase(),
                    };

                case PipelineMode.SmearBakeOnly:
                    return new List<IPipelineStage> { new SmearBakePhase() };

                case PipelineMode.PixelArtOnly:
                    return new List<IPipelineStage> { new PixelizationPhase() };

                default:
                    return new List<IPipelineStage>();
            }
        }

        // human-readable label for the dropdown
        public static string DisplayName(PipelineMode mode)
        {
            switch (mode)
            {
                case PipelineMode.Full: return "Full (smear -> pixel art)";
                case PipelineMode.SmearBakeOnly: return "Smear frame only (3D output)";
                case PipelineMode.PixelArtOnly: return "Pixel art only (3D or preloaded frames)";
                default: return mode.ToString();
            }
        }

        // PixelArtOnly can capture from 3D now; preload is optional.
        public static bool RequiresHighResPreload(PipelineMode mode)
            => false;
    }
}
