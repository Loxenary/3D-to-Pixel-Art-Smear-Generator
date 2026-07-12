using System.Collections.Generic;

namespace SmearFramework
{
    public interface IPipelineStage
    {
        string Name { get; }
        IReadOnlyList<ArtifactKey> InputKey { get; }
        IReadOnlyList<ArtifactKey> OutputKey { get; }
        void Execute(PipelineContext ctx);
    }
}
