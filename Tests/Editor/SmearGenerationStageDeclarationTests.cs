using System.Linq;
using NUnit.Framework;
using SmearFramework.DataTypes;
using SmearFramework.Stages;

namespace SmearFramework.Tests
{
    public class SmearGenerationStageDeclarationTests
    {
        [Test]
        public void ConsumesMotionAndTrajectory()
        {
            var stage = new SmearGenerationStage();
            Assert.IsTrue(stage.InputKey.Any(k => k.Key == "motion" && k.Type == typeof(MotionData)));
            Assert.IsTrue(stage.InputKey.Any(k => k.Key == "trajectory" && k.Type == typeof(TrajectoryData)));
        }

        [Test]
        public void ProducesSmearData()
        {
            var stage = new SmearGenerationStage();
            Assert.IsTrue(stage.OutputKey.Any(k => k.Key == "smear_data" && k.Type == typeof(SmearFrameData)));
        }
    }
}
