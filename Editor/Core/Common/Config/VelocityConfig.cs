using UnityEngine;

namespace SmearFramework
{
    /// <summary>
    /// Animation sampling settings for velocity extraction. Lower FPS = choppier but more stylized (pixel art usually 8-12). Smoothing window reduces noise in the motion offsets, too low causes jitter, too high blurs fast movements.
    /// </summary>
    [CreateAssetMenu(fileName = "VelocityConfig", menuName = "Smear Framework/Config/Velocity")]
    public class VelocityConfig : ScriptableObject
    {
        [Tooltip("How many frames per second are sampled from the animation.\n8 = choppy, very stylized retro feel\n12 = standard pixel art speed (recommended)\n24 = smooth but generates a much larger sprite sheet")]
        [SerializeField] private int _targetFps = 12;

        // quartic kernel for temporal smoothing
        [Tooltip("Frames on each side used to average out velocity spikes.\n1 = raw velocity, may jitter on fast direction changes\n2 = smooth (recommended)\n4 = over-smoothed, quick motions may not trigger smear")]
        [SerializeField, Range(1, 5)] private int _temporalSmoothingWindow = 2;

        [Tooltip("Scales animation playback time during sampling. 1 = normal speed. 2 = twice as fast, higher smear intensity. 0.5 = half speed, lower smear intensity. Useful for evaluation and parameter tuning.")]
        [SerializeField, Range(0.1f, 5f)] private float _playbackSpeed = 1f;

        public int TargetFps => _targetFps;
        public int TemporalSmoothingWindow => _temporalSmoothingWindow;
        public float PlaybackSpeed => _playbackSpeed;
    }
}
