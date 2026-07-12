using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SmearFramework.Tests
{
    public class PipelineContextArtifactsTests
    {
        private PipelineContext _ctx;

        [SetUp]
        public void SetUp()
        {
            var cfg = ScriptableObject.CreateInstance<PipelineConfig>();
            _ctx = new PipelineContext(cfg, new GameObject("t"), null);
        }

        [Test]
        public void Set_Then_Get_ReturnsSameInstance()
        {
            var list = new List<int> { 1, 2, 3 };
            _ctx.Set("numbers", list);
            var got = _ctx.Get<List<int>>("numbers");
            Assert.AreSame(list, got);
        }

        [Test]
        public void Has_ReturnsTrueAfterSet()
        {
            _ctx.Set("x", 42);
            Assert.IsTrue(_ctx.Has("x"));
        }

        [Test]
        public void Has_ReturnsFalseBeforeSet()
        {
            Assert.IsFalse(_ctx.Has("nope"));
        }

        [Test]
        public void Get_WrongType_Throws()
        {
            _ctx.Set("x", 42);
            Assert.Throws<System.InvalidCastException>(() => _ctx.Get<string>("x"));
        }

        [Test]
        public void Get_MissingKey_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => _ctx.Get<int>("missing"));
        }

        [Test]
        public void Set_Twice_Overwrites()
        {
            _ctx.Set("x", 1);
            _ctx.Set("x", 2);
            Assert.AreEqual(2, _ctx.Get<int>("x"));
        }
    }
}
