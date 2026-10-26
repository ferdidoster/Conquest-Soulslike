using System;
using System.Collections.Generic;
using System.IO;
using Conquest.Environment;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Turns the Blender desert kit into Unity assets:
    ///   * one shared toon material per palette entry  (Art/Environment/Materials)
    ///   * one prefab per kit piece with its collider, fire light or hover  (Prefabs/Environment/DesertKit/&lt;Category&gt;)
    ///   * the boss arena as a prefab of nested kit prefabs, spawn points and invisible cliff walls
    ///     (Prefabs/Environment/Levels)
    /// Build your own levels by dragging the kit prefabs into a scene; the arena prefab is just
    /// one arrangement of them.
    /// </summary>
    public static class DesertKitBuilder
    {
        public const string KitFolder = "Assets/_Project/Art/Environment/DesertKit";
        public const string ManifestPath = KitFolder + "/DesertKit.json";
        public const string BossArenaLayout = KitFolder + "/Layouts/BossArena.json";
        public const string MaterialFolder = "Assets/_Project/Art/Environment/Materials";
        public const string PrefabFolder = "Assets/_Project/Prefabs/Environment/DesertKit";
        public const string LevelFolder = "Assets/_Project/Prefabs/Environment/Levels";
        public const string BossArenaName = "Arena_DesertBoss";

        /// <summary>Invisible walls live on Ignore Raycast so lock-on and camera rays pass through.</summary>
        private const int BlockerLayer = 2;

        public sealed class Result
        {
            public GameObject Arena;
            public GameObject Atmosphere;
        }

        public static bool IsAvailable => File.Exists(ManifestPath);

        public static Result Build()
        {
            var manifest = LoadJson<DesertKitManifest>(ManifestPath);
            if (manifest == null) return null;

            // Re-import so the EnvironmentModelPostprocessor rules apply even to models copied in before it compiled.
            AssetDatabase.ImportAsset(KitFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Dictionary<string, Material> materials = BuildMaterials(manifest.palette);
            Dictionary<string, GameObject> prefabs = BuildPiecePrefabs(manifest.pieces, materials);

            var result = new Result
            {
                Atmosphere = DesertArtDirection.BuildAtmospherePrefab(),
                Arena = BuildLevel(BossArenaLayout, BossArenaName, prefabs),
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"Conquest: desert kit ready ({prefabs.Count} prefabs in {PrefabFolder}).");
            return result;
        }

        private static Dictionary<string, Material> BuildMaterials(PaletteEntry[] palette)
        {
            var materials = new Dictionary<string, Material>();
            foreach (PaletteEntry entry in palette)
            {
                Material material = ToonShading.SyncKitMaterial(MaterialFolder, entry);
                if (material != null) materials[entry.name] = material;
            }

            return materials;
        }

        private static Dictionary<string, GameObject> BuildPiecePrefabs(KitPieceEntry[] pieces,
            Dictionary<string, Material> materials)
        {
            var prefabs = new Dictionary<string, GameObject>();
            foreach (KitPieceEntry entry in pieces)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(entry.model);
                if (model == null)
                {
                    Debug.LogWarning($"Conquest: kit model missing: {entry.model}");
                    continue;
                }

                GameObject root = BuildPiece(entry, model, materials);
                string folder = $"{PrefabFolder}/{entry.category}";
                EditorAssetUtility.EnsureFolder(folder);
                prefabs[entry.name] = PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{entry.name}.prefab");
                Object.DestroyImmediate(root);
            }

            return prefabs;
        }

        /// <summary>
        /// Plain root (so placing, rotating and scaling never fights the FBX transform) with the
        /// model nested inside, kit materials, collider and extras.
        /// </summary>
        private static GameObject BuildPiece(KitPieceEntry entry, GameObject model, Dictionary<string, Material> materials)
        {
            var root = new GameObject(entry.name);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null && materials.TryGetValue(PaletteName(slots[i].name), out Material kit))
                        slots[i] = kit;
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            switch (entry.collider)
            {
                case "mesh":
                    foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>())
                        filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    break;
                case "box":
                    var box = root.AddComponent<BoxCollider>();
                    box.center = KitJson.Vec(entry.boxCenter);
                    box.size = KitJson.Vec(entry.boxSize);
                    break;
            }

            if (entry.hasLight) AddFireLight(root, entry);
            if (entry.hover) root.AddComponent<Hover>();

            StaticEditorFlags flags = entry.hover ? (StaticEditorFlags)0 : StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
                if (t.GetComponent<Light>() == null)
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            return root;
        }

        private static void AddFireLight(GameObject root, KitPieceEntry entry)
        {
            var go = new GameObject("Fire Light");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = KitJson.Vec(entry.lightOffset);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = KitJson.Col(entry.lightColor, new Color(1f, 0.64f, 0.3f));
            light.range = entry.lightRange;
            light.intensity = entry.lightIntensity;
            light.shadows = LightShadows.None;
            go.AddComponent<FlameFlicker>();
        }

        /// <summary>"Kit_Stone.001" / "Kit_Stone (Instance)" -> "Kit_Stone".</summary>
        private static string PaletteName(string materialName)
        {
            int cut = materialName.IndexOfAny(new[] { '.', ' ' });
            return cut > 0 ? materialName.Substring(0, cut) : materialName;
        }

        public static GameObject BuildLevel(string layoutPath, string levelName, Dictionary<string, GameObject> prefabs)
        {
            var layout = LoadJson<LevelLayout>(layoutPath);
            if (layout == null) return null;

            var root = new GameObject(levelName);
            var groups = new Dictionary<string, Transform>();

            foreach (PlacedPiece placed in layout.pieces)
            {
                if (!prefabs.TryGetValue(placed.piece, out GameObject prefab))
                {
                    Debug.LogWarning($"Conquest: layout {layout.name} uses unknown piece '{placed.piece}'.");
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Group(root, groups, placed.group));
                go.transform.localPosition = KitJson.Vec(placed.position);
                go.transform.localRotation = Quaternion.Euler(0f, placed.yaw, 0f);
                go.transform.localScale = Vector3.one * (placed.scale > 0f ? placed.scale : 1f);
            }

            Transform spawns = Group(root, groups, "Spawns");
            for (int i = 0; i < layout.spawns.Length; i++)
            {
                LayoutSpawn spawn = layout.spawns[i];
                var go = new GameObject($"Spawn_{spawn.kind}_{i}");
                go.transform.SetParent(spawns, false);
                go.transform.localPosition = KitJson.Vec(spawn.position);
                go.transform.localRotation = Quaternion.Euler(0f, spawn.yaw, 0f);
                var point = go.AddComponent<SpawnPoint>();
                if (Enum.TryParse(spawn.kind, out SpawnKind kind)) EditorAssetUtility.Set(point, "kind", (int)kind);
            }

            Transform blockers = Group(root, groups, "Blockers");
            foreach (LayoutBlocker blocker in layout.blockers)
            {
                var go = new GameObject("Blocker") { layer = BlockerLayer };
                go.transform.SetParent(blockers, false);
                go.transform.localPosition = KitJson.Vec(blocker.position);
                go.transform.localRotation = Quaternion.Euler(0f, blocker.yaw, 0f);
                go.AddComponent<BoxCollider>().size = KitJson.Vec(blocker.size);
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }

            EditorAssetUtility.EnsureFolder(LevelFolder);
            GameObject level = PrefabUtility.SaveAsPrefabAsset(root, $"{LevelFolder}/{levelName}.prefab");
            Object.DestroyImmediate(root);
            return level;
        }

        private static Transform Group(GameObject root, Dictionary<string, Transform> groups, string name)
        {
            if (string.IsNullOrEmpty(name)) name = "Misc";
            if (groups.TryGetValue(name, out Transform group)) return group;
            group = new GameObject(name).transform;
            group.SetParent(root.transform, false);
            groups[name] = group;
            return group;
        }

        private static T LoadJson<T>(string path) where T : class
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Conquest: {path} not found. Run Tools/Blender/build_arena_kit.py first.");
                return null;
            }

            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }
    }
}
