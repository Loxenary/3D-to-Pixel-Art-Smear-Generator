using System.Collections.Generic;

namespace SmearFramework.Editor
{
    // Testable stage-list model behind the editor window.
    public class PipelineListController
    {
        private List<IPipelineStage> _stages = new List<IPipelineStage>();
        private List<ArtifactKey> _preloads = new List<ArtifactKey>();

        public IReadOnlyList<IPipelineStage> Stages => _stages;
        public IReadOnlyList<ArtifactKey> Preloads => _preloads;

        // ReorderableList binds to the live list through this view.
        internal IList<IPipelineStage> StagesForBinding => _stages;

        // Adds a stage to the end of the list.
        public void Add(IPipelineStage stage)
        {
            _stages.Add(stage);
        }

        // Swaps the list contents in place so bindings stay valid.
        public void Replace(IEnumerable<IPipelineStage> stages)
        {
            _stages.Clear();
            if (stages == null) return;
            foreach (var s in stages) _stages.Add(s);
        }

        // Removes the stage at the given index.
        public void RemoveAt(int index)
        {
            _stages.RemoveAt(index);
        }

        // Moves one stage to a new slot.
        public void Move(int from, int to)
        {
            var s = _stages[from];
            _stages.RemoveAt(from);
            _stages.Insert(to, s);
        }

        // Replaces the preload set used by Validate.
        public void SetPreloads(IReadOnlyList<ArtifactKey> preloads)
        {
            _preloads.Clear();
            if (preloads != null) _preloads.AddRange(preloads);
        }

        // Validates the current stage list against the preload set.
        public ValidationReport Validate()
        {
            return PipelineValidator.Validate(_stages, _preloads);
        }
    }
}
