using System.Linq;
using NUnit.Framework;
using SmearFramework.DataTypes;
using SmearFramework.Stages;

namespace SmearFramework.Tests
{
    public class SpriteSheetExportStageDeclarationTests
    {
        [Test]
        public void ConsumesFramesHighresOrPixelized()
        {
            var stage = new SpriteSheetExportStage();
            Assert.IsTrue(stage.InputKey.Any(k => k.Key == "frames_pixelized" && k.Type == typeof(RawFrameData)));
        }

        [Test]
        public void ProducesSpriteSheet()
        {
            var stage = new SpriteSheetExportStage();
            Assert.IsTrue(stage.OutputKey.Any(k => k.Key == "sprite_sheet" && k.Type == typeof(SpriteSheetResult)));
        }
    }
}
