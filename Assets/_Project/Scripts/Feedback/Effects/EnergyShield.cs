using System.Collections.Generic;
using Conquest.Combat;
using Conquest.Stats;
using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// A faceted energy bubble around a character while it guards. It snaps up when the guard is
    /// raised, ripples from the point of every blocked hit, flickers when stamina runs low and
    /// shatters on a guard break. It only reads the <see cref="CombatReceiver"/> and
    /// <see cref="Stamina"/>, so any character with those can use it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnergyShield : MonoBehaviour
    {
        [SerializeField] private CombatReceiver receiver;
        [SerializeField, Tooltip("Optional. Low stamina makes the shield flicker.")]
        private Stamina stamina;
        [SerializeField, Tooltip("A material using the Conquest/Energy Shield shader.")]
        private Material material;

        [SerializeField, Tooltip("Optional. Played when the shield comes up.")]
        private AudioClip raiseSound;
        [SerializeField, Range(0f, 1f)] private float raiseVolume = 0.35f;

        [Header("Shape")]
        [SerializeField, Tooltip("Half extents of the bubble, in metres.")]
        private Vector3 radii = new Vector3(0.85f, 1.2f, 0.85f);
        [SerializeField, Tooltip("Centre of the bubble, relative to this object.")]
        private Vector3 center = new Vector3(0f, 1.05f, 0f);
        [SerializeField, Range(0, 3), Tooltip("Icosphere subdivisions: 1 = chunky facets, 3 = smooth.")]
        private int detail = 2;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float raiseTime = 0.08f;
        [SerializeField, Min(0.01f)] private float lowerTime = 0.18f;

        [Header("Reactions")]
        [SerializeField, Tooltip("Scale kick of a blocked hit, per unit of attack weight.")]
        private float hitPulse = 0.06f;
        [SerializeField, Tooltip("Brightness flash of a blocked hit.")]
        private float hitFlash = 0.8f;
        [SerializeField, Range(0f, 1f), Tooltip("Below this stamina fraction the shield flickers.")]
        private float lowStamina = 0.25f;
        [SerializeField] private float flickerRate = 22f;
        [SerializeField, Tooltip("Seconds the shield takes to shatter on a guard break.")]
        private float breakDuration = 0.35f;

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int HitPosId = Shader.PropertyToID("_HitPosOS");
        private static readonly int HitAgeId = Shader.PropertyToID("_HitAge");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        private Transform _shell;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private MaterialPropertyBlock _block;
        private float _shown;
        private float _pulse;
        private float _flash;
        private float _hitAge = 99f;
        private float _breakAge = 99f;
        private Vector3 _hitPosition;

        private bool IsBreaking => _breakAge < breakDuration;

        private void Awake()
        {
            if (receiver == null) receiver = GetComponent<CombatReceiver>();
            if (stamina == null) stamina = GetComponent<Stamina>();
            if (receiver == null || material == null)
            {
                Debug.LogWarning($"{name}: EnergyShield needs a CombatReceiver and a material.", this);
                enabled = false;
                return;
            }

            _mesh = Icosphere(detail, radii);
            var go = new GameObject("Energy Shield", typeof(MeshFilter), typeof(MeshRenderer));
            _shell = go.transform;
            _shell.SetParent(transform, false);
            _shell.localPosition = center;
            go.GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (receiver != null) receiver.HitReceived += OnHit;
        }

        private void OnDisable()
        {
            if (receiver != null) receiver.HitReceived -= OnHit;
            if (_renderer != null) _renderer.enabled = false;
            _shown = 0f;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        private void OnHit(HitResult result, DamageInfo hit)
        {
            if (result != HitResult.Blocked && result != HitResult.GuardBroken) return;

            // Ripple from where the blade met the bubble, or the side it came from.
            Vector3 toAttacker = hit.HitPoint - _shell.position;
            if (toAttacker.sqrMagnitude < 1e-4f) toAttacker = -hit.Direction;
            Vector3 local = _shell.InverseTransformDirection(toAttacker);
            local = Vector3.Scale(local.normalized, radii);
            _hitPosition = local;
            _hitAge = 0f;
            _flash = hitFlash;
            _pulse = hitPulse * Mathf.Max(1f, hit.Weight);
            _shown = 1f;

            if (result == HitResult.GuardBroken) _breakAge = 0f;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            _hitAge += dt;
            _breakAge += dt;
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 4f);
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 0.5f);

            bool raised = receiver.IsGuarding && !IsBreaking;
            if (raised && _shown <= 0f) AudioOneShots.Play(raiseSound, _shell.position, raiseVolume, Random.Range(0.96f, 1.04f));
            _shown = Mathf.MoveTowards(_shown, raised ? 1f : 0f, dt / (raised ? raiseTime : lowerTime));

            float intensity = _shown;
            float dissolve = 1f - _shown;
            float scale = Mathf.Lerp(0.88f, 1f, _shown) + _pulse;

            if (IsBreaking)
            {
                float t = _breakAge / breakDuration;
                intensity = 1.6f * (1f - t) + 0.2f;
                dissolve = t;
                scale = 1f + 0.3f * t;
            }
            else if (raised && stamina != null && stamina.Pool.Normalized < lowStamina)
            {
                float danger = 1f - stamina.Pool.Normalized / Mathf.Max(lowStamina, 1e-3f);
                float flicker = Mathf.PerlinNoise(Time.time * flickerRate, 0.37f) < 0.45f ? 1f - 0.75f * danger : 1f;
                intensity *= flicker;
            }

            intensity += _flash * _shown;
            bool visible = intensity > 0.01f && dissolve < 0.99f;
            _renderer.enabled = visible;
            if (!visible) return;

            _shell.localScale = Vector3.one * scale;
            _block.SetFloat(IntensityId, intensity);
            _block.SetVector(HitPosId, _hitPosition);
            _block.SetFloat(HitAgeId, _hitAge);
            _block.SetFloat(DissolveId, dissolve);
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>An ellipsoid icosphere with one normal per face, so it reads as cut facets.</summary>
        private static Mesh Icosphere(int subdivisions, Vector3 size)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
            var faces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            for (int s = 0; s < subdivisions; s++)
            {
                var midpoints = new Dictionary<long, int>();
                var next = new List<int>(faces.Count * 4);
                for (int f = 0; f < faces.Count; f += 3)
                {
                    int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                    int ab = Midpoint(points, midpoints, a, b);
                    int bc = Midpoint(points, midpoints, b, c);
                    int ca = Midpoint(points, midpoints, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }

            // Unshared vertices, so each face keeps its own normal, wound to face outward.
            var vertices = new Vector3[faces.Count];
            var normals = new Vector3[faces.Count];
            var triangles = new int[faces.Count];
            for (int f = 0; f < faces.Count; f += 3)
            {
                Vector3 a = Vector3.Scale(points[faces[f]], size);
                Vector3 b = Vector3.Scale(points[faces[f + 1]], size);
                Vector3 c = Vector3.Scale(points[faces[f + 2]], size);
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(normal, a + b + c) < 0f)
                {
                    (b, c) = (c, b);
                    normal = -normal;
                }
                vertices[f] = a; vertices[f + 1] = b; vertices[f + 2] = c;
                normals[f] = normals[f + 1] = normals[f + 2] = normal.normalized;
                triangles[f] = f; triangles[f + 1] = f + 1; triangles[f + 2] = f + 2;
            }

            var mesh = new Mesh { name = "EnergyShield" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int Midpoint(List<Vector3> points, Dictionary<long, int> cache, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            points.Add(((points[a] + points[b]) * 0.5f).normalized);
            cache[key] = points.Count - 1;
            return points.Count - 1;
        }
    }
}
