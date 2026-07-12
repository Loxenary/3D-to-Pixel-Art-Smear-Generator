using UnityEngine;

namespace SmearFramework
{
    /// <summary>
    /// Controls whether the pipeline exports debug artifacts (heatmaps, CSVs, per-frame diffs). Separated from PipelineConfig so diagnostics can be toggled without touching bake settings.
    /// </summary>
    [CreateAssetMenu(fileName = "DiagnosticsConfig", menuName = "Smear Generator/Config/Diagnostics")]
    public class DiagnosticsConfig : ScriptableObject
    {
        [Tooltip("Exports intermediate files (per-frame PNGs, velocity CSVs, kernel JSON) to disk after each bake. Useful for debugging the pipeline -- disable during normal use, bake will be noticeably faster.")]
        [SerializeField] private bool _exportDiagnostics = false;

        [Tooltip("Folder where debug exports land. A timestamped subfolder is created per bake run so runs don't overwrite each other. Must start with Assets/.")]
        [SerializeField] private string _diagnosticsPath = SmearFrameworkPaths.DiagnosticsOutput + "/";

        public bool ExportDiagnostics => _exportDiagnostics;
        public string DiagnosticsPath => _diagnosticsPath;
    }
}
