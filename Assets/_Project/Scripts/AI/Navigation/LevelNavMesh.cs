using System.Collections.Generic;
using System.Diagnostics;
using Conquest.Environment;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Conquest.AI
{
    /// <summary>
    /// Loads the level's NavMesh when the first AI brain wakes up. The editor bakes it into
    /// Resources/NavMesh/&lt;scene&gt;.asset every time Play mode starts (and from Conquest &gt; Bake Enemy
    /// NavMesh), so builds ship it baked. Only a scene without that asset is baked here at runtime, which
    /// in a build needs Read/Write meshes. It walks on every solid collider except characters, triggers
    /// and gate leaves; the leaves are carved out at runtime by <see cref="GateNavBlocker"/> while their
    /// gate is shut. A scene that already has NavMesh data (baked by hand) is left as it is.
    /// </summary>
    public static class LevelNavMesh
    {
        /// <summary>Resources path of a scene's baked NavMesh.</summary>
        public static string ResourcePath(string sceneName) => $"NavMesh/{sceneName}";

        private const int CharacterLayer = 8;
        private const int IncludedLayers = ~((1 << 5) | (1 << CharacterLayer)); // UI, characters

        private static NavMeshDataInstance _instance;
        private static int _builtFor = -1;

        public static bool IsReady { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = default;
            _builtFor = -1;
            IsReady = false;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        /// <summary>Builds the NavMesh for the active scene unless that already happened.</summary>
        public static bool Ensure()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (_builtFor == scene.GetHashCode()) return IsReady;
            _builtFor = scene.GetHashCode();
            Remove();

            foreach (Gate gate in Object.FindObjectsByType<Gate>())
                if (gate.GetComponent<GateNavBlocker>() == null) gate.gameObject.AddComponent<GateNavBlocker>();

            if (NavMesh.CalculateTriangulation().vertices.Length > 0)
            {
                IsReady = true; // baked by hand
                return true;
            }

            var baked = Resources.Load<NavMeshData>(ResourcePath(scene.name));
            if (baked != null)
            {
                _instance = NavMesh.AddNavMeshData(baked);
                IsReady = _instance.valid;
                return IsReady;
            }

            var timer = Stopwatch.StartNew();
            var sources = new List<NavMeshBuildSource>();
            NavMeshData data = CollectSources(sources, out Bounds bounds) ? Build(sources, bounds) : null;
            if (data == null)
            {
                Debug.LogWarning("Conquest: NavMesh build failed; enemies walk straight at their targets.");
                IsReady = false;
                return false;
            }

            data.name = $"{scene.name} NavMesh";
            _instance = NavMesh.AddNavMeshData(data);
            IsReady = _instance.valid;
            Debug.Log($"Conquest: baked the {scene.name} NavMesh from {sources.Count} colliders in {timer.ElapsedMilliseconds} ms.");
            return IsReady;
        }

        /// <summary>Bakes the collected sources with the enemies' agent size.</summary>
        public static NavMeshData Build(List<NavMeshBuildSource> sources, Bounds bounds)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.45f;
            settings.agentHeight = 1.8f;
            settings.agentClimb = 0.4f;
            settings.agentSlope = 45f;
            return NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        }

        /// <summary>Every walkable collider of the loaded scenes, and the bounds around them.</summary>
        public static bool CollectSources(List<NavMeshBuildSource> sources, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Collider c in Object.FindObjectsByType<Collider>())
            {
                if (!Walkable(c)) continue;
                if (any) bounds.Encapsulate(c.bounds);
                else bounds = c.bounds;
                any = true;
            }

            if (!any) return false;
            bounds.Expand(2f);
            NavMeshBuilder.CollectSources(bounds, IncludedLayers, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(s => !(s.component is Collider c) || !Walkable(c));
            return sources.Count > 0;
        }

        private static bool Walkable(Collider c)
        {
            if (!c.enabled || !c.gameObject.activeInHierarchy || c.isTrigger) return false;
            if (c is CharacterController || ((1 << c.gameObject.layer) & IncludedLayers) == 0) return false;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return false; // loose props
            return !GateNavBlocker.IsLeaf(c.transform);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (scene.GetHashCode() != _builtFor) return;
            Remove();
            _builtFor = -1;
        }

        private static void Remove()
        {
            if (_instance.valid) _instance.Remove();
            _instance = default;
            IsReady = false;
        }
    }
}
