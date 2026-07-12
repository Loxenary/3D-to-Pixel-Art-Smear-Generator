using NUnit.Framework;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.VelocityExtraction;

namespace SmearFramework.Tests
{
    public class BoneVelocityComputerTests
    {
        [Test]
        public void LinearMotion_ProducesCorrectVelocity()
        {
            // bone moving at 10 units/sec along X
            float fps = 10f;
            int frames = 5;
            var data = CreateSingleBoneData(frames, 1, 1, fps);

            for (int f = 0; f < frames; f++)
            {
                data.Bones[f][0] = new BoneSnapshot
                {
                    position = new Vector3(f * 1f, 0, 0), // 1 unit per frame = 10 units/sec at 10fps
                    rotation = Quaternion.identity
                };
            }

            var computer = new BoneVelocityComputer();
            computer.Compute(data);

            // mid frames should have ~10 units/sec on X
            // constant vel gets stripped out, should be ~0 after
            // since this is constant velocity, after stripping it should be ~0
            for (int f = 1; f < frames - 1; f++)
            {
                float speed = data.Bones[f][0].linearVelocity.magnitude;
                Assert.That(speed, Is.LessThan(0.1f),
                    $"Frame {f}: constant velocity should be stripped, got {speed}");
            }
        }

        [Test]
        public void AcceleratingMotion_PreservesAcceleration()
        {
            // bone accelerating along X: position = t^2
            float fps = 10f;
            int frames = 5;
            var data = CreateSingleBoneData(frames, 1, 1, fps);

            for (int f = 0; f < frames; f++)
            {
                float t = f / fps;
                data.Bones[f][0] = new BoneSnapshot
                {
                    position = new Vector3(t * t * 10f, 0, 0), // quadratic
                    rotation = Quaternion.identity
                };
            }

            var computer = new BoneVelocityComputer();
            computer.Compute(data);

            // not constant, so stripping shouldn't zero everything
            // at least some frames should have non-zero velocity after stripping
            bool hasNonZero = false;
            for (int f = 0; f < frames; f++)
            {
                if (data.Bones[f][0].linearVelocity.magnitude > 0.01f)
                    hasNonZero = true;
            }
            Assert.IsTrue(hasNonZero, "Accelerating motion should preserve some velocity after root stripping");
        }

        [Test]
        public void StationaryBone_ReturnsZeroVelocity()
        {
            float fps = 10f;
            int frames = 5;
            var data = CreateSingleBoneData(frames, 1, 1, fps);

            for (int f = 0; f < frames; f++)
            {
                data.Bones[f][0] = new BoneSnapshot
                {
                    position = new Vector3(3, 2, 1),
                    rotation = Quaternion.identity
                };
            }

            var computer = new BoneVelocityComputer();
            computer.Compute(data);

            for (int f = 0; f < frames; f++)
            {
                Assert.That(data.Bones[f][0].linearVelocity.magnitude, Is.LessThan(0.001f),
                    $"Frame {f}: stationary bone should have zero velocity");
                Assert.That(data.Bones[f][0].angularVelocity.magnitude, Is.LessThan(0.001f),
                    $"Frame {f}: stationary bone should have zero angular velocity");
            }
        }

        [Test]
        public void RotatingBone_ProducesAngularVelocity()
        {
            float fps = 10f;
            int frames = 5;
            var data = CreateSingleBoneData(frames, 1, 1, fps);

            for (int f = 0; f < frames; f++)
            {
                float angle = f * 10f; // 10 deg per frame
                data.Bones[f][0] = new BoneSnapshot
                {
                    position = Vector3.zero,
                    rotation = Quaternion.Euler(0, angle, 0)
                };
            }

            var computer = new BoneVelocityComputer();
            computer.Compute(data);

            // should have angular velocity around Y
            for (int f = 1; f < frames - 1; f++)
            {
                float angSpeed = data.Bones[f][0].angularVelocity.magnitude;
                Assert.That(angSpeed, Is.GreaterThan(0.1f),
                    $"Frame {f}: rotating bone should have angular velocity");
            }
        }

        private MotionData CreateSingleBoneData(int frames, int bones, int verts, float fps)
        {
            var data = new MotionData(frames, bones, verts, fps);
            data.SetSkeleton(new int[] { -1 }, new BoneWeight[] {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f }
            });
            // init vertices too
            for (int f = 0; f < frames; f++)
            {
                data.Vertices[f][0] = new VertexSnapshot
                {
                    position = Vector3.zero,
                    motionOffset = 0f
                };
            }
            return data;
        }
    }
}
