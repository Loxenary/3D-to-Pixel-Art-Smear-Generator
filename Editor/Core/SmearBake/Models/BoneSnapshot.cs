using UnityEngine;
using System;

namespace SmearFramework.DataTypes
{
    /// <summary>
    /// Stores one bone's transform and velocity at a single animation frame. Populated by AnimationSampler during velocity extraction.
    /// </summary>
    [Serializable]
    public struct BoneSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
    }
}
