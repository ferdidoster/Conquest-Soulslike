using System.Collections.Generic;
using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// Flat splats on the ground (blood, oil, scorch marks): pooled quads that pop in with a quick
    /// grow, and recycle the oldest once <see cref="Capacity"/> is reached. Use a material whose
    /// shader cuts the shape out of the quad (Conquest/Flat Splat).
    /// </summary>
    public sealed class SplatDecals : MonoBehaviour
    {
        public static int Capacity = 160;

        private struct Pending
        {
            public Material Material;
            public Vector3 Position;
            public Vector3 Normal;
            public float Size;
            public float StartTime;
            public float GrowTime;
        }

        private sealed class Splat
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float StartTime;
            public float GrowTime;
            public float Size;
            public bool Growing;
        }

        private static SplatDecals _instance;
        private static Mesh _quad;

        private readonly List<Pending> _pending = new List<Pending>();
        private readonly List<Splat> _splats = new List<Splat>();
        private int _next;
        private int _layer;

        /// <summary>
        /// Lays a splat flat on a surface. It appears after <paramref name="delay"/> seconds (time
        /// for a droplet to land) and grows to full size over <paramref name="growTime"/>.
        /// </summary>
        public static void Spawn(Material material, Vector3 position, Vector3 normal, float size, float delay = 0f,
            float growTime = 0.1f)
        {
            if (material == null || size <= 0f) return;
            if (_instance == null)
                _instance = new GameObject("[Splat Decals]").AddComponent<SplatDecals>();

            _instance._pending.Add(new Pending
            {
                Material = material,
                Position = position,
                Normal = normal.sqrMagnitude > 1e-4f ? normal.normalized : Vector3.up,
                Size = size,
                StartTime = Time.time + delay,
                GrowTime = Mathf.Max(0.01f, growTime),
            });
        }

        private void Awake() => _layer = gameObject.layer;

        private void Update()
        {
            float now = Time.time;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].StartTime > now) continue;
                Place(_pending[i], now);
                _pending.RemoveAt(i);
            }

            foreach (Splat splat in _splats)
            {
                if (!splat.Growing) continue;
                float t = Mathf.Clamp01((now - splat.StartTime) / splat.GrowTime);
                // Quick splats pop with a little overshoot; slow ones (pools) just spread.
                float eased = splat.GrowTime < 0.3f ? EaseOutBack(t) : 1f - (1f - t) * (1f - t) * (1f - t);
                splat.Transform.localScale = Vector3.one * (splat.Size * eased);
                splat.Growing = t < 1f;
            }
        }

        private void Place(Pending request, float now)
        {
            Splat splat = NextSplat();
            splat.Renderer.sharedMaterial = request.Material;
            splat.StartTime = now;
            splat.GrowTime = request.GrowTime;
            splat.Size = request.Size;
            splat.Growing = true;

            // Each splat sits a hair above the last so overlapping splats never flicker.
            float lift = 0.008f + 0.0004f * (_next % 16);
            Quaternion spin = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up);
            splat.Transform.SetPositionAndRotation(request.Position + request.Normal * lift,
                Quaternion.FromToRotation(Vector3.up, request.Normal) * spin);
            splat.Transform.localScale = Vector3.zero;
            splat.Transform.gameObject.SetActive(true);
        }

        private Splat NextSplat()
        {
            if (_splats.Count < Capacity)
            {
                var go = new GameObject("Splat") { layer = _layer };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = Quad;
                var meshRenderer = go.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var created = new Splat { Transform = go.transform, Renderer = meshRenderer };
                _splats.Add(created);
                _next++;
                return created;
            }

            Splat recycled = _splats[_next % _splats.Count];
            _next++;
            return recycled;
        }

        private static float EaseOutBack(float t)
        {
            const float overshoot = 1.6f;
            t -= 1f;
            return 1f + t * t * ((overshoot + 1f) * t + overshoot);
        }

        /// <summary>A unit quad lying in the XZ plane, facing up.</summary>
        private static Mesh Quad
        {
            get
            {
                if (_quad != null) return _quad;
                _quad = new Mesh { name = "Splat Quad" };
                _quad.vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f),
                };
                _quad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                _quad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                _quad.RecalculateBounds();
                return _quad;
            }
        }
    }
}
