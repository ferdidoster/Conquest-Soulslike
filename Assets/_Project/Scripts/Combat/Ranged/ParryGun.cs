using System;
using UnityEngine;

namespace Conquest.Combat
{
    /// <summary>
    /// A sidearm for parrying at range, after Bloodborne: a shot that lands while the target is
    /// winding up or swinging (<see cref="IParryable.IsParryableNow"/>) parries it. Any other hit only
    /// chips a little health. The shot is a sphere cast at chest height along the aim, so it forgives
    /// small aiming errors; the muzzle is only where effects start. Shows the pistol in hand while it
    /// is drawn and on the holster otherwise.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParryGun : MonoBehaviour
    {
        public struct Shot
        {
            public Vector3 Muzzle;
            public Vector3 End;
            public Vector3 Direction;
            public HitResult Result;
            /// <summary>The character hit, if any.</summary>
            public GameObject Target;
            public bool Parried => Result == HitResult.Parried;
        }

        [SerializeField, Tooltip("Where the tracer and muzzle flash start.")]
        private Transform muzzle;
        [SerializeField, Tooltip("The pistol in hand, shown while drawn.")]
        private GameObject handModel;
        [SerializeField, Tooltip("The holstered pistol, shown otherwise.")]
        private GameObject holsterModel;

        [Header("Shot")]
        [SerializeField] private float range = 12f;
        [SerializeField, Tooltip("Thickness of the shot: how forgiving the aim is.")]
        private float radius = 0.35f;
        [SerializeField, Tooltip("Height above the feet the shot travels at.")]
        private float aimHeight = 1.25f;
        [SerializeField, Tooltip("Hurtboxes and walls. Leave everything to stop the tracer at walls.")]
        private LayerMask hitMask = ~0;
        [SerializeField, Tooltip("Damage of a shot that does not parry.")]
        private float chipDamage = 4f;
        [SerializeField, Tooltip("Weight of a chip hit: below the targets' flinch weight, it doesn't interrupt them.")]
        private float chipWeight = 0.3f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private IHittable _self;

        /// <summary>Raised after every shot.</summary>
        public event Action<Shot> Fired;

        private void Awake()
        {
            _self = GetComponent<IHittable>();
            Holster();
        }

        public void Draw() => Show(true);
        public void Holster() => Show(false);

        private void Show(bool drawn)
        {
            if (handModel != null) handModel.SetActive(drawn);
            if (holsterModel != null) holsterModel.SetActive(!drawn);
        }

        /// <summary>Fires along <paramref name="direction"/> (flattened). Returns what the shot did.</summary>
        public Shot Fire(Vector3 direction)
        {
            direction.y = 0f;
            direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : transform.forward;
            Vector3 origin = transform.position + Vector3.up * aimHeight;
            var shot = new Shot
            {
                Muzzle = muzzle != null ? muzzle.position : origin,
                End = origin + direction * range,
                Direction = direction,
                Result = HitResult.Ignored,
            };

            int count = Physics.SphereCastNonAlloc(origin, radius, direction, _hits, range, hitMask,
                QueryTriggerInteraction.Collide);
            Array.Sort(_hits, 0, count, HitDistance.Instance);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider.transform.IsChildOf(transform)) continue;

                Vector3 point = hit.distance > 0f ? hit.point : hit.collider.ClosestPoint(origin);
                shot.End = point;
                if (hit.collider.TryGetComponent(out Hurtbox hurtbox) && hurtbox.Owner != null)
                {
                    if (!IsEnemy(hurtbox.Owner)) continue;
                    shot.Target = hurtbox.Root;
                    shot.Result = Hit(hurtbox, point, direction);
                }
                else if (hit.collider.isTrigger)
                {
                    continue; // triggers never stop a bullet
                }
                break;
            }

            Fired?.Invoke(shot);
            return shot;
        }

        private bool IsEnemy(IHittable target) =>
            !ReferenceEquals(target, _self) &&
            (_self == null || target.Faction != _self.Faction || target.Faction == Faction.Neutral);

        private HitResult Hit(Hurtbox hurtbox, Vector3 point, Vector3 direction)
        {
            if (hurtbox.Root != null && hurtbox.Root.TryGetComponent(out IParryable parryable) && parryable.IsParryableNow)
            {
                parryable.OnParried(gameObject);
                return HitResult.Parried;
            }

            return hurtbox.Receive(new DamageInfo(chipDamage, 0f, gameObject, point, direction,
                canBeParried: false, weight: chipWeight));
        }

        private sealed class HitDistance : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistance Instance = new HitDistance();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
