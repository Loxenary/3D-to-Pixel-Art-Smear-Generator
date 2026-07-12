using UnityEngine;
using System;

namespace SmearFramework.DataTypes
{
    /// <summary>
    /// Stores one vertex's position and motion offset at a single animation frame. MotionOffsetComputer fills the motionOffset field after velocity extraction.
    /// </summary>
    [Serializable]
    public struct VertexSnapshot
    {
        public Vector3 position;
        public float motionOffset; // how far this vertex moved relative to the mesh average, used as smear weight
    }
}
