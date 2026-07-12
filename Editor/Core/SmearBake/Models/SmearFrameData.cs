using UnityEngine;
using System;

namespace SmearFramework.DataTypes
{
    [Serializable]
    public class SmearFrameData
    {
        private int _frameCount;
        private FrameBuffer<Vector3> _deformedPositions; // vertex positions after smear displacement, per frame
        private Mesh[] _additionalGeometry; // per-frame ghost copy meshes for multiples
        private Mesh[] _motionLineGeometry; // per-frame motion-line meshes, kept separate so they can use their own material
        private bool[] _hasSmear; // whether this frame has visible smear
        private float[] _smearIntensity; // 0..1 strength of smear effect per frame

        public int FrameCount => _frameCount;
        public FrameBuffer<Vector3> DeformedPositions => _deformedPositions;
        public Mesh[] AdditionalGeometry => _additionalGeometry;
        public Mesh[] MotionLineGeometry => _motionLineGeometry;
        public bool[] HasSmear => _hasSmear;
        public float[] SmearIntensity => _smearIntensity;

        // allocates buffers for frameCount frames with vertexCount vertices each
        public SmearFrameData(int frameCount, int vertexCount)
        {
            _frameCount = frameCount;
            _deformedPositions = new FrameBuffer<Vector3>(frameCount, vertexCount);
            _additionalGeometry = new Mesh[frameCount];
            _motionLineGeometry = new Mesh[frameCount];
            _hasSmear = new bool[frameCount];
            _smearIntensity = new float[frameCount];
        }
    }
}
