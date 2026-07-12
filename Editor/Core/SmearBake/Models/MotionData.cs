using UnityEngine;
using System;

namespace SmearFramework.DataTypes
{
    /// <summary>
    /// Central data container for one animation clip's extracted motion. Holds per-frame bone transforms, vertex positions, skeleton hierarchy, and skin weights. Populated by VelocityExtractionStage, consumed by smear generation.
    /// </summary>
    [Serializable]
    public class MotionData
    {
        private int _frameCount;
        private int _boneCount;
        private int _vertexCount;
        private float _fps;
        private FrameBuffer<BoneSnapshot> _bones; // per-frame snapshot of every bone's transform and velocity
        private FrameBuffer<VertexSnapshot> _vertices; // per-frame snapshot of every vertex's position and motion offset

        private int[] _parentBoneIndex; // parent index per bone, -1 means root bone
        private BoneWeight[] _skinWeights; // Unity skinning weights per vertex (up to 4 bones each)
        private float[][] _propagatedWeights; // accumulated weight per vertex per bone, including child bone influence

        public int FrameCount => _frameCount;
        public int BoneCount => _boneCount;
        public int VertexCount => _vertexCount;
        public float Fps => _fps;
        public FrameBuffer<BoneSnapshot> Bones => _bones;
        public FrameBuffer<VertexSnapshot> Vertices => _vertices;
        public int[] ParentBoneIndex => _parentBoneIndex;
        public BoneWeight[] SkinWeights => _skinWeights;
        public float[][] PropagatedWeights => _propagatedWeights;

        public MotionData(int frameCount, int boneCount, int vertexCount, float fps)
        {
            _frameCount = frameCount;
            _boneCount = boneCount;
            _vertexCount = vertexCount;
            _fps = fps;
            _bones = new FrameBuffer<BoneSnapshot>(frameCount, boneCount);
            _vertices = new FrameBuffer<VertexSnapshot>(frameCount, vertexCount);
        }
        
        public void SetSkeleton(int[] parentIndex, BoneWeight[] weights)
        {
            _parentBoneIndex = parentIndex;
            _skinWeights = weights;
        }
        
        public void SetPropagatedWeights(float[][] weights)
        {
            _propagatedWeights = weights;
        }
    }
}
