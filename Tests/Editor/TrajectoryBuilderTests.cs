using NUnit.Framework;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.VelocityExtraction;

namespace SmearFramework.Tests
{
    public class TrajectoryBuilderTests
    {
        [Test]
        public void LinearTrajectory_EvaluatesCorrectly()
        {
            // straight line from origin along X
            int frames = 4;
            var data = new MotionData(frames, 1, 1, 10f);
            data.SetSkeleton(new int[] { -1 }, new BoneWeight[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f }
            });

            for (int f = 0; f < frames; f++)
            {
                data.Vertices[f][0] = new VertexSnapshot
                {
                    position = new Vector3(f, 0, 0)
                };
                data.Bones[f][0] = new BoneSnapshot { position = Vector3.zero, rotation = Quaternion.identity };
            }

            var builder = new TrajectoryBuilder();
            var traj = builder.Build(data);

            // at exact frame positions
            for (int f = 0; f < frames; f++)
            {
                var pos = builder.Evaluate(traj, 0, f);
                Assert.That(pos.x, Is.EqualTo(f).Within(0.01f),
                    $"Frame {f}: expected x={f}, got x={pos.x}");
            }

            // midpoint should be ~1.5 for linear motion
            var mid = builder.Evaluate(traj, 0, 1.5f);
            Assert.That(mid.x, Is.EqualTo(1.5f).Within(0.15f),
                $"Midpoint: expected x~1.5, got x={mid.x}");
        }

        [Test]
        public void Evaluate_ClampsBeyondRange()
        {
            int frames = 3;
            var data = new MotionData(frames, 1, 1, 10f);
            data.SetSkeleton(new int[] { -1 }, new BoneWeight[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f }
            });

            for (int f = 0; f < frames; f++)
            {
                data.Vertices[f][0] = new VertexSnapshot
                {
                    position = new Vector3(f * 2, 0, 0)
                };
                data.Bones[f][0] = new BoneSnapshot { position = Vector3.zero, rotation = Quaternion.identity };
            }

            var builder = new TrajectoryBuilder();
            var traj = builder.Build(data);

            // beyond last frame
            var beyond = builder.Evaluate(traj, 0, 10f);
            var last = builder.Evaluate(traj, 0, 2f);
            Assert.That(Vector3.Distance(beyond, last), Is.LessThan(0.01f),
                "Beyond range should clamp to last frame");

            // before first frame
            var before = builder.Evaluate(traj, 0, -5f);
            var first = builder.Evaluate(traj, 0, 0f);
            Assert.That(Vector3.Distance(before, first), Is.LessThan(0.01f),
                "Before range should clamp to first frame");
        }

        [Test]
        public void Build_ControlPointsMatchVertexPositions()
        {
            int frames = 3;
            int verts = 2;
            var data = new MotionData(frames, 1, verts, 10f);
            data.SetSkeleton(new int[] { -1 }, new BoneWeight[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 0, weight0 = 1f }
            });

            var expected = new Vector3[verts, frames];
            for (int v = 0; v < verts; v++)
            {
                for (int f = 0; f < frames; f++)
                {
                    expected[v, f] = new Vector3(v * 10 + f, f * 2, 0);
                    data.Vertices[f][v] = new VertexSnapshot { position = expected[v, f] };
                }
                data.Bones[0][0] = new BoneSnapshot { position = Vector3.zero, rotation = Quaternion.identity };
            }

            var builder = new TrajectoryBuilder();
            var traj = builder.Build(data);

            for (int v = 0; v < verts; v++)
            {
                for (int f = 0; f < frames; f++)
                {
                    Assert.That(traj.ControlPoints[v][f], Is.EqualTo(expected[v, f]),
                        $"Vert {v} frame {f}: control point mismatch");
                }
            }
        }
    }
}
