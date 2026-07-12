using NUnit.Framework;
using SmearFramework.Editor;
using SmearFramework.Stages;

namespace SmearFramework.Tests
{
    public class SmearFrameworkEditorControllerTests
    {
        [Test]
        public void ApplyPreset_PixelArtOnly_UpdatesModeAndStages()
        {
            var controller = new SmearFrameworkEditorController();

            controller.ApplyPreset(PipelineMode.PixelArtOnly);

            Assert.AreEqual(PipelineMode.PixelArtOnly, controller.CurrentMode);
            Assert.AreEqual(1, controller.Pipeline.Stages.Count);
            Assert.IsInstanceOf<PixelizationPhase>(controller.Pipeline.Stages[0]);
        }

        [Test]
        public void CloneStages_ReturnsNewStageInstances()
        {
            var controller = new SmearFrameworkEditorController(null, PipelineMode.Full);

            var cloned = controller.CloneStages();

            Assert.AreEqual(controller.Pipeline.Stages.Count, cloned.Count);
            Assert.IsInstanceOf<SmearBakePhase>(cloned[0]);
            Assert.IsInstanceOf<PixelizationPhase>(cloned[1]);
            Assert.AreNotSame(controller.Pipeline.Stages[0], cloned[0]);
            Assert.AreNotSame(controller.Pipeline.Stages[1], cloned[1]);
        }

        [Test]
        public void Validate_WithHighResSource_AddsFramesHighResPreload()
        {
            var controller = new SmearFrameworkEditorController(null, PipelineMode.PixelArtOnly);

            var report = controller.Validate(hasHighResSource: true);

            Assert.IsFalse(report.HasErrors);
            Assert.AreEqual(1, controller.Pipeline.Preloads.Count);
            Assert.AreEqual("frames_highres", controller.Pipeline.Preloads[0].Key);
        }

        [Test]
        public void Validate_WithoutHighResSource_ClearsPreloads()
        {
            var controller = new SmearFrameworkEditorController(null, PipelineMode.PixelArtOnly);

            controller.Validate(hasHighResSource: true);
            var report = controller.Validate(hasHighResSource: false);

            Assert.IsFalse(report.HasErrors);
            Assert.AreEqual(0, controller.Pipeline.Preloads.Count);
        }
    }
}
