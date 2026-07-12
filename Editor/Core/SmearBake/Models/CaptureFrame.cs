using UnityEngine;

namespace SmearFramework.DataTypes
{
    // Camera params from the last HighResCaptureStage run, used to project world positions to UV for the heatmap.
    public class CaptureFrame
    {
        public Vector3 Center;
        public Quaternion Rotation;
        public float OrthoSize;
        public int Resolution;
        public float ReferencePixelHeight;
    }
}
