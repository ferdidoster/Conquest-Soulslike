using Conquest.Combat;
using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// A flat swoosh ribbon behind a blade while its hit window is open. The ribbon is built
    /// between two points on the blade and smoothed with Catmull-Rom, so even a three-frame
    /// strike draws a clean arc. Its clock follows the animator, so the arc hangs in the air
    /// during hit-stop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponTrail : MonoBehaviour
    {
        [SerializeField] private MeleeHitDetector detector;
        [SerializeField, Tooltip("Optional. The trail follows whichever weapon is equipped.")]
        private WeaponLoadout loadout;
        [SerializeField, Tooltip("Inner edge of the ribbon (e.g. the middle of the blade).")]
        private Transform inner;
        [SerializeField, Tooltip("Outer edge of the ribbon (the tip).")]
        private Transform outer;
        [SerializeField] private Material material;
        [SerializeField, Tooltip("Optional. Its speed drives the trail's clock (0 during hit-stop).")]
        private Animator clockSource;
        [SerializeField] private Color color = new Color(1f, 0.97f, 0.9f, 0.85f);
        [SerializeField, Tooltip("Seconds a sample stays visible.")]
        private float lifetime = 0.16f;
        [SerializeField, Tooltip("Seconds the trail keeps drawing after the hit window closes.")]
        private float linger = 0.05f;
        [SerializeField, Range(1, 8)] private int subdivisions = 4;

        private const int MaxSamples = 48;

        private struct Sample
        {
            public Vector3 Inner;
            public Vector3 Outer;
            public float Time;
        }

        private readonly Sample[] _samples = new Sample[MaxSamples];
        private int _count;
        private float _clock;
        private bool _emitting;
        private float _stopAt = float.MaxValue;

        private Mesh _mesh;
        private GameObject _ribbon;
        private MeshRenderer _renderer;
        private Vector3[] _vertices = new Vector3[0];
        private Color[] _colors = new Color[0];
        private Vector2[] _uvs = new Vector2[0];
        private int[] _triangles = new int[0];

        private void Awake()
        {
            if (detector == null) detector = GetComponentInParent<MeleeHitDetector>();

            // The ribbon is in world space, so it lives outside the character's hierarchy.
            _mesh = new Mesh { name = "Weapon Trail" };
            _mesh.MarkDynamic();
            _ribbon = new GameObject($"{name} Weapon Trail");
            _ribbon.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _ribbon.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            // An empty mesh has no valid bounds, which breaks culling, so the renderer only runs with a ribbon to draw.
            _renderer.enabled = false;
        }

        private void OnEnable()
        {
            if (loadout != null) loadout.Equipped += OnEquipped;
            if (detector == null) return;
            detector.Opened += OnOpened;
            detector.Closed += OnClosed;
        }

        // The loadout may equip during its own Awake, before this component subscribed.
        private void Start()
        {
            if (loadout != null && loadout.Current != null) OnEquipped(loadout.Current);
        }

        private void OnEquipped(WeaponLoadout.Weapon weapon)
        {
            inner = weapon.Hilt;
            outer = weapon.Tip;
            _emitting = false;
            _count = 0;
        }

        private void OnDisable()
        {
            if (loadout != null) loadout.Equipped -= OnEquipped;
            if (detector != null)
            {
                detector.Opened -= OnOpened;
                detector.Closed -= OnClosed;
            }

            _emitting = false;
            _count = 0;
            if (_mesh != null) _mesh.Clear();
            if (_renderer != null) _renderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_ribbon != null) Destroy(_ribbon);
            if (_mesh != null) Destroy(_mesh);
        }

        private void OnOpened(DamageInfo _)
        {
            _emitting = true;
            _stopAt = float.MaxValue;
            _count = 0;
        }

        private void OnClosed() => _stopAt = _clock + linger;

        private void LateUpdate()
        {
            if (inner == null || outer == null) return;

            float speed = clockSource != null ? clockSource.speed : 1f;
            _clock += Time.deltaTime * speed;
            if (_emitting && _clock >= _stopAt) _emitting = false;

            if (_emitting && speed > 0f) AddSample(inner.position, outer.position);
            DropExpired();
            BuildMesh();
        }

        private void AddSample(Vector3 innerPoint, Vector3 outerPoint)
        {
            if (_count == MaxSamples)
            {
                System.Array.Copy(_samples, 1, _samples, 0, MaxSamples - 1);
                _count--;
            }

            _samples[_count++] = new Sample { Inner = innerPoint, Outer = outerPoint, Time = _clock };
        }

        private void DropExpired()
        {
            int expired = 0;
            while (expired < _count && _clock - _samples[expired].Time > lifetime) expired++;
            if (expired == 0) return;
            System.Array.Copy(_samples, expired, _samples, 0, _count - expired);
            _count -= expired;
        }

        private void BuildMesh()
        {
            _mesh.Clear();
            _renderer.enabled = _count >= 2;
            if (_count < 2) return;

            int points = (_count - 1) * subdivisions + 1;
            EnsureCapacity(points);

            int v = 0;
            for (int i = 0; i < _count - 1; i++)
            {
                Sample p0 = _samples[Mathf.Max(i - 1, 0)];
                Sample p1 = _samples[i];
                Sample p2 = _samples[i + 1];
                Sample p3 = _samples[Mathf.Min(i + 2, _count - 1)];
                for (int s = 0; s < subdivisions; s++)
                    AddPoint(ref v, s / (float)subdivisions, p0, p1, p2, p3, points);
            }

            Sample last = _samples[_count - 1];
            AddPoint(ref v, 1f, last, last, last, last, points);

            int t = 0;
            for (int i = 0; i < points - 1; i++)
            {
                int a = i * 2;
                _triangles[t++] = a;
                _triangles[t++] = a + 1;
                _triangles[t++] = a + 2;
                _triangles[t++] = a + 1;
                _triangles[t++] = a + 3;
                _triangles[t++] = a + 2;
            }

            _mesh.SetVertices(_vertices, 0, points * 2);
            _mesh.SetColors(_colors, 0, points * 2);
            _mesh.SetUVs(0, _uvs, 0, points * 2);
            _mesh.SetTriangles(_triangles, 0, (points - 1) * 6, 0);
            _mesh.RecalculateBounds();
        }

        private void AddPoint(ref int v, float t, Sample p0, Sample p1, Sample p2, Sample p3, int points)
        {
            Vector3 innerPoint = CatmullRom(p0.Inner, p1.Inner, p2.Inner, p3.Inner, t);
            Vector3 outerPoint = CatmullRom(p0.Outer, p1.Outer, p2.Outer, p3.Outer, t);
            float time = Mathf.Lerp(p1.Time, p2.Time, t);

            // Newest samples are opaque; the tail fades out. The inner edge is fainter than the tip.
            float life = 1f - Mathf.Clamp01((_clock - time) / lifetime);
            float along = (float)(v / 2) / (points - 1);
            Color tip = color;
            tip.a *= life * life;
            Color root = tip;
            root.a *= 0.35f;

            _vertices[v] = innerPoint;
            _colors[v] = root;
            _uvs[v++] = new Vector2(along, 0f);
            _vertices[v] = outerPoint;
            _colors[v] = tip;
            _uvs[v++] = new Vector2(along, 1f);
        }

        private void EnsureCapacity(int points)
        {
            if (_vertices.Length >= points * 2) return;
            int size = Mathf.NextPowerOfTwo(points * 2);
            _vertices = new Vector3[size];
            _colors = new Color[size];
            _uvs = new Vector2[size];
            _triangles = new int[size * 3];
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}
