using SmearFramework.DataTypes;
using UnityEngine;

namespace SmearFramework.Editor
{
    public sealed class SmearFrameworkEditorState
    {
        public string HighResSourceLabel;
        public RawFrameData AvailableHighRes;
        public SmearFrameData AvailableSmearMeta;
        public SpriteSheetResult LastPixelResult;
        public SmearScene3DExporter.Result LastSmear3DResult;
        public bool LastSmear3DResultIsTemporary;
        public string ValidationStatus;

        // True when Workflow 2 can read high-res frames without capturing from the character again.
        public bool HasHighResSource => AvailableHighRes != null && AvailableHighRes.FrameCount > 0;

        // Clears preloaded high-res input and metadata after the user removes the disk/session source.
        public void ClearHighResSource()
        {
            HighResSourceLabel = null;
            AvailableHighRes = null;
            AvailableSmearMeta = null;
        }

        // Clears outputs when a new run starts or stale results should stop driving export actions.
        public void ClearResults()
        {
            LastPixelResult = null;
            LastSmear3DResult = null;
            LastSmear3DResultIsTemporary = false;
        }
    }
}
