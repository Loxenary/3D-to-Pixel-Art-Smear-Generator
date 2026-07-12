using UnityEngine;

namespace SmearFramework
{
    [CreateAssetMenu(fileName = "OutputConfig", menuName = "Smear Framework/Config/Output")]
    public class OutputConfig : ScriptableObject
    {
        // render at high res first, then downscale to preserve detail
        [Tooltip("Final pixel art frame size in pixels.\n32 = classic retro look\n64 = modern pixel art (recommended)\n128 = detailed sprites with fine edges")]
        [SerializeField] private int _outputResolution = 64;

        [Tooltip("Internal 3D render size before downscaling to pixel art.\n512 = more detail preserved (recommended)\n128 = faster bake but thin features like hair may blur")]
        [SerializeField] private int _captureResolution = 512;

        [SerializeField, Min(0f)] private float _fixedCaptureOrthoSize = 0f;

        // palette quantization runs in PixelizationStage (Workflow 2, offline)
        [Tooltip("Max number of colors in the final sprite sheet.\n8 = very retro, limited palette\n16 = balanced (recommended)\n32 = richer gradients but less pixel-art feel")]
        [SerializeField] private int _paletteSize = 16;

        [Tooltip("Draws a solid outline around the character silhouette. Helps readability at small sizes -- most pixel art games use this.")]
        [SerializeField] private bool _enableOutline = true;

        [Tooltip("Color of the character outline. Black is the most common. Try dark desaturated colors to blend with the character palette.")]
        [SerializeField] private Color _outlineColor = Color.black;

        [Tooltip("Also saves the raw 3D capture PNG before pixelization. Useful for re-running just the pixel art step later without re-baking the full 3D animation.")]
        [SerializeField] private bool _saveHighResToDisk;

        [Tooltip("Pixels per Unity unit for the generated 2D sprite.\n32 = each pixel is 1/32 of a Unity unit (recommended)\nMatch this to your game's pixel density setting.")]
        [SerializeField] private int _pixelsPerUnit = 32;

        [Tooltip("When enabled, the generated animation clip loops automatically. Disable for one-shot animations like attacks or death sequences.")]
        [SerializeField] private bool _loopPlayback = true;

        [Tooltip("Sprite anchor point (normalized).\n(0.5, 0) = bottom-center, feet on ground (recommended for characters)\n(0.5, 0.5) = centered")]
        [SerializeField] private Vector2 _pivotNormalized = new Vector2(0.5f, 0f);

        // root folder -- the custom editor replaces this with a browse button
        [Tooltip("Root folder where the sprite sheet and prefab are saved. Use the Browse button in the inspector to pick a folder.")]
        [SerializeField] private string _outputDirectory = SmearFrameworkPaths.Output;

        // folder name section
        [Tooltip("Creates a subfolder with this name inside the output directory. Also used as the base identifier in sprite names. Leave empty to auto-name from character + clip.")]
        [UnityEngine.Serialization.FormerlySerializedAs("_fileName")]
        [SerializeField] private string _folderName = "";

        [Tooltip("Text prepended before the frame number in sprite names. E.g. 'spinkick-' gives spinkick-000, spinkick-001 ...")]
        [SerializeField] private string _prefix = "";

        [Tooltip("Text appended after the frame number in sprite names. E.g. '-v1' gives 000-v1, 001-v1 ...")]
        [SerializeField] private string _suffix = "";

        public int OutputResolution => _outputResolution;
        public int CaptureResolution => _captureResolution;
        public float FixedCaptureOrthoSize => Mathf.Max(0f, _fixedCaptureOrthoSize);
        public int PaletteSize => _paletteSize;
        public bool EnableOutline => _enableOutline;
        public Color OutlineColor => _outlineColor;
        public bool SaveHighResToDisk => _saveHighResToDisk;
        public int PixelsPerUnit => Mathf.Max(1, _pixelsPerUnit);
        public bool LoopPlayback => _loopPlayback;
        public Vector2 PivotNormalized => _pivotNormalized;

        public string FolderName => _folderName != null ? _folderName.Trim() : "";
        public string Prefix => _prefix ?? "";
        public string Suffix => _suffix ?? "";

        // base dir shown in the browse button; does not include the Name subfolder
        public string BaseOutputDirectory =>
            string.IsNullOrWhiteSpace(_outputDirectory) ? SmearFrameworkPaths.Output : _outputDirectory.Trim();

        // full save path: appends the Name subfolder when one is set
        public string OutputDirectory
        {
            get
            {
                string dir = BaseOutputDirectory.TrimEnd('/');
                string name = FolderName;
                return string.IsNullOrEmpty(name) ? dir : dir + "/" + name;
            }
        }
    }
}
