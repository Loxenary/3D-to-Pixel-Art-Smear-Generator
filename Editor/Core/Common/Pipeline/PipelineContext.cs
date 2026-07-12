using System.Collections.Generic;
using UnityEngine;
using SmearFramework.DataTypes;

namespace SmearFramework
{
    public class PipelineContext
    {
        private PipelineConfig _config;
        private GameObject _target;
        private AnimationClip _clip;
        private DiagnosticsData _diagnostics;
        private float[] _stageTimes = new float[0];
        private Dictionary<string, object> _artifacts = new Dictionary<string, object>();

        public PipelineConfig Config => _config;
        public GameObject Target => _target;
        public AnimationClip Clip => _clip;
        public DiagnosticsData Diagnostics { get => _diagnostics; set => _diagnostics = value; }
        public float[] StageTimes { get => _stageTimes; set => _stageTimes = value; }
        public T Get<T>(string key)
        {
            if (!_artifacts.TryGetValue(key, out var value))
                throw new KeyNotFoundException($"Artifact '{key}' not found in pipeline context.");
            return (T)value;
        }

        public bool Has(string key) => _artifacts.ContainsKey(key);
        public IEnumerable<string> Keys => _artifacts.Keys;
        public object GetRaw(string key) => _artifacts.TryGetValue(key, out var v) ? v : null;
        public void Set<T>(string key, T value) => _artifacts[key] = value;

        public PipelineContext(PipelineConfig config, GameObject target, AnimationClip clip)
        {
            _config = config;
            _target = target;
            _clip = clip;
        }
    }
}
