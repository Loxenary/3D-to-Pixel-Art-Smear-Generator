using System.Linq;
using NUnit.Framework;
using SmearFramework.DataTypes;
using SmearFramework.Stages;

namespace SmearFramework.Tests
{
    public class PixelizationStageDeclarationTests
    {
        [Test]
        public void ConsumesFramesHighres()
        {
            var stage = new PixelizationStage();
            Assert.IsTrue(stage.InputKey.Any(k => k.Key == "frames_highres" && k.Type == typeof(RawFrameData)));
        }

        [Test]
        public void ProducesFramesPixelized()
        {
            var stage = new PixelizationStage();
            Assert.IsTrue(stage.OutputKey.Any(k => k.Key == "frames_pixelized" && k.Type == typeof(RawFrameData)));
        }
    }
}
