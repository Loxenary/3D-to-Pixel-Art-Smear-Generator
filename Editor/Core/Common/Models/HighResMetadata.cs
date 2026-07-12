using System;

namespace SmearFramework.DataTypes
{
    [Serializable]
    public class HighResMetadata
    {
        public int schema_version = 1;
        public string prefab;
        public string clip;
        public string baked_at;
        public int sheet_width;
        public int sheet_height;
        public int cell_width;
        public int cell_height;
        public int cols;
        public int rows;
        public int frame_count;
        public int fps;
        public int smeared_count;
        public float[] smear_intensity = new float[0];
    }
}
