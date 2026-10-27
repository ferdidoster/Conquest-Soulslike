using UnityEngine;

namespace Conquest.Character
{
    /// <summary>
    /// The data points the player dropped on death, waiting where they fell (see <see cref="DataPointLoss"/>).
    /// Walking into it while alive hands them back. Without a prefab it builds its own look: a spinning
    /// shard over a pale beam, so it can be spotted from across the room.
    /// </summary>
    public sealed class DataPointCache : MonoBehaviour
    {
        public static readonly Color Glow = new Color(1f, 0.72f, 0.25f);

        private DataPointLoss _owner;
        private float _radius;
        private Transform _shard;
        private Light _light;

        public int Amount { get; private set; }

        public static DataPointCache Create(Vector3 position, int amount, DataPointLoss owner, GameObject prefab,
            float radius)
        {
            GameObject go = prefab != null ? Instantiate(prefab, position, Quaternion.identity) : BuildLook(position);
            go.name = $"Data Point Cache ({amount})";
            var cache = go.GetComponent<DataPointCache>();
            if (cache == null) cache = go.AddComponent<DataPointCache>();
            cache.Amount = amount;
            cache._owner = owner;
            cache._radius = radius;
            cache._shard = go.transform.Find("Shard");
            cache._light = go.GetComponentInChildren<Light>();
            return cache;
        }

        private void Update()
        {
            if (_owner == null) return;

            if (_shard != null)
            {
                _shard.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
                _shard.localPosition = new Vector3(0f, 0.9f + Mathf.Sin(Time.time * 2.4f) * 0.08f, 0f);
            }
            if (_light != null) _light.intensity = 2.4f + Mathf.Sin(Time.time * 3.1f) * 0.6f;

            if (!_owner.CanRecover) return;
            Vector3 offset = _owner.transform.position - transform.position;
            if (Mathf.Abs(offset.y) < 2f && new Vector2(offset.x, offset.z).magnitude <= _radius)
            {
                _owner.Recover(this);
                Destroy(gameObject);
            }
        }

        private static GameObject BuildLook(Vector3 position)
        {
            var root = new GameObject("Data Point Cache");
            root.transform.position = position;

            Part(root, PrimitiveType.Cube, "Shard", new Vector3(0f, 0.9f, 0f), Vector3.one * 0.3f,
                Quaternion.Euler(45f, 0f, 45f), Glow, 2.5f);
            Part(root, PrimitiveType.Cylinder, "Beam", new Vector3(0f, 1.6f, 0f), new Vector3(0.05f, 1.6f, 0.05f),
                Quaternion.identity, Glow, 1.2f);
            Part(root, PrimitiveType.Cylinder, "Pool", new Vector3(0f, 0.02f, 0f), new Vector3(0.9f, 0.01f, 0.9f),
                Quaternion.identity, Glow * 0.6f, 0.8f);

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Glow;
            light.range = 4f;
            light.intensity = 2.4f;
            light.shadows = LightShadows.None;
            return root;
        }

        private static void Part(GameObject parent, PrimitiveType type, string name, Vector3 position, Vector3 scale,
            Quaternion rotation, Color color, float emission)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            // No collider: the cache must never block the player walking into it.
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            var material = new Material(renderer.sharedMaterial) { name = "Data Point Glow" };
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", color * emission);
            material.EnableKeyword("_EMISSION");
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void OnDestroy()
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
                if (r.sharedMaterial != null && r.sharedMaterial.name == "Data Point Glow") Destroy(r.sharedMaterial);
        }
    }
}
