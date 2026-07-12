using System.Collections.Generic;
using SmearFramework.DataTypes;

namespace SmearFramework.Editor
{
    public sealed class SmearFrameworkEditorController
    {
        private readonly PipelineListController _pipeline;

        public PipelineListController Pipeline => _pipeline;
        public PipelineMode CurrentMode { get; private set; }

        // Owns workflow preset, stage-list cloning, and validation decisions for the editor window.
        public SmearFrameworkEditorController(PipelineListController pipeline = null, PipelineMode initialMode = PipelineMode.Full)
        {
            _pipeline = pipeline ?? new PipelineListController();
            ApplyPreset(initialMode);
        }

        // Replaces the stage list with a known workflow preset.
        public void ApplyPreset(PipelineMode mode)
        {
            CurrentMode = mode;
            _pipeline.Replace(PipelinePresets.Build(mode));
        }

        // Clones the current stage list so a run cannot mutate the editor list.
        public List<IPipelineStage> CloneStages()
        {
            var copy = new List<IPipelineStage>(_pipeline.Stages.Count);
            foreach (var stage in _pipeline.Stages)
                copy.Add(StageRegistry.Instantiate(stage.GetType()));
            return copy;
        }

        // Validates the current stage list against either a live/preloaded high-res source or no source.
        public ValidationReport Validate(bool hasHighResSource)
        {
            var preloaded = hasHighResSource
                ? new[] { ArtifactKey.Of<RawFrameData>("frames_highres") }
                : null;
            _pipeline.SetPreloads(preloaded);
            return _pipeline.Validate();
        }
    }
}
