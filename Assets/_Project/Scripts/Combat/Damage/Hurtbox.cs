using UnityEngine;

namespace Conquest.Combat
{
    /// <summary>
    /// Put on any collider that should be hittable (the body capsule, or per-limb colliders).
    /// Forwards to the <see cref="IHittable"/> on the owner.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Hurtbox : MonoBehaviour
    {
        [SerializeField, Tooltip("Damage multiplier for this collider (e.g. 1.5 for the head).")]
        private float damageMultiplier = 1f;

        private IHittable _owner;

        public IHittable Owner
        {
            get
            {
                if (_owner == null) _owner = GetComponentInParent<IHittable>();
                return _owner;
            }
        }

        public GameObject Root => (Owner as Component)?.gameObject;

        public HitResult Receive(in DamageInfo hit)
        {
            if (Owner == null) return HitResult.Ignored;
            return Owner.ReceiveHit(hit.WithAmount(hit.Amount * damageMultiplier));
        }
    }
}
