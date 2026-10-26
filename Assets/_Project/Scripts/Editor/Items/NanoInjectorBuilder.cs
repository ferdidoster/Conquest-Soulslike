using System.Collections.Generic;
using System.Linq;
using Conquest.Character;
using Conquest.Environment;
using Conquest.Feedback;
using Conquest.Stats;
using Conquest.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Conquest.EditorTools
{
    /// <summary>
    /// The nano-injector heal: wires <see cref="NanoInjector"/>, <see cref="NanoInjectorFeedback"/> and
    /// <see cref="PlayerRespawn"/> onto the player, builds its effects, adds the charge pips to the HUD
    /// and a checkpoint at the player's spawn. New scenes get it through <see cref="SandboxSceneBuilder"/>;
    /// Conquest > Install Nano-Injector (or ConquestInjectorInstall.pending) adds it to the existing
    /// project in place, without rebuilding prefabs or scenes.
    /// </summary>
    [InitializeOnLoad]
    public static class NanoInjectorBuilder
    {
        /// <summary>The checkpoint materials; the effects go with the combat ones in Art/VFX/Combat.</summary>
        public const string Folder = "Assets/_Project/Art/VFX/Injector";
        private const string KnightModel = "Assets/_Project/Art/Characters/Knight/Knight.fbx";
        private const string AnimationFolder = "Assets/_Project/Art/Characters/Knight/Animation";
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/Player_Knight.prefab";
        private static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/Sandbox.unity",
            "Assets/_Project/Scenes/Lab_Demo.unity",
        };
        private const string InstallMarker = "ConquestInjectorInstall.pending";

        public static readonly Color NaniteBlue = new Color(0.25f, 0.62f, 1f);

        private static double _nextPoll;

        static NanoInjectorBuilder()
        {
            SandboxSceneBuilder.SceneBuilt += OnSceneBuilt;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !System.IO.File.Exists(InstallMarker))
                return;
            System.IO.File.Delete(InstallMarker);
            Install();
        }

        // ---------- Player ----------

        /// <summary>Adds the injector, its feedback and checkpoint respawning to a player being built.</summary>
        public static void AttachToPlayer(GameObject root)
        {
            var injector = root.GetComponent<NanoInjector>();
            if (injector == null) injector = root.AddComponent<NanoInjector>();
            if (root.GetComponent<PlayerRespawn>() == null) root.AddComponent<PlayerRespawn>();

            var feedback = root.GetComponent<NanoInjectorFeedback>();
            if (feedback == null) feedback = root.AddComponent<NanoInjectorFeedback>();

            Transform cartridge = EditorAssetUtility.FindDeep(root.transform, "Injector_Hand");
            if (cartridge != null) cartridge.gameObject.SetActive(false);
            EditorAssetUtility.Set(feedback, "injector", injector);
            EditorAssetUtility.Set(feedback, "flash", root.GetComponent<HitFlash>());
            EditorAssetUtility.Set(feedback, "cartridge", cartridge != null ? cartridge.gameObject : null);
            EditorAssetUtility.Set(feedback, "port", EditorAssetUtility.FindDeep(root.transform, "Injector_Port"));
            EditorAssetUtility.Set(feedback, "tip", EditorAssetUtility.FindDeep(root.transform, "Injector_Tip"));
            var tuning = new SerializedObject(feedback);
            tuning.FindProperty("flashDuration").floatValue = 0.2f;
            tuning.ApplyModifiedPropertiesWithoutUndo();

            Material spark = CombatFeedbackBuilder.SplatMaterial("Spark", Color.white, Color.white, 0f, false,
                vertexColor: true, shadows: false);
            EditorAssetUtility.Set(feedback, "slamBurst", SlamBurst(spark));
            EditorAssetUtility.Set(feedback, "tossPuff", TossPuff(spark));
            ParticleSystem motes = RepairMotes(root, spark);
            EditorAssetUtility.SetLayerRecursively(motes.gameObject, root.layer);
            EditorAssetUtility.Set(feedback, "repairMotes", motes);
            EditorAssetUtility.Set(feedback, "drawSound", Clip("Injector_Draw"));
            EditorAssetUtility.Set(feedback, "slamSound", Clip("Injector_Slam"));
            EditorAssetUtility.Set(feedback, "repairSound", Clip("Nanite_Repair"));
            EditorAssetUtility.Set(feedback, "emptySound", Clip("Injector_Empty"));
            EditorAssetUtility.Set(feedback, "refillSound", Clip("Injector_Refill"));
        }

        private static AudioClip Clip(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"{CombatFeedbackBuilder.AudioFolder}/{name}.wav");

        // ---------- Effects ----------

        private static ParticleSystem SlamBurst(Material spark)
        {
            // Saved with the other combat effects in Art/VFX/Combat.
            return CombatFeedbackBuilder.Sparks("VFX_NaniteSlam", spark, 22, 0.55f, new Color(0.75f, 0.92f, 1f), NaniteBlue);
        }

        private static ParticleSystem TossPuff(Material spark)
        {
            var root = new GameObject("VFX_NaniteToss");
            ParticleSystem motes = CombatFeedbackBuilder.Emitter(root, spark, 10, lifetime: (0.25f, 0.5f),
                speed: (0.4f, 1.4f), size: (0.025f, 0.05f), gravity: -0.1f, coneAngle: 70f, stretch: false);
            var main = motes.main;
            main.startColor = new ParticleSystem.MinMaxGradient(NaniteBlue, new Color(0.7f, 0.9f, 1f));
            return CombatFeedbackBuilder.SavePrefab(root);
        }

        /// <summary>Blue motes drifting up around the body while the nanites repair; a child of the player.</summary>
        private static ParticleSystem RepairMotes(GameObject player, Material spark)
        {
            Transform existing = player.transform.Find("Nanite Motes");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject("Nanite Motes");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // emit upward
            ParticleSystem motes = CombatFeedbackBuilder.Emitter(go, spark, 1, lifetime: (0.6f, 1.1f),
                speed: (0.6f, 1.6f), size: (0.03f, 0.075f), gravity: -0.05f, coneAngle: 0f, stretch: false);
            var main = motes.main;
            main.loop = true;
            main.duration = 1f;
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(NaniteBlue, new Color(0.75f, 0.95f, 1f));
            var emission = motes.emission;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            emission.rateOverTime = 90f;
            var shape = motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.42f;
            shape.radiusThickness = 0.3f;
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 1.4f;
            var colour = motes.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
            return motes;
        }

        // ---------- Scene: HUD pips and a checkpoint ----------

        private static void OnSceneBuilt(GameObject player, Camera camera)
        {
            var injector = player.GetComponent<NanoInjector>();
            if (injector == null) return;
            AddHud(injector);
            AddCheckpoint(player.transform.position, player.transform.rotation);
        }

        /// <summary>The cartridge pips under the stamina bar, on the scene's "HUD" canvas.</summary>
        public static ChargeCounter AddHud(NanoInjector injector)
        {
            GameObject hud = GameObject.Find("HUD");
            if (hud == null) return null;
            Transform old = hud.transform.Find("Injector Charges");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject("Injector Charges", typeof(RectTransform));
            go.transform.SetParent(hud.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(40f, -98f);
            rect.sizeDelta = new Vector2(200f, 30f);
            var counter = go.AddComponent<ChargeCounter>();
            EditorAssetUtility.Set(counter, "source", injector);
            return counter;
        }

        /// <summary>A checkpoint at the player's spawn: a small plinth with a glowing nanite core and a beacon.</summary>
        public static Checkpoint AddCheckpoint(Vector3 spawn, Quaternion facing)
        {
            foreach (Checkpoint old in Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                if (old.name == "Checkpoint (Spawn)") Object.DestroyImmediate(old.gameObject);

            var root = new GameObject("Checkpoint (Spawn)");
            root.transform.SetPositionAndRotation(spawn, facing);
            var checkpoint = root.AddComponent<Checkpoint>();

            var respawn = new GameObject("Respawn Point");
            respawn.transform.SetParent(root.transform, false);

            // The station stands beside the spawn (behind and to the left) so the knight does not start inside it.
            var station = new GameObject("Station");
            station.transform.SetParent(root.transform, false);
            station.transform.localPosition = new Vector3(-1.6f, 0f, -0.9f);
            Material stone = ToonMaterial("Checkpoint_Stone", new Color(0.2f, 0.21f, 0.24f), Color.black);
            Material glow = ToonMaterial("Checkpoint_Glow", NaniteBlue, NaniteBlue * 1.1f);
            Part(station, PrimitiveType.Cylinder, "Plinth", new Vector3(0f, 0.35f, 0f), new Vector3(0.55f, 0.35f, 0.55f),
                Quaternion.identity, stone, keepCollider: true);
            Part(station, PrimitiveType.Cylinder, "Cap", new Vector3(0f, 0.73f, 0f), new Vector3(0.7f, 0.04f, 0.7f),
                Quaternion.identity, stone, keepCollider: false);
            GameObject core = Part(station, PrimitiveType.Cube, "Core", new Vector3(0f, 1.05f, 0f), Vector3.one * 0.26f,
                Quaternion.Euler(45f, 0f, 45f), glow, keepCollider: false);
            var hover = core.AddComponent<Hover>();
            var hoverSo = new SerializedObject(hover);
            hoverSo.FindProperty("height").floatValue = 0.05f;
            hoverSo.FindProperty("period").floatValue = 3f;
            hoverSo.FindProperty("sway").floatValue = 10f;
            hoverSo.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Part(station, PrimitiveType.Cube, "Rune", new Vector3(Mathf.Cos(angle) * 0.33f, 0.5f, Mathf.Sin(angle) * 0.33f),
                    new Vector3(0.05f, 0.22f, 0.05f), Quaternion.Euler(0f, -i * 60f, 0f), glow, keepCollider: false);
            }

            var lightGo = new GameObject("Beacon");
            lightGo.transform.SetParent(station.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            var beacon = lightGo.AddComponent<Light>();
            beacon.type = LightType.Point;
            beacon.color = NaniteBlue;
            beacon.range = 6f;
            beacon.intensity = 1.2f;
            beacon.shadows = LightShadows.None;

            EditorAssetUtility.Set(checkpoint, "respawnPoint", respawn.transform);
            EditorAssetUtility.Set(checkpoint, "beacon", beacon);
            var so = new SerializedObject(checkpoint);
            so.FindProperty("radius").floatValue = 2.4f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return checkpoint;
        }

        private static GameObject Part(GameObject parent, PrimitiveType type, string name, Vector3 position, Vector3 scale,
            Quaternion rotation, Material material, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static Material ToonMaterial(string name, Color color, Color emission)
        {
            EditorAssetUtility.EnsureFolder(Folder);
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = ToonShading.Shader;
            if (material == null)
            {
                material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_EmissionColor", emission);
            if (material.HasProperty("_GritStrength")) material.SetFloat("_GritStrength", 0.05f);
            if (emission.maxColorComponent > 0f) material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- Install into the existing project ----------

        [MenuItem("Conquest/Install Nano-Injector", priority = 3)]
        public static void Install()
        {
            var log = new List<string>();
            ConquestSetupMenuSave();

            // 1. The heal clips and the cartridge and port meshes come with the re-exported knight.
            AssetDatabase.ImportAsset(KnightModel, ImportAssetOptions.ForceUpdate);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimationFolder + "/Knight.controller");
            if (controller == null)
            {
                Debug.LogError("Conquest: Knight.controller missing; run Conquest > Build Knight Sandbox instead.");
                return;
            }
            List<string> added = AnimatorControllerBuilder.AddMissingOptionalStates(controller, KnightModel);
            log.Add(added.Count > 0 ? $"added states {string.Join(", ", added)} to Knight.controller" : "Knight.controller already has Heal");
            AnimatorControllerBuilder.BuildOverride(controller, KnightModel, AnimatorControllerBuilder.TwoHandedSuffix,
                AnimationFolder + "/Knight_TwoHanded.overrideController");
            log.Add("refreshed Knight_TwoHanded.overrideController (Heal_2H)");

            // 2. The player prefab.
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            AttachToPlayer(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefab);
            PrefabUtility.UnloadPrefabContents(contents);
            log.Add("Player_Knight: NanoInjector, NanoInjectorFeedback, PlayerRespawn");

            // 3. HUD pips and a spawn checkpoint in the playable scenes.
            string open = SceneManager.GetActiveScene().path;
            foreach (string path in Scenes)
            {
                if (!System.IO.File.Exists(path)) continue;
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var injector = Object.FindAnyObjectByType<NanoInjector>(FindObjectsInactive.Include);
                if (injector == null)
                {
                    log.Add($"{path}: no player found");
                    continue;
                }
                AddHud(injector);
                AddCheckpoint(injector.transform.position, injector.transform.rotation);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)}: charge pips and spawn checkpoint");
            }
            if (!string.IsNullOrEmpty(open) && SceneManager.GetActiveScene().path != open)
                EditorSceneManager.OpenScene(open, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            string report = "Conquest: nano-injector installed.\n- " + string.Join("\n- ", log);
            System.IO.Directory.CreateDirectory("Captures");
            System.IO.File.WriteAllText("Captures/injector_install.txt", report);
            Debug.Log(report);
        }

        /// <summary>Opening scenes replaces the current one, so keep unsaved work in saved scenes first.</summary>
        private static void ConquestSetupMenuSave()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            }
        }
    }
}
