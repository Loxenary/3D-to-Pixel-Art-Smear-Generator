using System.Linq;
using NUnit.Framework;
using SmearFramework.DataTypes;
using SmearFramework.Stages;

namespace SmearFramework.Tests
{
    public class VelocityExtractionStageDeclarationTests
    {
        [Test]
        public void HasNoInputs()
        {
            var stage = new VelocityExtractionStage();
            Assert.AreEqual(0, stage.InputKey.Count);
        }

        [Test]
        public void ProducesMotionAndTrajectory()
        {
            var stage = new VelocityExtractionStage();
            Assert.IsTrue(stage.OutputKey.Any(k => k.Key == "motion" && k.Type == typeof(MotionData)));
            Assert.IsTrue(stage.OutputKey.Any(k => k.Key == "trajectory" && k.Type == typeof(TrajectoryData)));
        }
    }
}
