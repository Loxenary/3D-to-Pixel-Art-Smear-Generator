using UnityEngine;

namespace SmearFramework
{
    // Steps through baked smear frame meshes at a fixed rate on all three layers
    // (main body, ghost copies, motion lines). Replaces the legacy Animation component
    // which doesn't reliably drive MeshFilter.m_Mesh PPtr curves at runtime.
    public class Smear3DFramePlayer : MonoBehaviour
    {
        [SerializeField] private Mesh[] _frames;       // main body mesh per frame
        [SerializeField] private Mesh[] _ghostFrames;  // ghost copy mesh per frame (some may be null)
        [SerializeField] private Mesh[] _lineFrames;   // motion line mesh per frame (some may be null)
        [SerializeField] private float  _fps = 30f;

        private MeshFilter _mainFilter;
        private MeshFilter _ghostFilter;
        private MeshFilter _lineFilter;
        private float _elapsed;
        private int   _currentFrame;

        void Awake()
        {
            _mainFilter  = GetComponent<MeshFilter>();
            var ghost = transform.Find("Ghost");
            var lines = transform.Find("Lines");
            if (ghost != null) _ghostFilter = ghost.GetComponent<MeshFilter>();
            if (lines != null) _lineFilter  = lines.GetComponent<MeshFilter>();
        }

        // Reset to frame 0 every time the overlay becomes active.
        void OnEnable()
        {
            _elapsed      = 0f;
            _currentFrame = 0;
            ApplyFrame(0);
        }

        void Update()
        {
            if (_frames == null || _frames.Length == 0) return;

            _elapsed += Time.deltaTime;
            float frameDuration = 1f / Mathf.Max(1f, _fps);
            while (_elapsed >= frameDuration)
            {
                _elapsed -= frameDuration;
                _currentFrame = (_currentFrame + 1) % _frames.Length;
            }
            ApplyFrame(_currentFrame);
        }

        // Called by SmearScene3DExporter at build time to populate the serialized arrays.
        public void Init(Mesh[] frames, Mesh[] ghostFrames, Mesh[] lineFrames, float fps)
        {
            _frames      = frames;
            _ghostFrames = ghostFrames;
            _lineFrames  = lineFrames;
            _fps         = Mathf.Max(1f, fps);
        }

        // How many frames are stored -- used by the demo scene verifier.
        public int FrameCount => _frames != null ? _frames.Length : 0;

        void ApplyFrame(int f)
        {
            if (_mainFilter != null && _frames != null && f < _frames.Length)
                _mainFilter.sharedMesh = _frames[f];

            if (_ghostFilter != null && _ghostFrames != null && f < _ghostFrames.Length)
                _ghostFilter.sharedMesh = _ghostFrames[f];

            if (_lineFilter != null && _lineFrames != null && f < _lineFrames.Length)
                _lineFilter.sharedMesh = _lineFrames[f];
        }
    }
}
