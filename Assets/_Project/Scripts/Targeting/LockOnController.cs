using System;
using UnityEngine;

namespace Conquest.Targeting
{
    /// <summary>
    /// Player-facing lock-on: toggle, switch left/right, and automatic release when the target
    /// dies, gets too far away or stays hidden behind walls for too long.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LockOnController : MonoBehaviour, ITargetProvider
    {
        [SerializeField, Tooltip("Usually the main camera. Used to pick what the player is looking at.")]
        private Transform view;
        [SerializeField, Tooltip("Where line of sight is measured from (e.g. the character's head).")]
        private Transform eyes;
        [SerializeField] private float acquireDistance = 20f;
        [SerializeField] private float keepDistance = 25f;
        [SerializeField, Range(0f, 180f)] private float acquireViewAngle = 60f;
        [SerializeField] private LayerMask occlusionMask = 1;
        [SerializeField, Tooltip("Seconds without line of sight before the lock breaks.")]
        private float lostSightTimeout = 1.5f;

        private float _hiddenTime;

        public ITargetable CurrentTarget { get; private set; }
        public bool HasTarget => CurrentTarget != null;

        /// <summary>Raised with the new target (null when the lock is released).</summary>
        public event Action<ITargetable> TargetChanged;

        private Vector3 EyePosition => eyes != null ? eyes.position : transform.position + Vector3.up * 1.6f;

        public void SetView(Transform viewTransform) => view = viewTransform;

        public void Toggle()
        {
            if (HasTarget) Release();
            else SetTarget(TargetSelector.FindBest(Targetable.All, BuildQuery(acquireDistance, acquireViewAngle)));
        }

        /// <summary>Switch to the next target on the left (negative) or right (positive).</summary>
        public void Switch(float direction)
        {
            if (!HasTarget || Mathf.Approximately(direction, 0f)) return;

            ITargetable next = TargetSelector.FindAdjacent(
                Targetable.All, CurrentTarget, direction, BuildQuery(acquireDistance, 180f));
            if (next != null) SetTarget(next);
        }

        public void Release() => SetTarget(null);

        private void LateUpdate()
        {
            if (!HasTarget) return;

            if (!CurrentTarget.IsTargetable ||
                Vector3.Distance(EyePosition, CurrentTarget.LockPoint.position) > keepDistance)
            {
                Release();
                return;
            }

            _hiddenTime = TargetSelector.HasLineOfSight(EyePosition, CurrentTarget, occlusionMask)
                ? 0f
                : _hiddenTime + Time.deltaTime;

            if (_hiddenTime > lostSightTimeout) Release();
        }

        private void SetTarget(ITargetable target)
        {
            if (ReferenceEquals(target, CurrentTarget)) return;
            CurrentTarget = target;
            _hiddenTime = 0f;
            TargetChanged?.Invoke(CurrentTarget);
        }

        private TargetSelector.Query BuildQuery(float distance, float angle)
        {
            Transform v = view != null ? view : transform;
            return new TargetSelector.Query
            {
                Origin = EyePosition,
                ViewForward = v.forward,
                MaxDistance = distance,
                MaxViewAngle = angle,
                OcclusionMask = occlusionMask,
                Ignore = gameObject,
            };
        }
    }
}
