using UnityEngine;
using System;

namespace SmearFramework.DataTypes
{
    /// <summary>
    /// Output of SpriteSheetExportStage: the packed sprite sheet texture, grid layout, and timing info needed to reconstruct the animation at runtime or in an editor.
    /// </summary>
    [Serializable]
    public class SpriteSheetResult
    {
        private Texture2D _spriteSheet;
        private int _columns;
        private int _rows;
        private int _frameWidth;
        private int _frameHeight;
        private int _frameCount;
        private float _frameDuration;
        private string _pngPath;
        private string _jsonPath;
        private string _packageFolder;
        private string _animationJsonPath;
        private string _packageJsonPath;
        private string _clipPath;
        private string _controllerPath;
        private string _prefabPath;

        public Texture2D SpriteSheet { get => _spriteSheet; set => _spriteSheet = value; }
        public int Columns { get => _columns; set => _columns = value; }
        public int Rows { get => _rows; set => _rows = value; }
        public int FrameWidth { get => _frameWidth; set => _frameWidth = value; }
        public int FrameHeight { get => _frameHeight; set => _frameHeight = value; }
        public int FrameCount { get => _frameCount; set => _frameCount = value; }
        public float FrameDuration { get => _frameDuration; set => _frameDuration = value; }
        public string PngPath { get => _pngPath; set => _pngPath = value; }
        public string JsonPath { get => _jsonPath; set => _jsonPath = value; }
        public string PackageFolder { get => _packageFolder; set => _packageFolder = value; }
        public string AnimationJsonPath { get => _animationJsonPath; set => _animationJsonPath = value; }
        public string PackageJsonPath { get => _packageJsonPath; set => _packageJsonPath = value; }
        public string ClipPath { get => _clipPath; set => _clipPath = value; }
        public string ControllerPath { get => _controllerPath; set => _controllerPath = value; }
        public string PrefabPath { get => _prefabPath; set => _prefabPath = value; }
    }

    [Serializable]
    public class SpriteSheetMetadata
    {
        public int schema_version = 1;
        public string outputMode = "pixel";
        public string characterName;
        public string clipName;
        public string sheetFile = "sprite_sheet.png";
        public int frameCount;
        public int frameWidth;
        public int frameHeight;
        public int columns;
        public int rows;
        public int fps;
        public bool loopPlayback = true;
        public int pixelsPerUnit = 32;
        public float pivotX = 0.5f;
        public float pivotY = 0f;
        public float frameDuration;
        public float totalDuration;
        public int sheetWidth;
        public int sheetHeight;
        public int captureResolution;
        public bool smearEnabled;
        public SpriteSheetFrameMetadata[] frames;
    }

    [Serializable]
    public class SpriteSheetFrameMetadata
    {
        public int index;
        public string spriteName;
        public int x;
        public int y;
        public int width;
        public int height;
        public bool hasSmear;
        public float smearIntensity;
    }

    [Serializable]
    public class PixelAnimationPackageManifest
    {
        public int schema_version = 1;
        public string packageType = "smear_pixel_animation";
        public string characterName;
        public string clipName;
        public string outputMode = "pixel";
        public string spriteSheetFile = "sprite_sheet.png";
        public string animationFile = "animation.json";
        public string clipAssetFile;
        public string controllerAssetFile;
        public string prefabAssetFile;
        public string generatedAt;
    }
}
