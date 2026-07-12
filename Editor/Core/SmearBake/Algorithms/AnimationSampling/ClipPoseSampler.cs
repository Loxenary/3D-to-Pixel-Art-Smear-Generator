using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SmearFramework.AnimationSampling
{
    /// <summary>Samples clip poses directly or through Mecanim retargeting.</summary>
    public sealed class ClipPoseSampler : System.IDisposable
    {
        const string HumanoidSetupMessage = "Humanoid clips need a target with a valid humanoid avatar. Run Smear Generator/FBX Avatar Setup, assign Character Fbx and Clip Fbx, then click Prepare Retarget Pair.";
        const string GenericSetupMessage = "Generic clips only work on the same rig hierarchy. For cross-character clips, run Smear Generator/FBX Avatar Setup, assign Character Fbx and Clip Fbx, then click Prepare Retarget Pair.";

        private readonly GameObject _target;
        private readonly AnimationClip _clip;
        private readonly Animator _animator;
        private readonly RuntimeAnimatorController _savedController;
        private readonly bool _savedApplyRootMotion;
        private readonly AnimatorCullingMode _savedCullingMode;
        private readonly bool _savedEnabled;
        private readonly AnimatorController _controller;
        private readonly int _stateHash;

        public bool UsesMecanim { get; }

        // Chooses direct sampling or Animator-driven humanoid retargeting once up front.
        public ClipPoseSampler(GameObject target, AnimationClip clip)
        {
            if (target == null)
                throw new System.ArgumentNullException(nameof(target));
            if (clip == null)
                throw new System.ArgumentNullException(nameof(clip));

            _target = target;
            _clip = clip;
            UsesMecanim = NeedsMecanim(target, clip);
            if (!UsesMecanim)
                return;

            _animator = FindAnimator(target);
            _savedController = _animator.runtimeAnimatorController;
            _savedApplyRootMotion = _animator.applyRootMotion;
            _savedCullingMode = _animator.cullingMode;
            _savedEnabled = _animator.enabled;

            _controller = new AnimatorController
            {
                name = "_SmearRetargetSampler",
                hideFlags = HideFlags.HideAndDontSave,
            };
            _controller.AddLayer("Base Layer");
            var stateMachine = _controller.layers[0].stateMachine;
            var state = stateMachine.AddState("Sample");
            state.motion = clip;
            stateMachine.defaultState = state;
            _stateHash = Animator.StringToHash("Base Layer.Sample");

            _animator.runtimeAnimatorController = _controller;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _animator.enabled = true;
        }

        // Samples one clip time on the target.
        public void Sample(float time)
        {
            if (!UsesMecanim)
            {
                _clip.SampleAnimation(_target, time);
                return;
            }

            float normalized = _clip.length > 0f ? Mathf.Clamp01(time / _clip.length) : 0f;
            _animator.Play(_stateHash, 0, normalized);
            _animator.Update(0f);
        }

        // Detects when a humanoid clip can go through Mecanim retargeting.
        public static bool NeedsMecanim(GameObject target, AnimationClip clip)
        {
            if (target == null || clip == null || !clip.humanMotion)
                return false;

            var animator = FindAnimator(target);
            var avatar = animator != null ? animator.avatar : null;
            return avatar != null && avatar.isValid && avatar.isHuman;
        }

        // Explains why a clip cannot be sampled on the current target.
        public static string GetInputProblem(GameObject target, AnimationClip clip)
        {
            if (target == null || clip == null)
                return null;
            if (clip.humanMotion)
                return NeedsMecanim(target, clip) ? null : HumanoidSetupMessage;
            if (HasTransformBindings(clip) && !HasAnyResolvableTransformPath(target, clip))
                return GenericSetupMessage;
            return null;
        }

        // Check whether the clip animates any transform paths.
        static bool HasTransformBindings(AnimationClip clip)
        {
            if (clip == null)
                return false;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type == typeof(Transform))
                    return true;
            }
            return false;
        }

        // Check whether at least one animated transform path exists on the target.
        static bool HasAnyResolvableTransformPath(GameObject target, AnimationClip clip)
        {
            if (target == null || clip == null)
                return false;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform))
                    continue;
                if (string.IsNullOrEmpty(binding.path) || target.transform.Find(binding.path) != null)
                    return true;
            }
            return false;
        }


        // Restores the target Animator after temporary retarget sampling.
        public void Dispose()
        {
            if (!UsesMecanim)
                return;

            _animator.runtimeAnimatorController = _savedController;
            _animator.applyRootMotion = _savedApplyRootMotion;
            _animator.cullingMode = _savedCullingMode;
            _animator.enabled = _savedEnabled;

            if (_controller != null)
                Object.DestroyImmediate(_controller);
        }

        // Finds the Animator that owns the target avatar.
        static Animator FindAnimator(GameObject target)
        {
            return target != null ? target.GetComponentInChildren<Animator>(true) : null;
        }
    }
}
