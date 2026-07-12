using System.Linq;
using NUnit.Framework;
using SmearFramework.Stages;

namespace SmearFramework.Tests
{
    public class PipelinePresetsTests
    {
        [Test]
        public void Full_HasSmearBakeThenPixelization()
        {
            var stages = PipelinePresets.Build(PipelineMode.Full);
            Assert.AreEqual(2, stages.Count);
            Assert.IsInstanceOf<SmearBakePhase>(stages[0]);
            Assert.IsInstanceOf<PixelizationPhase>(stages[1]);
        }

        [Test]
        public void SmearBakeOnly_HasOnlySmearBake()
        {
            var stages = PipelinePresets.Build(PipelineMode.SmearBakeOnly);
            Assert.AreEqual(1, stages.Count);
            Assert.IsInstanceOf<SmearBakePhase>(stages[0]);
        }

        [Test]
        public void PixelArtOnly_HasOnlyPixelization()
        {
            var stages = PipelinePresets.Build(PipelineMode.PixelArtOnly);
            Assert.AreEqual(1, stages.Count);
            Assert.IsInstanceOf<PixelizationPhase>(stages[0]);
        }

        [Test]
        public void PixelArtOnly_DoesNotRequirePreload()
        {
            Assert.IsFalse(PipelinePresets.RequiresHighResPreload(PipelineMode.PixelArtOnly));
            Assert.IsFalse(PipelinePresets.RequiresHighResPreload(PipelineMode.Full));
            Assert.IsFalse(PipelinePresets.RequiresHighResPreload(PipelineMode.SmearBakeOnly));
        }

        [Test]
        public void PixelArtOnly_PassesValidation_WithoutPreload()
        {
            var stages = PipelinePresets.Build(PipelineMode.PixelArtOnly);
            var report = PipelineValidator.Validate(stages, preloadedKeys: null);
            Assert.IsFalse(report.HasErrors, "Pixelization captures from 3D when frames_highres is not preloaded");
        }

        [Test]
        public void PixelArtOnly_PassesValidation_WithPreload()
        {
            var stages = PipelinePresets.Build(PipelineMode.PixelArtOnly);
            var preloaded = new[] { ArtifactKey.Of<DataTypes.RawFrameData>("frames_highres") };
            var report = PipelineValidator.Validate(stages, preloaded);
            Assert.IsFalse(report.HasErrors, "expected no errors when frames_highres is preloaded");
        }

        [Test]
        public void Full_PassesValidation_WithoutPreload()
        {
            var stages = PipelinePresets.Build(PipelineMode.Full);
            var report = PipelineValidator.Validate(stages, preloadedKeys: null);
            Assert.IsFalse(report.HasErrors, "Full pipeline should be self-contained");
        }

        [Test]
        public void StageRegistry_UserFacing_OnlyShowsTwoPhases()
        {
            var types = StageRegistry.GetStageTypes();
            Assert.AreEqual(2, types.Count, "user-facing dropdown should expose only the two phases");
            CollectionAssert.Contains(types, typeof(SmearBakePhase));
            CollectionAssert.Contains(types, typeof(PixelizationPhase));
        }
    }
}
