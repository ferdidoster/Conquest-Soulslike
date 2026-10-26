using System;
using UnityEngine;

namespace Conquest.Combat
{
    /// <summary>
    /// A charged laser for ranged enemies. <see cref="BeginShot"/> starts a telegraph line that tracks
    /// the aim, locks a moment before firing so the target can dodge, then fires one beam: a sphere cast
    /// at chest height. The beam can be blocked or dodged like any hit (the target's CombatReceiver
    /// decides), but not parried.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LaserCannon : MonoBehaviour
    {
        [SerializeField, Tooltip("Where the beam is drawn from.")]
        private Transform muzzle;

        [Header("Timing")]
        [SerializeField, Tooltip("Seconds from the start of the telegraph to the shot.")]
        private float chargeTime = 1.1f;
        [SerializeField, Tooltip("The aim stops tracking this many seconds before the shot: the dodge window.")]
        private float lockTime = 0.35f;
        [SerializeField] private float beamTime = 0.2f;

        [Header("Beam")]
        [SerializeField] private float range = 20f;
        [SerializeField] private float radius = 0.3f;
        [SerializeField, Tooltip("Height above the feet the beam travels at.")]
        private float aimHeight = 1.3f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private float damage = 22f;
        [SerializeField] private float guardDamage = 30f;
        [SerializeField] private float weight = 1f;

        [Header("Look")]
        [SerializeField] private Color color = new Color(1f, 0.55f, 0.15f);
        [SerializeField] private float telegraphWidth = 0.03f;
        [SerializeField] private float beamWidth = 0.22f;
        [SerializeField] private AudioClip chargeSound;
        [SerializeField] private AudioClip fireSound;
        [SerializeField, Tooltip("A puff of smoke played where the beam hits a character.")]
        private ParticleSystem impactSmoke;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private IHittable _self;
        private LineRenderer _line;
        private Func<Vector3> _aim;
        private Vector3 _direction;
        private Vector3 _end;
        private float _age = -1f;
        private bool _fired;
        private ParticleSystem _smoke;

        /// <summary>True from the start of the telegraph until the beam fades.</summary>
        public bool IsBusy => _age >= 0f;

        /// <summary>Raised when the beam fires, with the result of the hit.</summary>
        public event Action<HitResult> Fired;

        private void Awake()
        {
            _self = GetComponent<IHittable>();
            // World-space line, so it lives outside the character's hierarchy.
            var go = new GameObject($"{name} Laser");
            _line = go.AddComponent<LineRenderer>();
            _line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.numCapVertices = 2;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.enabled = false;
        }

        private void OnDestroy()
        {
            if (_line != null) Destroy(_line.gameObject);
            if (_smoke != null) Destroy(_smoke.gameObject);
        }

        private void OnDisable() => Cancel();

        /// <summary>Starts charging a shot. <paramref name="aim"/> returns the direction to shoot in.</summary>
        public void BeginShot(Func<Vector3> aim)
        {
            if (IsBusy) return;
            _aim = aim;
            _age = 0f;
            _fired = false;
            _direction = Flat(aim());
            Play(chargeSound, 0.6f);
        }

        /// <summary>Stops a charge (e.g. when staggered). A beam already fired just fades.</summary>
        public void Cancel()
        {
            if (_fired) return;
            _age = -1f;
            if (_line != null) _line.enabled = false;
        }

        private void Update()
        {
            if (!IsBusy) return;
            _age += Time.deltaTime;

            if (!_fired)
            {
                if (_age < chargeTime - lockTime) _direction = Flat(_aim());
                if (_age >= chargeTime) Fire();
                else DrawTelegraph();
                return;
            }

            float fade = 1f - (_age - chargeTime) / beamTime;
            if (fade <= 0f)
            {
                _age = -1f;
                _line.enabled = false;
                return;
            }
            _line.SetPosition(0, Muzzle);
            _line.SetPosition(1, _end);
            _line.widthMultiplier = beamWidth * fade;
            _line.startColor = _line.endColor = Color.Lerp(Color.white, color, 0.4f);
        }

        private Vector3 Origin => transform.position + Vector3.up * aimHeight;
        private Vector3 Muzzle => muzzle != null ? muzzle.position : Origin;

        private void DrawTelegraph()
        {
            float t = _age / chargeTime;
            bool locked = _age >= chargeTime - lockTime;
            // Flickers faster as the shot nears; turns solid once the aim is locked.
            float pulse = locked ? 1f : 0.5f + 0.5f * Mathf.Sin(_age * Mathf.Lerp(12f, 40f, t));
            _line.enabled = true;
            _line.SetPosition(0, Muzzle);
            _line.SetPosition(1, Trace(out _));
            _line.widthMultiplier = telegraphWidth * (locked ? 2f : 1f);
            Color c = color;
            c.a = Mathf.Lerp(0.25f, 0.9f, t) * pulse;
            _line.startColor = _line.endColor = c;
        }

        private void Fire()
        {
            _fired = true;
            HitResult result = HitResult.Ignored;
            _end = Trace(out RaycastHit? hit);
            if (hit.HasValue && hit.Value.collider.TryGetComponent(out Hurtbox hurtbox))
                result = hurtbox.Receive(new DamageInfo(damage, guardDamage, gameObject, _end, _direction,
                    canBeParried: false, weight: weight));
            Play(fireSound, 1f);
            if (result.Connected()) PlaySmoke(_end);
            Fired?.Invoke(result);
        }

        /// <summary>Where the beam stops: the first enemy hurtbox or solid collider along the aim.</summary>
        private Vector3 Trace(out RaycastHit? target)
        {
            target = null;
            Vector3 origin = Origin;
            int count = Physics.SphereCastNonAlloc(origin, radius, _direction, _hits, range, hitMask,
                QueryTriggerInteraction.Collide);
            Array.Sort(_hits, 0, count, HitDistance.Instance);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider.transform.IsChildOf(transform)) continue;
                Vector3 point = hit.distance > 0f ? hit.point : hit.collider.ClosestPoint(origin);
                if (hit.collider.TryGetComponent(out Hurtbox hurtbox) && hurtbox.Owner != null)
                {
                    if (!IsEnemy(hurtbox.Owner)) continue;
                    target = hit;
                    return point;
                }
                if (hit.collider.isTrigger) continue;
                return point;
            }
            return origin + _direction * range;
        }

        private bool IsEnemy(IHittable other) =>
            !ReferenceEquals(other, _self) &&
            (_self == null || other.Faction != _self.Faction || other.Faction == Faction.Neutral);

        private Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : transform.forward;
        }

        private void PlaySmoke(Vector3 point)
        {
            if (impactSmoke == null) return;
            // One puff at a time is enough: shots are seconds apart.
            if (_smoke == null) _smoke = Instantiate(impactSmoke);
            _smoke.transform.SetPositionAndRotation(point, Quaternion.LookRotation(-_direction));
            _smoke.Clear(true);
            _smoke.Play(true);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null) AudioSource.PlayClipAtPoint(clip, Muzzle, volume);
        }

        private sealed class HitDistance : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistance Instance = new HitDistance();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
