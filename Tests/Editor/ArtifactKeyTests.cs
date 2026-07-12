using System;
using NUnit.Framework;

namespace SmearFramework.Tests
{
    public class ArtifactKeyTests
    {
        [Test]
        public void OfT_SetsKeyAndType()
        {
            var k = ArtifactKey.Of<string>("motion");
            Assert.AreEqual("motion", k.Key);
            Assert.AreEqual(typeof(string), k.Type);
        }

        [Test]
        public void Equals_SameKeyAndType_True()
        {
            var a = ArtifactKey.Of<int>("k");
            var b = ArtifactKey.Of<int>("k");
            Assert.IsTrue(a.Equals(b));
        }

        [Test]
        public void Equals_DifferentKey_False()
        {
            var a = ArtifactKey.Of<int>("a");
            var b = ArtifactKey.Of<int>("b");
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equals_DifferentType_False()
        {
            var a = ArtifactKey.Of<int>("k");
            var b = ArtifactKey.Of<string>("k");
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void GetHashCode_EqualKeys_EqualHashes()
        {
            var a = ArtifactKey.Of<int>("k");
            var b = ArtifactKey.Of<int>("k");
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }
    }
}
