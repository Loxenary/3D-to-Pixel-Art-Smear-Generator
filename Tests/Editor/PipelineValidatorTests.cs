using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace SmearFramework.Tests
{
    public class PipelineValidatorTests
    {
        // tiny fake stages just for validator tests
        private class StageA : IPipelineStage
        {
            public string Name => "A";
            public IReadOnlyList<ArtifactKey> InputKey => System.Array.Empty<ArtifactKey>();
            public IReadOnlyList<ArtifactKey> OutputKey => new[] { ArtifactKey.Of<int>("x") };
            public void Execute(PipelineContext ctx) { }
        }

        private class StageB : IPipelineStage
        {
            public string Name => "B";
            public IReadOnlyList<ArtifactKey> InputKey => new[] { ArtifactKey.Of<int>("x") };
            public IReadOnlyList<ArtifactKey> OutputKey => new[] { ArtifactKey.Of<string>("y") };
            public void Execute(PipelineContext ctx) { }
        }

        private class StageB_WrongType : IPipelineStage
        {
            public string Name => "B-wrong";
            public IReadOnlyList<ArtifactKey> InputKey => new[] { ArtifactKey.Of<string>("x") };
            public IReadOnlyList<ArtifactKey> OutputKey => System.Array.Empty<ArtifactKey>();
            public void Execute(PipelineContext ctx) { }
        }

        [Test]
        public void EmptyPipeline_Warning()
        {
            var report = PipelineValidator.Validate(new List<IPipelineStage>(), preloadedKeys: null);
            Assert.IsFalse(report.HasErrors);
            Assert.IsTrue(report.Issues.Any(i => i.Severity == ValidationSeverity.Warning && i.Message.Contains("empty")));
        }

        [Test]
        public void MissingInput_Error()
        {
            var stages = new List<IPipelineStage> { new StageB() };
            var report = PipelineValidator.Validate(stages, preloadedKeys: null);
            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Issues.Any(i => i.Severity == ValidationSeverity.Error && i.Message.Contains("x")));
        }

        [Test]
        public void MissingInput_SatisfiedByPreload_NoError()
        {
            var stages = new List<IPipelineStage> { new StageB() };
            var preloads = new[] { ArtifactKey.Of<int>("x") };
            var report = PipelineValidator.Validate(stages, preloadedKeys: preloads);
            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void TypeMismatch_Error()
        {
            var stages = new List<IPipelineStage> { new StageA(), new StageB_WrongType() };
            var report = PipelineValidator.Validate(stages, preloadedKeys: null);
            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Issues.Any(i => i.Message.Contains("type") && i.Message.Contains("x")));
        }

        [Test]
        public void DuplicateProducer_Error()
        {
            var stages = new List<IPipelineStage> { new StageA(), new StageA() };
            var report = PipelineValidator.Validate(stages, preloadedKeys: null);
            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(report.Issues.Any(i => i.Message.Contains("produced by both") || i.Message.Contains("Duplicate")));
        }

        [Test]
        public void OrphanOutput_Warning()
        {
            var stages = new List<IPipelineStage> { new StageA() };
            var report = PipelineValidator.Validate(stages, preloadedKeys: null);
            Assert.IsFalse(report.HasErrors);
            Assert.IsTrue(report.Issues.Any(i => i.Severity == ValidationSeverity.Warning && i.Message.Contains("orphan")));
        }

        [Test]
        public void ValidChain_NoErrors()
        {
            var stages = new List<IPipelineStage> { new StageA(), new StageB() };
            var report = PipelineValidator.Validate(stages, preloadedKeys: null);
            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void Reorder_ProducerAfterConsumer_Error()
        {
            var stages = new List<IPipelineStage> { new StageB(), new StageA() };
            var report = PipelineValidator.Validate(stages, preloadedKeys: null);
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void Preload_And_StageProducer_StageWins_Compatible()
        {
            // both preload and StageA produce "x" as int -- should NOT flag as duplicate
            // because preload is treated as "already available", not as a competing producer
            var stages = new List<IPipelineStage> { new StageA(), new StageB() };
            var preloads = new[] { ArtifactKey.Of<int>("x") };
            var report = PipelineValidator.Validate(stages, preloadedKeys: preloads);
            Assert.IsFalse(report.HasErrors);
        }
    }
}
