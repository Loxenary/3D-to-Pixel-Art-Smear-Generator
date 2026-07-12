using UnityEngine;
using System.Collections.Generic;
using SmearFramework.DataTypes;

namespace SmearFramework.VelocityExtraction
{
    // note: rohmer-2021-velocity-skinning.md
    // Computes per-bone velocities from sampled animation data using finite differences, and builds propagated bone weights for smear influence.
    public class BoneVelocityComputer
    {
        public void Compute(MotionData data)
        {
            ComputeVelocities(data);
            StripRootTranslation(data);
        }

        private void ComputeVelocities(MotionData data)
        {
            float fps = data.Fps;

            for (int f = 0; f < data.FrameCount; f++)
                ComputeFrameVelocities(data, f, fps);
        }

        private void ComputeFrameVelocities(MotionData data, int f, float fps)
        {
            for (int b = 0; b < data.BoneCount; b++)
            {
                var snap = data.Bones[f][b];

                snap.linearVelocity = ComputeLinearVelocity(data, f, b, fps);
                snap.angularVelocity = ComputeAngularVelocity(data, f, b, fps);

                data.Bones[f][b] = snap;
            }
        }

        // finite difference, central where possible
        private Vector3 ComputeLinearVelocity(MotionData data, int f, int b, float fps)
        {
            if (f == 0 && data.FrameCount > 1)
                return (data.Bones[1][b].position - data.Bones[0][b].position) * fps;

            if (f == data.FrameCount - 1)
                return (data.Bones[f][b].position - data.Bones[f - 1][b].position) * fps;

            // central difference
            return (data.Bones[f+1][b].position - data.Bones[f-1][b].position) * (fps * 0.5f);
        }

        // angular velocity from quaternion delta between frames
        private Vector3 ComputeAngularVelocity(MotionData data, int f, int b, float fps)
        {
            if (f == 0 && data.FrameCount > 1)
                return QuaternionToAngularVelocity(data.Bones[0][b].rotation, data.Bones[1][b].rotation, fps);

            return QuaternionToAngularVelocity(data.Bones[f - 1][b].rotation, data.Bones[f][b].rotation, fps);
        }

        // removes constant root drift so only acceleration-based motion triggers smear
        private void StripRootTranslation(MotionData data)
        {
            if (data.FrameCount < 3) return;

            for (int b = 0; b < data.BoneCount; b++)
            {
                if (data.ParentBoneIndex[b] != -1) continue;
                StripRootBoneTranslation(data, b);
            }
        }

        private void StripRootBoneTranslation(MotionData data, int b)
        {
            var avgVel = ComputeAverageBoneVelocity(data, b);
            SubtractVelocityFromBone(data, b, avgVel);
        }

        private Vector3 ComputeAverageBoneVelocity(MotionData data, int b)
        {
            var avg = Vector3.zero;
            for (int f = 0; f < data.FrameCount; f++)
                avg += data.Bones[f][b].linearVelocity;
            return avg / data.FrameCount;
        }

        private void SubtractVelocityFromBone(MotionData data, int b, Vector3 vel)
        {
            for (int f = 0; f < data.FrameCount; f++)
            {
                var snap = data.Bones[f][b];
                snap.linearVelocity -= vel;
                data.Bones[f][b] = snap;
            }
        }

        private static Vector3 QuaternionToAngularVelocity(Quaternion from, Quaternion to, float fps)
        {
            var delta = Quaternion.Inverse(from) * to;

            // ensure shortest path
            if (delta.w < 0)
            {
                delta.x = -delta.x;
                delta.y = -delta.y;
                delta.z = -delta.z;
                delta.w = -delta.w;
            }

            delta.ToAngleAxis(out float angleDeg, out Vector3 axis);

            if (float.IsInfinity(axis.x) || float.IsNaN(axis.x))
                return Vector3.zero;

            float angleRad = angleDeg * Mathf.Deg2Rad;
            return axis.normalized * (angleRad * fps);
        }

        // builds a per-vertex weight array that accounts for the full bone hierarchy, not just direct skin bindings
        public float[][] ComputePropagatedWeights(BoneWeight[] weights, int[] parentIndex, int boneCount, int vertexCount)
        {
            var descendants = BuildDescendantLists(parentIndex, boneCount);

            var result = new float[vertexCount][];
            for (int v = 0; v < vertexCount; v++)
                result[v] = ComputeVertexBoneWeights(weights[v], descendants, boneCount);

            return result;
        }

        // maps each bone to itself + all its children/grandchildren etc.
        private List<int>[] BuildDescendantLists(int[] parentIndex, int boneCount)
        {
            var descendants = new List<int>[boneCount];
            for (int b = 0; b < boneCount; b++)
            {
                descendants[b] = new List<int>();
                descendants[b].Add(b);
            }

            // walk up the hierarchy
            for (int b = 0; b < boneCount; b++)
            {
                int current = parentIndex[b];
                while (current >= 0)
                {
                    descendants[current].Add(b);
                    current = parentIndex[current];
                }
            }

            return descendants;
        }

        // sum up skin weights of all descendants per bone so parent bones propagate smear influence to vertices skinned to their children
        private float[] ComputeVertexBoneWeights(BoneWeight bw, List<int>[] descendants, int boneCount)
        {
            var perBone = new float[boneCount];
            int[] boneIdx = { bw.boneIndex0, bw.boneIndex1, bw.boneIndex2, bw.boneIndex3 };
            float[] boneWt = { bw.weight0, bw.weight1, bw.weight2, bw.weight3 };

            for (int b = 0; b < boneCount; b++)
                perBone[b] = SumDescendantWeights(descendants[b], boneIdx, boneWt);

            return perBone;
        }

        private float SumDescendantWeights(List<int> descs, int[] boneIdx, float[] boneWt)
        {
            float sum = 0f;
            foreach (int desc in descs)
                sum += GetWeightForBone(desc, boneIdx, boneWt);
            return sum;
        }

        private float GetWeightForBone(int boneIndex, int[] boneIdx, float[] boneWt)
        {
            for (int k = 0; k < 4; k++)
            {
                if (boneIdx[k] == boneIndex)
                    return boneWt[k];
            }
            return 0f;
        }
        // multiplies all extracted bone velocities by a factor -- used to simulate faster/slower playback
        public void ScaleVelocities(MotionData data, float factor)
        {
            for (int f = 0; f < data.FrameCount; f++)
                for (int b = 0; b < data.BoneCount; b++)
                {
                    var snap = data.Bones[f][b];
                    snap.linearVelocity *= factor;
                    snap.angularVelocity *= factor;
                    data.Bones[f][b] = snap;
                }
        }

    }
}
