using NUnit.Framework;
using SmearFramework.DataTypes;
using SmearFramework.Editor;
using SmearFramework.Stages;

namespace SmearFramework.Tests
{
    public class PipelineListControllerTests
    {
        [Test]
        public void NewController_IsEmpty()
        {
            var c = new PipelineListController();
            Assert.AreEqual(0, c.Stages.Count);
        }

        [Test]
        public void AddStage_Appends()
        {
            var c = new PipelineListController();
            c.Add(new VelocityExtractionStage());
            Assert.AreEqual(1, c.Stages.Count);
        }

        [Test]
        public void Remove_RemovesAtIndex()
        {
            var c = new PipelineListController();
            c.Add(new VelocityExtractionStage());
            c.Add(new SmearGenerationStage());
            c.RemoveAt(0);
            Assert.AreEqual(1, c.Stages.Count);
            Assert.IsInstanceOf<SmearGenerationStage>(c.Stages[0]);
        }

        [Test]
        public void Move_SwapsOrder()
        {
            var c = new PipelineListController();
            c.Add(new VelocityExtractionStage());
            c.Add(new SmearGenerationStage());
            c.Move(0, 1);
            Assert.IsInstanceOf<SmearGenerationStage>(c.Stages[0]);
            Assert.IsInstanceOf<VelocityExtractionStage>(c.Stages[1]);
        }

        [Test]
        public void Validate_ReportsErrorWhenInputsMissing()
        {
            var c = new PipelineListController();
            c.Add(new SmearGenerationStage()); // upstream motion/trajectory missing
            var report = c.Validate();
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void Validate_WithPreloads_Passes()
        {
            var c = new PipelineListController();
            c.Add(new PixelizationStage());
            c.SetPreloads(new[] { ArtifactKey.Of<RawFrameData>("frames_highres") });
            var report = c.Validate();
            Assert.IsFalse(report.HasErrors);
        }
    }
}
