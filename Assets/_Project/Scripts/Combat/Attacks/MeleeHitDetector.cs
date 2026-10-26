using System.Collections.Generic;
using UnityEngine;

namespace Conquest.Combat
{
    /// <summary>
    /// Weapon hit detection without weapon colliders. While a window is open it sweeps spheres
    /// between each sample point's previous and current position, so fast swings never tunnel.
    /// Each target is hit at most once per window.
    /// </summary>
    public sealed class MeleeHitDetector : MonoBehaviour
    {
        [SerializeField, Tooltip("Points along the blade, from hilt to tip.")]
        private Transform[] samplePoints = new Transform[0];
        [SerializeField] private float radius = 0.15f;
        [SerializeField] private LayerMask hitMask = ~0;

        private readonly HashSet<IHittable> _alreadyHit = new HashSet<IHittable>();
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly Collider[] _overlaps = new Collider[16];
        private Vector3[] _previous = new Vector3[0];
        private IHittable _self;
        private DamageInfo _template;
        private bool _active;

        public bool IsActive => _active;

        /// <summary>Raised for every connected hit with its result.</summary>
        public event System.Action<HitResult, IHittable> Hit;
        /// <summary>Raised when a hit window opens (the swing begins to cut), with the swing's damage.</summary>
        public event System.Action<DamageInfo> Opened;
        public event System.Action Closed;

        private void Awake() => _self = GetComponentInParent<IHittable>();

        public float Radius => radius;

        /// <summary>Switches to another blade (e.g. when a weapon is equipped). Closes any open window.</summary>
        public void SetSamplePoints(Transform[] points, float sweepRadius)
        {
            Close();
            samplePoints = points;
            radius = sweepRadius;
            _previous = new Vector3[points.Length];
        }

        public void Open(in DamageInfo template)
        {
            _template = template;
            _alreadyHit.Clear();
            if (_previous.Length != samplePoints.Length) _previous = new Vector3[samplePoints.Length];
            for (int i = 0; i < samplePoints.Length; i++) _previous[i] = samplePoints[i].position;
            _active = true;
            Opened?.Invoke(template);
        }

        public void Close()
        {
            if (!_active) return;
            _active = false;
            Closed?.Invoke();
        }

        // LateUpdate runs after the Animator, so sample points are at their posed positions.
        private void LateUpdate()
        {
            if (_active) Sweep();
        }

        private void Sweep()
        {
            for (int i = 0; i < samplePoints.Length; i++)
            {
                Vector3 current = samplePoints[i].position;
                Vector3 delta = current - _previous[i];
                float length = delta.magnitude;

                int count = length > 1e-4f
                    ? Physics.SphereCastNonAlloc(_previous[i], radius, delta / length, _hits, length, hitMask,
                        QueryTriggerInteraction.Collide)
                    : 0;

                // A sweep that starts inside a collider reports no point, so use the blade point instead.
                for (int h = 0; h < count; h++)
                    TryHit(_hits[h].collider, _hits[h].distance > 0f ? _hits[h].point : current, delta);

                // Overlap at the current point catches targets the blade started inside of.
                int overlaps = Physics.OverlapSphereNonAlloc(current, radius, _overlaps, hitMask,
                    QueryTriggerInteraction.Collide);
                for (int o = 0; o < overlaps; o++) TryHit(_overlaps[o], current, delta);

                _previous[i] = current;
            }
        }

        private void TryHit(Collider collider, Vector3 point, Vector3 swingDirection)
        {
            if (!collider.TryGetComponent(out Hurtbox hurtbox)) return;

            IHittable target = hurtbox.Owner;
            if (target == null || ReferenceEquals(target, _self) || _alreadyHit.Contains(target)) return;
            if (_self != null && target.Faction == _self.Faction && target.Faction != Faction.Neutral) return;

            _alreadyHit.Add(target);

            Vector3 direction = hurtbox.transform.position - _template.Source.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) direction = swingDirection;

            HitResult result = hurtbox.Receive(_template.WithImpact(point, direction.normalized, swingDirection.normalized));
            if (result.Connected()) Hit?.Invoke(result, target);
        }
    }
}
