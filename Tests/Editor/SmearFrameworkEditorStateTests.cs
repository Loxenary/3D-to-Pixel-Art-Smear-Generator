using NUnit.Framework;
using SmearFramework.DataTypes;
using SmearFramework.Editor;

namespace SmearFramework.Tests
{
    public class SmearFrameworkEditorStateTests
    {
        [Test]
        public void ClearHighResSource_RemovesLoadedFramesAndLabel()
        {
            var state = new SmearFrameworkEditorState
            {
                HighResSourceLabel = "disk",
                AvailableHighRes = new RawFrameData(1, 16, 16),
                AvailableSmearMeta = new SmearFrameData(1, 0)
            };

            state.ClearHighResSource();

            Assert.IsNull(state.HighResSourceLabel);
            Assert.IsNull(state.AvailableHighRes);
            Assert.IsNull(state.AvailableSmearMeta);
            Assert.IsFalse(state.HasHighResSource);
        }

        [Test]
        public void ClearResults_RemovesExportableOutputs()
        {
            var state = new SmearFrameworkEditorState
            {
                LastPixelResult = new SpriteSheetResult(),
                LastSmear3DResultIsTemporary = true
            };

            state.ClearResults();

            Assert.IsNull(state.LastPixelResult);
            Assert.IsNull(state.LastSmear3DResult);
            Assert.IsFalse(state.LastSmear3DResultIsTemporary);
        }

        [Test]
        public void HasHighResSource_FalseWhenNull()
        {
            var state = new SmearFrameworkEditorState();
            Assert.IsFalse(state.HasHighResSource);
        }

        [Test]
        public void HasHighResSource_TrueWhenFramesPresent()
        {
            var state = new SmearFrameworkEditorState
            {
                AvailableHighRes = new RawFrameData(3, 8, 8)
            };
            Assert.IsTrue(state.HasHighResSource);
        }
    }
}
