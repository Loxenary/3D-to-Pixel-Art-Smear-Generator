using UnityEngine;
using UnityEngine.Serialization;
namespace SmearFramework
{
    [CreateAssetMenu(fileName = "PostProcessConfig", menuName = "Smear Generator/Config/Post Process")]
    public class PostProcessConfig : ScriptableObject
    {
        // measured in CIELAB space so perceptually uniform
        [FormerlySerializedAs("_flickerThreshold")]
        [Tooltip("How much a pixel must change color (CIELAB distance) before it updates between frames.\n1 = very locked, barely any updates (may miss real motion changes)\n5 = balanced suppression (recommended)\n12 = loose, colors update freely but flicker is visible")]
        [SerializeField, Range(1f, 15f)] private float _flickerSuppressOnDistance = 5f;

        [Header("Pixelization")]
        [FormerlySerializedAs("_pixelWidth")]
        [Tooltip("Output pixel art width per frame.\n16 = very chunky, blocky retro\n64 = balanced detail (recommended)\n128 = fine detail, less pixel-art feel")]
        [SerializeField, Range(16, 256)] private int _targetPixelWidth = 64;

        [FormerlySerializedAs("_pixelHeight")]
        [Tooltip("Output pixel art height per frame.\n16 = very chunky, blocky retro\n64 = balanced detail (recommended)\n128 = fine detail, less pixel-art feel")]
        [SerializeField, Range(16, 256)] private int _targetPixelHeight = 64;

        [Tooltip("How many times the content-adaptive downscaler refines its edge kernels (Kopf 2013).\n3 = fast, edges may be slightly rough\n5 = balanced (recommended)\n10+ = diminishing returns, noticeably slower")]
        [SerializeField, Range(3, 15)] private int _emIterations = 5;

        [Tooltip("Number of colors the palette generator picks for the sprite sheet.\n4 = Game Boy look, very limited\n16 = standard pixel art palette (recommended)\n64 = near-photorealistic, loses pixel-art feel")]
        [SerializeField, Range(4, 64)] private int _paletteSize = 16;

        [Tooltip("Lock to an artist-defined palette. When set, auto color generation is skipped and every pixel snaps to the nearest color here. Leave empty to use automatic palette generation.")]
        [SerializeField] private Color[] _paletteLUT;

        public float FlickerThreshold => _flickerSuppressOnDistance;
        public int PixelWidth => _targetPixelWidth;
        public int PixelHeight => _targetPixelHeight;
        public int EmIterations => _emIterations;
        public int PaletteSize => _paletteSize;
        public Color[] PaletteLUT => _paletteLUT;
    }
}
