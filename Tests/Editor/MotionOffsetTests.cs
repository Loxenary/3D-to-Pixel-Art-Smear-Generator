using NUnit.Framework;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.VelocityExtraction;

namespace SmearFramework.Tests
{
    public class MotionOffsetTests
    {
        [Test]
        public void MovingBone_LeadingVertexGetsPositiveOffset()
        {
            var data = CreateMovingBoneSetup(velocity: new Vector3(10, 0, 0));

            PipelineConfig config = ScriptableObject.CreateInstance<PipelineConfig>();

            var computer = new MotionOffsetComputer();
            computer.Compute(data, config);

            float leadingOffset = data.Vertices[2][0].motionOffset;
            float trailingOffset = data.Vertices[2][1].motionOffset;

            Assert.That(leadingOffset, Is.GreaterThan(trailingOffset));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void StationaryBone_AllOffsetsNearZero()
        {
            int frames = 5;
            int verts = 2;
            float fps = 10f;

            var data = new MotionData(frames, 1, verts, fps);
            data.SetSkeleton(new int[] { -1 }, new BoneWeight[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 0, weight0 = 1f }
            });

            for (int f = 0; f < frames; f++)
            {
                data.Bones[f][0] = new BoneSnapshot
                {
                    position = Vector3.zero,
                    rotation = Quaternion.identity,
                    linearVelocity = Vector3.zero,
                    angularVelocity = Vector3.zero
                };
                data.Vertices[f][0] = new VertexSnapshot { position = new Vector3(1, 0, 0) };
                data.Vertices[f][1] = new VertexSnapshot { position = new Vector3(-1, 0, 0) };
            }

            var config = ScriptableObject.CreateInstance<PipelineConfig>();
            var computer = new MotionOffsetComputer();
            computer.Compute(data, config);

            for (int f = 0; f < frames; f++)
            {
                Assert.That(Mathf.Abs(data.Vertices[f][0].motionOffset), Is.LessThan(0.01f));
                Assert.That(Mathf.Abs(data.Vertices[f][1].motionOffset), Is.LessThan(0.01f));
            }

            Object.DestroyImmediate(config);
        }

        [Test]
        public void OffsetsClamped_ToMinusOnePlusOne()
        {
            var data = CreateMovingBoneSetup(velocity: new Vector3(1000, 0, 0));

            var config = ScriptableObject.CreateInstance<PipelineConfig>();
            var computer = new MotionOffsetComputer();
            computer.Compute(data, config);

            for (int f = 0; f < data.FrameCount; f++)
            {
                for (int v = 0; v < data.VertexCount; v++)
                {
                    float offset = data.Vertices[f][v].motionOffset;
                    Assert.That(offset, Is.GreaterThanOrEqualTo(-1f).And.LessThanOrEqualTo(1f),
                        $"Frame {f}, vert {v}: offset {offset} out of [-1,1] range");
                }
            }

            Object.DestroyImmediate(config);
        }

        private MotionData CreateMovingBoneSetup(Vector3 velocity)
        {
            int frames = 5;
            int verts = 2;
            float fps = 10f;

            var data = new MotionData(frames, 1, verts, fps);
            data.SetSkeleton(new int[] { -1 }, new BoneWeight[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 0, weight0 = 1f }
            });

            for (int f = 0; f < frames; f++)
            {
                float t = f / fps;
                Vector3 bonePos = velocity * t;

                data.Bones[f][0] = new BoneSnapshot
                {
                    position = bonePos,
                    rotation = Quaternion.identity,
                    linearVelocity = velocity,
                    angularVelocity = Vector3.zero
                };

                data.Vertices[f][0] = new VertexSnapshot
                {
                    position = bonePos + velocity.normalized * 0.5f
                };
                data.Vertices[f][1] = new VertexSnapshot
                {
                    position = bonePos - velocity.normalized * 0.5f
                };
            }

            return data;
        }
    }
}
