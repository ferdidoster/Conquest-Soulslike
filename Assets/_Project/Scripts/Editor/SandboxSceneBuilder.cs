using System.Collections.Generic;
using Conquest.CameraSystem;
using Conquest.Environment;
using Conquest.Player;
using Conquest.Stats;
using Conquest.Targeting;
using Conquest.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Conquest.EditorTools
{
    /// <summary>
    /// The sandbox: a level (the desert boss arena when the kit is built, otherwise a plain
    /// floor with pillars), the player and sparring knights on the level's spawn points, and a HUD.
    /// </summary>
    public static class SandboxSceneBuilder
    {
        /// <summary>
        /// Raised after the scene content is created and before it is saved, with the player and
        /// the main camera. Optional modules (e.g. the Cinemachine rig) hook in here.
        /// </summary>
        public static event System.Action<GameObject, Camera> SceneBuilt;

        /// <summary>
        /// Yaw of the arena in the scene. The isometric camera looks along +45 degrees, so this puts
        /// the temple straight ahead of it, at the top of the screen.
        /// </summary>
        private const float ArenaYaw = 45f;

        public static Scene Build(GameObject playerPrefab, GameObject enemyPrefab, string scenePath, string materialFolder,
            DesertKitBuilder.Result environment = null, GameObject heavyEnemyPrefab = null,
            GameObject gunnerPrefab = null)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            (Pose playerSpawn, List<Pose> enemySpawns) = environment?.Arena != null
                ? PlaceLevel(environment)
                : BuildArena(materialFolder);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(playerSpawn.position, playerSpawn.rotation);

            for (int i = 0; i < enemySpawns.Count; i++)
            {
                Pose spot = enemySpawns[i];
                // The last spot gets the heavy enemy, when there is one.
                bool heavy = heavyEnemyPrefab != null && i == enemySpawns.Count - 1;
                // The first spot gets the stationary gunner, when there is one and spots to spare.
                bool gunner = gunnerPrefab != null && i == 0 && enemySpawns.Count > 1;
                GameObject prefab = gunner ? gunnerPrefab : heavy ? heavyEnemyPrefab : enemyPrefab;
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                enemy.transform.SetPositionAndRotation(spot.position, spot.rotation);
            }

            Camera camera = Camera.main;
            var reader = player.GetComponent<PlayerInputReader>();
            var lockOn = player.GetComponent<LockOnController>();
            EditorAssetUtility.Set(reader, "view", camera.transform);
            EditorAssetUtility.Set(lockOn, "view", camera.transform);

            var orbit = camera.gameObject.AddComponent<ThirdPersonCamera>();
            EditorAssetUtility.Set(orbit, "followTarget", player.transform);
            EditorAssetUtility.Set(orbit, "lookSource", reader);
            EditorAssetUtility.Set(orbit, "targetSource", lockOn);
            EditorAssetUtility.Set(orbit, "collisionMask", 1 << 0);
            camera.transform.SetPositionAndRotation(player.transform.TransformPoint(new Vector3(0f, 2.5f, -4.5f)),
                player.transform.rotation * Quaternion.Euler(15f, 0f, 0f));
            camera.nearClipPlane = 0.05f;

            BuildHud(player, lockOn, camera);
            SceneBuilt?.Invoke(player, camera);

            EditorAssetUtility.EnsureFolder(System.IO.Path.GetDirectoryName(scenePath)?.Replace('\\', '/'));
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                Debug.LogError($"Conquest: could not save the sandbox scene to {scenePath}.");
            AddToBuildSettings(scenePath);
            return scene;
        }

        /// <summary>Places a level prefab plus its atmosphere and returns its spawn points.</summary>
        private static (Pose, List<Pose>) PlaceLevel(DesertKitBuilder.Result environment)
        {
            if (environment.Atmosphere != null)
            {
                foreach (Light light in Object.FindObjectsByType<Light>())
                    if (light.type == LightType.Directional) Object.DestroyImmediate(light.gameObject);
                PrefabUtility.InstantiatePrefab(environment.Atmosphere);
            }

            var level = (GameObject)PrefabUtility.InstantiatePrefab(environment.Arena);
            level.transform.rotation = Quaternion.Euler(0f, ArenaYaw, 0f);

            var player = new Pose(Vector3.zero, Quaternion.identity);
            var enemies = new List<Pose>();
            foreach (SpawnPoint spawn in level.GetComponentsInChildren<SpawnPoint>())
            {
                if (spawn.Kind == SpawnKind.Player) player = spawn.Pose;
                else enemies.Add(spawn.Pose);
            }

            return (player, enemies);
        }

        private static (Pose, List<Pose>) BuildArena(string materialFolder)
        {
            Material ground = SolidMaterial(materialFolder + "/Ground.mat", new Color(0.32f, 0.3f, 0.27f));
            Material stone = SolidMaterial(materialFolder + "/Stone.mat", new Color(0.5f, 0.48f, 0.45f));

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground";
            floor.transform.localScale = new Vector3(8f, 1f, 8f);
            floor.GetComponent<Renderer>().sharedMaterial = ground;

            var arena = new GameObject("Arena");
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = "Pillar";
                pillar.transform.SetParent(arena.transform);
                pillar.transform.position = new Vector3(Mathf.Cos(angle) * 18f, 2f, Mathf.Sin(angle) * 18f + 6f);
                pillar.transform.localScale = new Vector3(1.4f, 4f, 1.4f);
                pillar.GetComponent<Renderer>().sharedMaterial = stone;
            }

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Cover Wall";
            wall.transform.SetParent(arena.transform);
            wall.transform.position = new Vector3(-4f, 1f, 4f);
            wall.transform.localScale = new Vector3(3f, 2f, 0.6f);
            wall.GetComponent<Renderer>().sharedMaterial = stone;

            var enemies = new List<Pose>();
            foreach (Vector3 spot in new[] { new Vector3(0f, 0f, 8f), new Vector3(-6f, 0f, 12f), new Vector3(7f, 0f, 14f) })
                enemies.Add(new Pose(spot, Quaternion.LookRotation(-spot.normalized)));
            return (new Pose(Vector3.zero, Quaternion.identity), enemies);
        }

        private static void BuildHud(GameObject player, LockOnController lockOn, Camera camera)
        {
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Bar(canvasGo.transform, "Health Bar", player.GetComponent<Health>(), new Vector2(40f, -40f), 420f,
                new Color(0.65f, 0.08f, 0.08f));
            Bar(canvasGo.transform, "Stamina Bar", player.GetComponent<Stamina>(), new Vector2(40f, -72f), 320f,
                new Color(0.2f, 0.55f, 0.2f));

            var marker = new GameObject("Lock-On Marker", typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(canvasGo.transform, false);
            var markerRect = (RectTransform)marker.transform;
            markerRect.sizeDelta = new Vector2(14f, 14f);
            marker.GetComponent<Image>().color = new Color(1f, 0.95f, 0.85f, 0.9f);
            marker.SetActive(false);

            var reticle = canvasGo.AddComponent<LockOnReticle>();
            reticle.Bind(lockOn, markerRect, camera);
        }

        private static void Bar(Transform parent, string name, MonoBehaviour source, Vector2 position, float width,
            Color color)
        {
            RectTransform frame = Rect(parent, name, new Color(0f, 0f, 0f, 0.6f));
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0f, 1f);
            frame.anchoredPosition = position;
            frame.sizeDelta = new Vector2(width, 16f);

            RectTransform trail = Rect(frame, "Trail", new Color(0.9f, 0.75f, 0.3f, 0.8f));
            RectTransform fill = Rect(frame, "Fill", color);
            foreach (RectTransform r in new[] { trail, fill })
            {
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(2f, 2f);
                r.offsetMax = new Vector2(-2f, -2f);
            }

            var bar = frame.gameObject.AddComponent<ResourceBar>();
            EditorAssetUtility.Set(bar, "source", source);
            EditorAssetUtility.Set(bar, "fill", fill);
            EditorAssetUtility.Set(bar, "trail", trail);
        }

        private static RectTransform Rect(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        private static Material SolidMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EditorAssetUtility.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            Shader shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial.shader
                : Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void AddToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
