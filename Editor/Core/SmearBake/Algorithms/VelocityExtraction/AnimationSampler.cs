using UnityEngine;
using System.Collections.Generic;
using SmearFramework.AnimationSampling;
using SmearFramework.DataTypes;
namespace SmearFramework.VelocityExtraction
{
    // Steps through an AnimationClip frame-by-frame, baking mesh + bone state into MotionData.
    public class AnimationSampler
    {
        // Walk through each frame of the clip, baking mesh and bone transforms into MotionData
        public MotionData Sample(GameObject target, AnimationClip clip, PipelineConfig config)
        {
            var smrs = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (smrs == null || smrs.Length == 0)
            {
                Debug.LogError($"No SkinnedMeshRenderer found on {target.name} or its children.");
                return null;
            }

            var bones = smrs[0].bones;
            if (bones == null || bones.Length == 0)
            {
                Debug.LogError($"SkinnedMeshRenderer on {target.name} has no bones.");
                return null;
            }

            int totalVerts = 0;
            foreach (var smr in smrs)
                totalVerts += smr.sharedMesh.vertexCount;

            int frameCount = Mathf.CeilToInt(clip.length * config.TargetFps);
            if (frameCount < 2) frameCount = 2;

            int boneCount = bones.Length;
            float fps = config.TargetFps;

            var motionData = new MotionData(frameCount, boneCount, totalVerts, fps);

            int[] parentIndex = BuildParentIndex(bones);
            var allWeights = MergeBoneWeights(smrs);
            motionData.SetSkeleton(parentIndex, allWeights);

            var bakedMeshes = new Mesh[smrs.Length];
            for (int i = 0; i < smrs.Length; i++)
                bakedMeshes[i] = new Mesh();

            float dt = 1f / fps;

            using (var poseSampler = new ClipPoseSampler(target, clip))
            {
                for (int f = 0; f < frameCount; f++)
                {
                    float time = f * dt;
                    poseSampler.Sample(time);
                    SampleFrame(motionData, smrs, bakedMeshes, bones, f, boneCount);
                }
            }

            for (int i = 0; i < bakedMeshes.Length; i++)
                Object.DestroyImmediate(bakedMeshes[i]);

            return motionData;
        }

        // Capture both vertex positions and bone transforms for a single frame
        private void SampleFrame(MotionData motionData, SkinnedMeshRenderer[] smrs, Mesh[] bakedMeshes, Transform[] bones, int f, int boneCount)
        {
            BakeVertices(motionData, smrs, bakedMeshes, f);
            CaptureBoneState(motionData, bones, f, boneCount);
        }

        // Bake all skinned meshes and write world-space vertex positions into MotionData
        private void BakeVertices(MotionData motionData, SkinnedMeshRenderer[] smrs, Mesh[] bakedMeshes, int f)
        {
            int vertOffset = 0;
            for (int m = 0; m < smrs.Length; m++)
            {
                smrs[m].BakeMesh(bakedMeshes[m]);
                var verts = bakedMeshes[m].vertices;
                var smrTransform = smrs[m].transform;

                WriteVertexPositions(motionData, f, verts, smrTransform, vertOffset);
                vertOffset += verts.Length;
            }
        }

        // Convert local vertex positions to world space and store them in the frame snapshot
        private void WriteVertexPositions(MotionData motionData, int f, Vector3[] verts, Transform smrTransform, int vertOffset)
        {
            for (int v = 0; v < verts.Length; v++)
            {
                var ws = smrTransform.TransformPoint(verts[v]);
                motionData.Vertices[f][vertOffset + v] = new VertexSnapshot
                {
                    position = ws,
                    motionOffset = 0f
                };
            }
        }

        // Record each bone's world position and rotation for this frame
        private void CaptureBoneState(MotionData motionData, Transform[] bones, int f, int boneCount)
        {
            for (int b = 0; b < boneCount; b++)
            {
                motionData.Bones[f][b] = new BoneSnapshot
                {
                    position = bones[b].position,
                    rotation = bones[b].rotation,
                    linearVelocity = Vector3.zero,
                    angularVelocity = Vector3.zero
                };
            }
        }

        // Combine bone weights from all skinned mesh renderers into a single flat array
        private BoneWeight[] MergeBoneWeights(SkinnedMeshRenderer[] smrs)
        {
            var allWeights = new List<BoneWeight>();
            foreach (var smr in smrs)
            {
                var weights = smr.sharedMesh.boneWeights;
                if (weights != null && weights.Length > 0)
                    allWeights.AddRange(weights);
                else
                {
                    for (int i = 0; i < smr.sharedMesh.vertexCount; i++)
                        allWeights.Add(new BoneWeight());
                }
            }
            return allWeights.ToArray();
        }

        // Maps each bone to its parent's index in the array (-1 if root).
        private int[] BuildParentIndex(Transform[] bones)
        {
            int[] parentIndex = new int[bones.Length];

            for (int i = 0; i < bones.Length; i++)
                parentIndex[i] = FindParentIndex(bones, i);

            return parentIndex;
        }

        // Unity bones are transforms, so we have to search the array to match .parent
        private int FindParentIndex(Transform[] bones, int i)
        {
            if (bones[i].parent == null) return -1;

            for (int j = 0; j < bones.Length; j++)
            {
                if (j != i && bones[j] == bones[i].parent)
                    return j;
            }

            return -1;
        }
    }
}
