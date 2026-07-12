using UnityEngine;

namespace SmearFramework
{
    /// <summary>3D smear output settings used when the Smear Frame phase runs by itself.</summary>
    [CreateAssetMenu(fileName = "SmearOutputConfig", menuName = "Smear Framework/Config/Smear Output")]
    public class SmearOutputConfig : ScriptableObject
    {
        [Tooltip("Where the 3D smear prefab is saved. Must start with Assets/.")]
        [SerializeField] private string _outputDirectory = SmearFrameworkPaths.Output;

        // 3D smear export is always on when the Smear Bake workflow is active.
        public bool ExportSmear3D => true;

        // safe accessor: trims whitespace, falls back to default if blank
        public string OutputDirectory =>
            string.IsNullOrWhiteSpace(_outputDirectory) ? SmearFrameworkPaths.Output : _outputDirectory.Trim();
    }
}
