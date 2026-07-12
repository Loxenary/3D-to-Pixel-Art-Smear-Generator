using System;
using SmearFramework.DataTypes;

namespace SmearFramework.Editor
{
    internal sealed class SmearResultPopupModel
    {
        public SpriteSheetResult PixelResult;
        public SmearScene3DExporter.Result Smear3DResult;
        public float BakeTimeMs;
        public string PixelExportFolder;
        public string PixelExportFolderName;
        public bool Smear3DResultIsTemporary;
        public string Smear3DExportFolder;
        public string Smear3DExportBaseName;
        public Func<string, string, SmearScene3DExporter.Result> ExportSmear3D;
    }
}
