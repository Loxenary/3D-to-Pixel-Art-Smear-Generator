using NUnit.Framework;
using UnityEngine;
using SmearFramework.DataTypes;
using SmearFramework.VelocityExtraction;

namespace SmearFramework.Tests
{
    public class PropagatedWeightsTests
    {
        [Test]
        public void ThreeBoneChain_RootGetsAllWeights()
        {
            // 0 -> 1 -> 2
            int[] parentIndex = { -1, 0, 1 };
            int boneCount = 3;
            int vertexCount = 1;

            // vertex is 100% on bone2
            var weights = new BoneWeight[]
            {
                new BoneWeight { boneIndex0 = 2, weight0 = 1f }
            };

            var computer = new BoneVelocityComputer();
            var result = computer.ComputePropagatedWeights(weights, parentIndex, boneCount, vertexCount);

            Assert.That(result[0][2], Is.EqualTo(1f).Within(0.001f));
            Assert.That(result[0][1], Is.EqualTo(1f).Within(0.001f)); // parent picks it up
            Assert.That(result[0][0], Is.EqualTo(1f).Within(0.001f)); // root gets everything
        }

        [Test]
        public void SplitWeights_PropagateCorrectly()
        {
            // 0 -> 1, 0 -> 2 (two children)
            int[] parentIndex = { -1, 0, 0 };
            int boneCount = 3;
            int vertexCount = 1;

            // 60% bone1, 40% bone2
            var weights = new BoneWeight[]
            {
                new BoneWeight
                {
                    boneIndex0 = 1, weight0 = 0.6f,
                    boneIndex1 = 2, weight1 = 0.4f
                }
            };

            var computer = new BoneVelocityComputer();
            var result = computer.ComputePropagatedWeights(weights, parentIndex, boneCount, vertexCount);

            Assert.That(result[0][1], Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(result[0][2], Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(result[0][0], Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void UnrelatedBone_GetsZeroWeight()
        {
            // 0 -> 1, bone2 is a separate root
            int[] parentIndex = { -1, 0, -1 };
            int boneCount = 3;
            int vertexCount = 1;

            var weights = new BoneWeight[]
            {
                new BoneWeight { boneIndex0 = 1, weight0 = 1f }
            };

            var computer = new BoneVelocityComputer();
            var result = computer.ComputePropagatedWeights(weights, parentIndex, boneCount, vertexCount);

            Assert.That(result[0][2], Is.EqualTo(0f).Within(0.001f),
                "Unrelated bone should have zero propagated weight");
        }
    }
}
