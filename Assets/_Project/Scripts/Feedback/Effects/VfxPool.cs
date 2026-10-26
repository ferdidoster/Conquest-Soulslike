using System.Collections.Generic;
using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// Plays one-shot particle effects from a small pool per prefab, so hits never instantiate
    /// at runtime after the first few. Prefabs should use Hierarchy scaling to honour the scale.
    /// </summary>
    public static class VfxPool
    {
        public static int MaxInstancesPerPrefab = 12;

        private static readonly Dictionary<ParticleSystem, List<ParticleSystem>> Pools =
            new Dictionary<ParticleSystem, List<ParticleSystem>>();
        private static Transform _root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Pools.Clear();
            _root = null;
        }

        public static ParticleSystem Play(ParticleSystem prefab, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            if (prefab == null) return null;
            if (_root == null)
            {
                // The pool lives in the scene, so loading another scene throws it away.
                _root = new GameObject("[VFX Pool]").transform;
                Pools.Clear();
            }

            ParticleSystem instance = Acquire(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = Vector3.one * scale;
            instance.Clear(true);
            instance.Play(true);
            return instance;
        }

        private static ParticleSystem Acquire(ParticleSystem prefab)
        {
            if (!Pools.TryGetValue(prefab, out List<ParticleSystem> pool))
                Pools[prefab] = pool = new List<ParticleSystem>();

            foreach (ParticleSystem candidate in pool)
                if (candidate != null && !candidate.IsAlive(true)) return candidate;

            if (pool.Count < MaxInstancesPerPrefab)
            {
                ParticleSystem created = Object.Instantiate(prefab, _root);
                pool.Add(created);
                return created;
            }

            // All busy: recycle the oldest and move it to the back.
            ParticleSystem oldest = pool[0];
            pool.RemoveAt(0);
            pool.Add(oldest);
            return oldest;
        }
    }
}
