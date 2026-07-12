using UnityEngine;

namespace SmearFramework.DataTypes
{
    /// <summary>
    /// Collects intermediate textures, heatmaps, and per-frame metrics from each pipeline stage. Used by DiagnosticsExporter to dump debug artifacts when diagnostics are enabled.
    /// </summary>
    public class DiagnosticsData
    {
        private string _outputFolder;

        #region Velocity Extraction
        private Texture2D[] _motionOffsetHeatmaps;
        private float[] _maxVelocityPerFrame;
        private FrameBuffer<Vector3> _boneVelocityVectors;

        public string OutputFolder { get => _outputFolder; set => _outputFolder = value; }
        public Texture2D[] MotionOffsetHeatmaps { get => _motionOffsetHeatmaps; set => _motionOffsetHeatmaps = value; }
        public float[] MaxVelocityPerFrame { get => _maxVelocityPerFrame; set => _maxVelocityPerFrame = value; }
        public FrameBuffer<Vector3> BoneVelocityVectors { get => _boneVelocityVectors; set => _boneVelocityVectors = value; }
        #endregion

        #region Smear Generation
        private Texture2D[] _baseVsSmeared;
        private string[] _smearTypePerFrame;
        private float[] _smearIntensityPerFrame;

        public Texture2D[] BaseVsSmeared { get => _baseVsSmeared; set => _baseVsSmeared = value; }
        public string[] SmearTypePerFrame { get => _smearTypePerFrame; set => _smearTypePerFrame = value; }
        public float[] SmearIntensityPerFrame { get => _smearIntensityPerFrame; set => _smearIntensityPerFrame = value; }
        #endregion

        #region Pixel Art Conversion
        private Texture2D[] _highResCaptures;
        private Texture2D[] _afterOutline;
        private Texture2D[] _afterDownscale;

        public Texture2D[] HighResCaptures { get => _highResCaptures; set => _highResCaptures = value; }
        public Texture2D[] AfterOutline { get => _afterOutline; set => _afterOutline = value; }
        public Texture2D[] AfterDownscale { get => _afterDownscale; set => _afterDownscale = value; }
        #endregion

        #region Post-Processing
        private Texture2D[] _preQuantize;
        private Texture2D[] _postQuantize;
        private Texture2D[] _flickerMaps;
        private Color[] _finalPalette;
        private Texture2D _paletteSwatch;

        public Texture2D[] PreQuantize { get => _preQuantize; set => _preQuantize = value; }
        public Texture2D[] PostQuantize { get => _postQuantize; set => _postQuantize = value; }
        public Texture2D[] FlickerMaps { get => _flickerMaps; set => _flickerMaps = value; }
        public Color[] FinalPalette { get => _finalPalette; set => _finalPalette = value; }
        public Texture2D PaletteSwatch { get => _paletteSwatch; set => _paletteSwatch = value; }
        #endregion
    }
}
