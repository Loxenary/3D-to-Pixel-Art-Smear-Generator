using UnityEngine;
using System;

namespace SmearFramework.DataTypes
{
    /// <summary>
    /// Per-vertex spline control points built by TrajectoryBuilder. Indexed vertex-first so each vertex's trajectory is contiguous in memory for cache-friendly spline evaluation during smear displacement.
    /// </summary>
    [Serializable]
    public class TrajectoryData
    {
        private int _frameCount;
        private int _vertexCount;
        private ElementBuffer<Vector3> _controlPoints;

        public int FrameCount => _frameCount;
        public int VertexCount => _vertexCount;
        public ElementBuffer<Vector3> ControlPoints => _controlPoints;

        public TrajectoryData(int vertexCount, int frameCount)
        {
            _frameCount = frameCount;
            _vertexCount = vertexCount;
            _controlPoints = new ElementBuffer<Vector3>(vertexCount, frameCount);
        }
    }
}
