using UnityEngine;

namespace SmearFramework
{
    [CreateAssetMenu(fileName = "PipelineConfig", menuName = "Smear Generator/Config/Pipeline Config")]
    public class PipelineConfig : ScriptableObject
    {
        [SerializeField] private VelocityConfig _velocity;
        [SerializeField] private SmearEffectsConfig _smearEffects;
        [SerializeField] private SmearOutputConfig _smearOutput;
        [SerializeField] private OutputConfig _output;
        [SerializeField] private PostProcessConfig _postProcess;
        [SerializeField] private DiagnosticsConfig _diagnostics;
        [SerializeField] private Vector3 _captureCameraEuler;
        // capture is always unlit -- removed serialized light fields
        [SerializeField] private bool _reusePaletteAcrossFrames = true; // build IOKM palette once, LUT-snap all other frames

        public VelocityConfig Velocity => _velocity;
        public SmearEffectsConfig SmearEffects => _smearEffects;
        public SmearOutputConfig SmearOutput => _smearOutput;
        public OutputConfig Output => _output;
        public PostProcessConfig PostProcess => _postProcess;
        public DiagnosticsConfig Diagnostics => _diagnostics;

        // convenience shortcuts
        public int TargetFps => _velocity != null ? _velocity.TargetFps : 12;
        public int TemporalSmoothingWindow => _velocity != null ? _velocity.TemporalSmoothingWindow : 2;
        public float PlaybackSpeed => _velocity != null ? _velocity.PlaybackSpeed : 1f;

        public float MultipleSpeedThreshold => _smearEffects != null ? _smearEffects.MultipleSpeedThreshold : 2f;
        public float SmearStrength => _smearEffects != null ? _smearEffects.SmearStrength : 1f;
        public bool EnableElongated => _smearEffects == null || _smearEffects.EnableElongated;
        public float ElongationMax => _smearEffects != null ? _smearEffects.ElongationMax : 0.5f;
        public bool ElongatedUseNoise => _smearEffects != null && _smearEffects.ElongatedUseNoise;
        public float NoiseScale => _smearEffects != null ? _smearEffects.NoiseScale : 1f;
        public bool EnableMultiples => _smearEffects != null && _smearEffects.EnableMultiples;
        public int PastCopies => _smearEffects != null ? _smearEffects.PastCopies : 2;
        public int FutureCopies => _smearEffects != null ? _smearEffects.FutureCopies : 1;
        public int OverlapCount => _smearEffects != null ? _smearEffects.OverlapCount : 1;
        public float PastDisplacement => _smearEffects != null ? _smearEffects.PastDisplacement : 1f;
        public float FutureDisplacement => _smearEffects != null ? _smearEffects.FutureDisplacement : 1f;
        public float PastOpacityFactor => _smearEffects != null ? _smearEffects.PastOpacityFactor : 0.7f;
        public float FutureOpacityFactor => _smearEffects != null ? _smearEffects.FutureOpacityFactor : 0.7f;
        public bool EnableMotionLines => _smearEffects != null && _smearEffects.EnableMotionLines;
        public float MotionLineSpeedThreshold => _smearEffects != null ? _smearEffects.MotionLineSpeedThreshold : 2f;
        public int MotionLineSeeds => _smearEffects != null ? _smearEffects.MotionLineSeeds : 20;
        public float MotionLineMaxLength => _smearEffects != null ? _smearEffects.MotionLineMaxLength : 1.5f;
        public float MotionLineThickness => _smearEffects != null ? _smearEffects.MotionLineThickness : 0.01f;
        public Color MotionLineColor => _smearEffects != null ? _smearEffects.MotionLineColor : new Color(0, 0, 0, 0.5f);

        public bool ExportSmear3D => _smearOutput == null || _smearOutput.ExportSmear3D;
        public string SmearOutputDirectory => _smearOutput != null ? _smearOutput.OutputDirectory : SmearFrameworkPaths.Output;

        public int OutputResolution => _output != null ? _output.OutputResolution : 64;
        public int CaptureResolution => _output != null ? _output.CaptureResolution : 512;
        public float FixedCaptureOrthoSize => _output != null ? _output.FixedCaptureOrthoSize : 0f;
        public int PaletteSize => _output != null ? _output.PaletteSize : 16;
        public bool EnableOutline => _output != null && _output.EnableOutline;
        public Color OutlineColor => _output != null ? _output.OutlineColor : Color.black;
        public bool SaveHighResToDisk => _output != null && _output.SaveHighResToDisk;
        public string OutputDirectory => _output != null ? _output.OutputDirectory : SmearFrameworkPaths.Output;
        public int PixelsPerUnit => _output != null ? _output.PixelsPerUnit : 32;
        public bool LoopPlayback => _output == null || _output.LoopPlayback;
        public Vector2 PivotNormalized => _output != null ? _output.PivotNormalized : new Vector2(0.5f, 0f);
        public string Prefix => _output != null ? _output.Prefix : "";
        public string Suffix => _output != null ? _output.Suffix : "";
        public Color SmearOverlayColor => Color.clear;
        public Vector3 CaptureCameraEuler => _captureCameraEuler;
        public bool CaptureUnlit => true;
        public bool CaptureLightEnabled => false;
        public bool ReusePaletteAcrossFrames => _reusePaletteAcrossFrames;

        public float FlickerThreshold => _postProcess != null ? _postProcess.FlickerThreshold : 5f;
        public int PixelWidth => _output != null ? _output.OutputResolution : (_postProcess != null ? _postProcess.PixelWidth : 64);
        public int PixelHeight => _output != null ? _output.OutputResolution : (_postProcess != null ? _postProcess.PixelHeight : 64);
        public int EmIterations => _postProcess != null ? _postProcess.EmIterations : 5;
        public int PostProcessPaletteSize => _postProcess != null ? _postProcess.PaletteSize : 16;
        public Color[] PaletteLUT => _postProcess != null ? _postProcess.PaletteLUT : null;

        public bool ExportDiagnostics => _diagnostics != null && _diagnostics.ExportDiagnostics;
        public string DiagnosticsPath => _diagnostics != null ? _diagnostics.DiagnosticsPath : SmearFrameworkPaths.DiagnosticsOutput + "/";
    }
}
