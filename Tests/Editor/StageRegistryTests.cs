using System.Linq;
using NUnit.Framework;
using SmearFramework.Stages;

namespace SmearFramework.Tests
{
    public class StageRegistryTests
    {
        [Test]
        public void GetAllStageTypes_DiscoversInternalsAndPhases()
        {
            var all = StageRegistry.GetAllStageTypes().ToList();
            Assert.Contains(typeof(VelocityExtractionStage), all);
            Assert.Contains(typeof(SmearGenerationStage), all);
            Assert.Contains(typeof(HighResCaptureStage), all);
            Assert.Contains(typeof(PixelizationStage), all);
            Assert.Contains(typeof(SpriteSheetExportStage), all);
            Assert.Contains(typeof(SmearBakePhase), all);
            Assert.Contains(typeof(PixelizationPhase), all);
        }

        [Test]
        public void GetStageTypes_ExcludesInternalStages()
        {
            var userFacing = StageRegistry.GetStageTypes().ToList();
            CollectionAssert.DoesNotContain(userFacing, typeof(VelocityExtractionStage));
            CollectionAssert.DoesNotContain(userFacing, typeof(SmearGenerationStage));
            CollectionAssert.DoesNotContain(userFacing, typeof(HighResCaptureStage));
            CollectionAssert.DoesNotContain(userFacing, typeof(PixelizationStage));
            CollectionAssert.DoesNotContain(userFacing, typeof(SpriteSheetExportStage));
            CollectionAssert.Contains(userFacing, typeof(SmearBakePhase));
            CollectionAssert.Contains(userFacing, typeof(PixelizationPhase));
        }

        [Test]
        public void ExcludesAbstractTypes()
        {
            var types = StageRegistry.GetStageTypes();
            foreach (var t in types)
                Assert.IsFalse(t.IsAbstract, $"{t.Name} should not be abstract");
        }

        [Test]
        public void OrderingIsAlphabeticalByName()
        {
            var names = StageRegistry.GetStageTypes().Select(t => t.Name).ToList();
            var sorted = names.OrderBy(n => n).ToList();
            CollectionAssert.AreEqual(sorted, names);
        }

        [Test]
        public void Instantiate_ReturnsIPipelineStage()
        {
            var stage = StageRegistry.Instantiate(typeof(SmearBakePhase));
            Assert.IsNotNull(stage);
            Assert.IsInstanceOf<IPipelineStage>(stage);
        }
    }
}
