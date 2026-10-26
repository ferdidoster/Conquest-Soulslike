using System.Collections.Generic;
using System.IO;
using Conquest.Environment;
using Conquest.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Turns the Blender underground lab kit into Unity assets, the same way as the desert kit:
    ///   * one shared toon material per palette entry  (Art/Environment/Materials/Lab)
    ///   * one prefab per kit piece with colliders, lights, gates and camera fading
    ///     (Prefabs/Environment/LabKit/&lt;Category&gt;)
    ///   * the demo level as a prefab of nested kit prefabs and spawn points (Prefabs/Environment/Levels)
    ///   * Lab_Atmosphere (cold fill light, dark haze, bloom) and the Lab_Demo scene.
    /// Build your own levels by dragging the kit prefabs into a scene; Level_LabDemo is just one
    /// arrangement of them.
    /// </summary>
    public static class LabKitBuilder
    {
        public const string KitFolder = "Assets/_Project/Art/Environment/LabKit";
        public const string ManifestPath = KitFolder + "/LabKit.json";
        public const string LayoutPath = KitFolder + "/Layouts/LabDemo.json";
        public const string MaterialFolder = "Assets/_Project/Art/Environment/Materials/Lab";
        public const string PrefabFolder = "Assets/_Project/Prefabs/Environment/LabKit";
        public const string LevelName = "Level_LabDemo";
        public const string AtmospherePrefabPath = "Assets/_Project/Prefabs/Environment/Lab_Atmosphere.prefab";
        public const string SkyMaterialPath = DesertArtDirection.RenderingFolder + "/Sky_Lab.mat";
        public const string PostProfilePath = DesertArtDirection.RenderingFolder + "/Lab_PostFX.asset";
        public const string ScenePath = "Assets/_Project/Scenes/Lab_Demo.unity";

        private const string Root = "Assets/_Project";
        private const string PlayerPrefab = Root + "/Prefabs/Characters/Player_Knight.prefab";
        private const string EnemyPrefab = Root + "/Prefabs/Characters/Enemy_Knight.prefab";
        private const string HeavyEnemyPrefab = Root + "/Prefabs/Characters/Enemy_Bulwark.prefab";
        private const string GunnerPrefab = Root + "/Prefabs/Characters/Enemy_Warden.prefab";
        private const string CharacterMaterialFolder = Root + "/Art/Materials";

        private const string ClosedStatus = "Lab_LightRed";
        private const string OpenStatus = "Lab_LightGreen";

        /// <summary>Sensors and invisible walls live on Ignore Raycast so lock-on and camera rays pass through.</summary>
        private const int IgnoreRaycastLayer = 2;

        public static bool IsAvailable => File.Exists(ManifestPath);

        [MenuItem("Conquest/Build Lab Demo", priority = 10)]
        public static void BuildSceneMenu() => BuildScene();

        [MenuItem("Conquest/Rebuild Lab Kit Prefabs", priority = 11)]
        public static void BuildKitMenu() => Build();

        /// <summary>Kit materials, piece prefabs, the level prefab and the atmosphere prefab.</summary>
        public static DesertKitBuilder.Result Build()
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogWarning($"Conquest: {ManifestPath} not found. Run Tools/Blender/build_lab_kit.py first.");
                return null;
            }

            var manifest = JsonUtility.FromJson<LabKitManifest>(File.ReadAllText(ManifestPath));
            AssetDatabase.ImportAsset(KitFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            Dictionary<string, Material> materials = BuildMaterials(manifest.palette);
            Dictionary<string, GameObject> prefabs = BuildPiecePrefabs(manifest.pieces, materials);
            var result = new DesertKitBuilder.Result
            {
                Atmosphere = BuildAtmospherePrefab(),
                Arena = BuildLevel(prefabs),
            };
            AssetDatabase.SaveAssets();
            Debug.Log($"Conquest: lab kit ready ({prefabs.Count} prefabs in {PrefabFolder}).");
            return result;
        }

        /// <summary>Builds the kit and the playable Lab_Demo scene (player, enemies on the spawns, HUD, cameras).</summary>
        public static bool BuildScene()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab);
            if (player == null || enemy == null)
            {
                Debug.LogError("Conquest: character prefabs missing. Run Conquest > Build Knight Sandbox once first.");
                return false;
            }

            SaveOpenScenesWithPath();
            DesertKitBuilder.Result environment = Build();
            if (environment?.Arena == null) return false;

            SandboxSceneBuilder.Build(player, enemy, ScenePath, CharacterMaterialFolder, environment,
                AssetDatabase.LoadAssetAtPath<GameObject>(HeavyEnemyPrefab),
                AssetDatabase.LoadAssetAtPath<GameObject>(GunnerPrefab));

            // Interior extras: bloom needs post-processing. Walls facing the camera are cut away by
            // LevelSections (on the level) rather than faded by an OccluderFader.
            Camera camera = Camera.main;
            var reader = Object.FindFirstObjectByType<PlayerInputReader>();
            if (camera != null)
            {
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                camera.backgroundColor = new Color(0.01f, 0.015f, 0.025f);
            }

            var sections = Object.FindFirstObjectByType<LevelSections>();
            if (sections != null && reader != null)
            {
                EditorAssetUtility.Set(sections, "target", reader.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sections);
            }

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Conquest: lab demo ready. Press Play in {ScenePath}.");
            return true;
        }

        // ------------------------------------------------------------------ materials
        private static Dictionary<string, Material> BuildMaterials(LabPaletteEntry[] palette)
        {
            var materials = new Dictionary<string, Material>();
            foreach (LabPaletteEntry entry in palette)
            {
                Material material = ToonShading.SyncKitMaterial(MaterialFolder, entry.ToPaletteEntry());
                if (material == null) continue;
                if (!string.IsNullOrEmpty(entry.shadow))
                    material.SetColor("_ShadowColor", KitJson.Col(entry.shadow, new Color(0.62f, 0.5f, 0.62f)));
                EditorUtility.SetDirty(material);
                materials[entry.name] = material;
            }

            return materials;
        }

        // ------------------------------------------------------------------ piece prefabs
        private static Dictionary<string, GameObject> BuildPiecePrefabs(LabPieceEntry[] pieces,
            Dictionary<string, Material> materials)
        {
            var prefabs = new Dictionary<string, GameObject>();
            foreach (LabPieceEntry entry in pieces)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(entry.model);
                if (model == null)
                {
                    Debug.LogWarning($"Conquest: lab kit model missing: {entry.model}");
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

        private static bool IsLeaf(Transform t) => t.name.Contains("_Leaf_");
        private static bool IsCollisionShape(Transform t) => t.name.Contains("_COL_");

        private static GameObject BuildPiece(LabPieceEntry entry, GameObject model, Dictionary<string, Material> materials)
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
                renderer.shadowCastingMode = ShadowCastingMode.On;
                // dimmed per pixel outside the section the player is in (see LevelSections)
                if (entry.name != "Floor_Void_Slab")
                    renderer.renderingLayerMask |= LevelSections.SectionLayer;
            }

            foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>())
            {
                Transform t = filter.transform;
                if (IsCollisionShape(t))
                {
                    t.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    var hidden = t.GetComponent<MeshRenderer>();
                    if (hidden != null) hidden.enabled = false;
                }
                else if (IsLeaf(t))
                {
                    var box = t.gameObject.AddComponent<BoxCollider>();
                    Bounds b = filter.sharedMesh.bounds;
                    box.center = b.center;
                    box.size = b.size;
                }
                else if (entry.collider == "mesh")
                {
                    t.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                }
            }

            if (entry.collider == "box")
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = KitJson.Vec(entry.boxCenter);
                box.size = KitJson.Vec(entry.boxSize);
            }

            var lights = new List<Light>();
            if (entry.lights != null)
                for (int i = 0; i < entry.lights.Length; i++)
                    lights.Add(AddLight(root, entry.lights[i], i));

            if (entry.fade)
            {
                // walls and locked doors are cut when they face the camera, tall props only when in the way
                var occluder = root.AddComponent<FadeOccluder>();
                var so = new SerializedObject(occluder);
                so.FindProperty("isWall").boolValue = entry.category == "Walls" || entry.category == "Doors";
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (entry.isGate) AddGate(root, entry, materials, lights);

            // Everything that never moves is static; gates (leaves, status swaps) and lights stay dynamic.
            StaticEditorFlags flags = entry.isGate
                ? (StaticEditorFlags)0
                : StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic;
            foreach (Transform t in root.GetComponentsInChildren<Transform>())
                if (t.GetComponent<Light>() == null)
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, entry.fade ? flags & ~StaticEditorFlags.OccluderStatic : flags);
            return root;
        }

        private static Light AddLight(GameObject root, LabLightEntry entry, int index)
        {
            var go = new GameObject($"Light_{index}");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = KitJson.Vec(entry.offset);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = KitJson.Col(entry.color, new Color(0.62f, 0.85f, 1f));
            light.range = entry.range;
            light.intensity = entry.intensity;
            light.shadows = LightShadows.None;
            if (entry.flicker > 0f)
            {
                var flicker = go.AddComponent<FlameFlicker>();
                SetFloat(flicker, "amount", Mathf.Clamp01(entry.flicker));
                SetFloat(flicker, "speed", 9f);
            }

            return light;
        }

        private static void AddGate(GameObject root, LabPieceEntry entry, Dictionary<string, Material> materials,
            List<Light> lights)
        {
            var gate = root.AddComponent<Gate>();
            var so = new SerializedObject(gate);
            so.FindProperty("startsOpen").boolValue = entry.gateStartsOpen;
            so.FindProperty("autoOpen").boolValue = entry.gateAuto;
            so.FindProperty("travel").floatValue = entry.gateTravel;
            so.FindProperty("openSeconds").floatValue = entry.gateAuto ? 0.6f : 2.2f;
            if (materials.TryGetValue(ClosedStatus, out Material closed))
                so.FindProperty("closedStatus").objectReferenceValue = closed;
            if (materials.TryGetValue(OpenStatus, out Material open))
                so.FindProperty("openStatus").objectReferenceValue = open;
            // Event gates show their state in their lights too; a sliding door's lamp is plain lighting.
            SerializedProperty status = so.FindProperty("statusLights");
            List<Light> statusLights = entry.gateAuto ? new List<Light>() : lights;
            status.arraySize = statusLights.Count;
            for (int i = 0; i < statusLights.Count; i++) status.GetArrayElementAtIndex(i).objectReferenceValue = statusLights[i];
            AudioClip sound = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Combat/Shield_Up.wav");
            if (sound != null && !entry.gateAuto) so.FindProperty("moveSound").objectReferenceValue = sound;
            so.ApplyModifiedPropertiesWithoutUndo();

            Vector3 size = KitJson.Vec(entry.sensorSize);
            if (entry.gateAuto && size != Vector3.zero)
            {
                var sensor = new GameObject("Sensor") { layer = IgnoreRaycastLayer };
                sensor.transform.SetParent(root.transform, false);
                var box = sensor.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = KitJson.Vec(entry.sensorCenter);
                box.size = size;
                EditorAssetUtility.Set(sensor.AddComponent<GateSensor>(), "gate", gate);
            }
        }

        /// <summary>"Lab_Wall.001" / "Lab_Wall (Instance)" -> "Lab_Wall".</summary>
        private static string PaletteName(string materialName)
        {
            int cut = materialName.IndexOfAny(new[] { '.', ' ' });
            return cut > 0 ? materialName.Substring(0, cut) : materialName;
        }

        // ------------------------------------------------------------------ level
        private static GameObject BuildLevel(Dictionary<string, GameObject> prefabs)
        {
            if (!File.Exists(LayoutPath))
            {
                Debug.LogWarning($"Conquest: {LayoutPath} not found.");
                return null;
            }

            var layout = JsonUtility.FromJson<LabLayout>(File.ReadAllText(LayoutPath));
            var root = new GameObject(LevelName);
            var groups = new Dictionary<string, Transform>();
            var missing = new HashSet<string>();
            string[] sectionNames = layout.sectionMap?.names ?? System.Array.Empty<string>();
            var members = new List<GameObject>();
            var masks = new List<int>();

            foreach (LabPlacedPiece placed in layout.pieces)
            {
                if (!prefabs.TryGetValue(placed.piece, out GameObject prefab))
                {
                    if (missing.Add(placed.piece)) Debug.LogWarning($"Conquest: lab layout uses unknown piece '{placed.piece}'.");
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Group(root.transform, groups, placed.group));
                go.transform.localPosition = KitJson.Vec(placed.position);
                go.transform.localRotation = Quaternion.Euler(0f, placed.yaw, 0f);
                Vector3 scale = placed.scale != null && placed.scale.Length == 3 ? KitJson.Vec(placed.scale) : Vector3.one;
                if ((scale - Vector3.one).sqrMagnitude > 1e-6f) go.transform.localScale = scale;
                int mask = 0;
                if (placed.sections != null)
                    foreach (string section in placed.sections)
                    {
                        int index = System.Array.IndexOf(sectionNames, section);
                        if (index >= 0) mask |= 1 << index;
                    }

                if (mask != 0)
                {
                    members.Add(go);
                    masks.Add(mask);
                }
                if (!string.IsNullOrEmpty(placed.id))
                {
                    go.name = placed.id;
                    var gate = go.GetComponent<Gate>();
                    if (gate != null)
                    {
                        var so = new SerializedObject(gate);
                        so.FindProperty("gateId").stringValue = placed.id;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }

            Transform spawns = Group(root.transform, groups, "Spawns");
            for (int i = 0; i < layout.spawns.Length; i++)
            {
                LayoutSpawn spawn = layout.spawns[i];
                var go = new GameObject($"Spawn_{spawn.kind}_{i}");
                go.transform.SetParent(spawns, false);
                go.transform.localPosition = KitJson.Vec(spawn.position);
                go.transform.localRotation = Quaternion.Euler(0f, spawn.yaw, 0f);
                var point = go.AddComponent<SpawnPoint>();
                if (System.Enum.TryParse(spawn.kind, out SpawnKind kind)) EditorAssetUtility.Set(point, "kind", (int)kind);
            }

            if (layout.blockers != null && layout.blockers.Length > 0)
            {
                Transform blockers = Group(root.transform, groups, "Blockers");
                foreach (LayoutBlocker blocker in layout.blockers)
                {
                    var go = new GameObject("Blocker") { layer = IgnoreRaycastLayer };
                    go.transform.SetParent(blockers, false);
                    go.transform.localPosition = KitJson.Vec(blocker.position);
                    go.transform.localRotation = Quaternion.Euler(0f, blocker.yaw, 0f);
                    go.AddComponent<BoxCollider>().size = KitJson.Vec(blocker.size);
                }
            }

            // only the section the player stands in is shown (hub, corridor, each room ...)
            LabSectionMap map = layout.sectionMap;
            if (map != null && sectionNames.Length > 0)
                root.AddComponent<LevelSections>().Bake(sectionNames, new Vector2(map.originX, map.originZ),
                    map.cellSize, map.width, map.height, map.cells, members.ToArray(), masks.ToArray());

            EditorAssetUtility.EnsureFolder(DesertKitBuilder.LevelFolder);
            GameObject level = PrefabUtility.SaveAsPrefabAsset(root, $"{DesertKitBuilder.LevelFolder}/{LevelName}.prefab");
            Object.DestroyImmediate(root);
            return level;
        }

        /// <summary>Nested groups from "Hub/Walls"-style paths.</summary>
        private static Transform Group(Transform root, Dictionary<string, Transform> groups, string path)
        {
            if (string.IsNullOrEmpty(path)) path = "Misc";
            if (groups.TryGetValue(path, out Transform group)) return group;
            int cut = path.LastIndexOf('/');
            Transform parent = cut > 0 ? Group(root, groups, path.Substring(0, cut)) : root;
            group = new GameObject(cut > 0 ? path.Substring(cut + 1) : path).transform;
            group.SetParent(parent, false);
            groups[path] = group;
            return group;
        }

        // ------------------------------------------------------------------ atmosphere
        public static GameObject BuildAtmospherePrefab()
        {
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                Shader shader = Shader.Find("Conquest/Sky Gradient");
                if (shader != null)
                {
                    EditorAssetUtility.EnsureFolder(DesertArtDirection.RenderingFolder);
                    sky = new Material(shader);
                    AssetDatabase.CreateAsset(sky, SkyMaterialPath);
                }
            }

            if (sky != null)
            {
                // underground: no sky, just a deep blue-black void
                sky.SetColor("_ZenithColor", new Color(0.012f, 0.018f, 0.03f));
                sky.SetColor("_HorizonColor", new Color(0.03f, 0.045f, 0.07f));
                sky.SetColor("_GroundColor", new Color(0.01f, 0.012f, 0.02f));
                sky.SetFloat("_SunSize", 0f);
                sky.SetFloat("_CloudCover", 1f); // 1 = no clouds
                EditorUtility.SetDirty(sky);
            }

            var root = new GameObject("Lab_Atmosphere");
            var fillGo = new GameObject("Cold Fill Light");
            fillGo.transform.SetParent(root.transform, false);
            fillGo.transform.rotation = Quaternion.Euler(62f, -30f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.46f, 0.56f, 0.74f);
            fill.intensity = 0.5f;
            fill.shadows = LightShadows.Soft;
            fill.shadowStrength = 0.9f;

            var atmosphere = root.AddComponent<AtmosphereSettings>();
            var so = new SerializedObject(atmosphere);
            so.FindProperty("skybox").objectReferenceValue = sky;
            so.FindProperty("sun").objectReferenceValue = fill;
            so.FindProperty("skyColor").colorValue = new Color(0.24f, 0.29f, 0.39f);
            so.FindProperty("equatorColor").colorValue = new Color(0.13f, 0.16f, 0.22f);
            so.FindProperty("groundColor").colorValue = new Color(0.07f, 0.075f, 0.09f);
            so.FindProperty("fog").boolValue = true;
            so.FindProperty("fogColor").colorValue = new Color(0.035f, 0.05f, 0.08f);
            so.FindProperty("fogStart").floatValue = 20f;
            so.FindProperty("fogEnd").floatValue = 75f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 5f;
            volume.sharedProfile = BuildPostProfile();

            EditorAssetUtility.EnsureFolder(Path.GetDirectoryName(AtmospherePrefabPath)?.Replace('\\', '/'));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, AtmospherePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static VolumeProfile BuildPostProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile == null)
            {
                EditorAssetUtility.EnsureFolder(DesertArtDirection.RenderingFolder);
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostProfilePath);
            }

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(1.4f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(0.75f, 0.9f, 1f));

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.active = true;
            vignette.intensity.Override(0.38f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.0f, 0.01f, 0.03f));

            // concept mood: high contrast, deep shadows, muted paint
            ColorAdjustments grade = GetOrAdd<ColorAdjustments>(profile);
            grade.active = true;
            grade.contrast.Override(12f);
            grade.saturation.Override(-12f);
            grade.postExposure.Override(0.35f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ------------------------------------------------------------------ helpers
        private static void SetFloat(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Building opens a new scene, so keep unsaved work in already-saved scenes.</summary>
        private static void SaveOpenScenesWithPath()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            }
        }
    }
}
