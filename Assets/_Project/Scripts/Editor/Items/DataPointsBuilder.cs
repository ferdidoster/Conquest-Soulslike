using System.Collections.Generic;
using Conquest.Character;
using Conquest.Stats;
using Conquest.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Data points, the souls of Conquest: <see cref="DataPoints"/> and <see cref="DataPointLoss"/> on the
    /// player, a <see cref="DataPointReward"/> on every enemy and the counter on the HUD. New builds get it
    /// through <see cref="CharacterPrefabBuilder"/> and <see cref="SandboxSceneBuilder"/>; Conquest > Install
    /// Data Points (or ConquestDataPointsInstall.pending) adds it to the existing project in place.
    /// </summary>
    [InitializeOnLoad]
    public static class DataPointsBuilder
    {
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/Player_Knight.prefab";
        private const string BasePrefab = "Assets/_Project/Prefabs/Characters/Enemy_Bulwark.prefab";

        /// <summary>What each enemy is worth. The Bulwark is the base prefab, so it goes first.</summary>
        public static readonly (string prefab, int amount)[] Rewards =
        {
            (BasePrefab, 150),
            ("Assets/_Project/Prefabs/Characters/Enemy_Knight.prefab", 60),
            ("Assets/_Project/Prefabs/Characters/Enemy_Warden.prefab", 100),
        };

        private static readonly string[] Scenes =
        {
            "Assets/_Project/Scenes/Sandbox.unity",
            "Assets/_Project/Scenes/Lab_Demo.unity",
        };
        private const string InstallMarker = "ConquestDataPointsInstall.pending";

        private static double _nextPoll;

        static DataPointsBuilder()
        {
            SandboxSceneBuilder.SceneBuilt += (player, camera) => AddHud(player.GetComponent<DataPoints>());
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

        /// <summary>The wallet and the drop-on-death rule, on a player being built.</summary>
        public static void AttachToPlayer(GameObject root)
        {
            if (root.GetComponent<DataPoints>() == null) root.AddComponent<DataPoints>();
            var loss = root.GetComponent<DataPointLoss>();
            if (loss == null) loss = root.AddComponent<DataPointLoss>();
            EditorAssetUtility.Set(loss, "groundMask", 1 << 0); // the level geometry, not characters
        }

        /// <summary>Makes an enemy worth <paramref name="amount"/> data points.</summary>
        public static void SetReward(GameObject root, int amount)
        {
            var reward = root.GetComponent<DataPointReward>();
            if (reward == null) reward = root.AddComponent<DataPointReward>();
            EditorAssetUtility.Set(reward, "amount", amount);
        }

        public static int RewardFor(string prefabPath)
        {
            foreach ((string prefab, int amount) in Rewards)
                if (prefab == prefabPath) return amount;
            return 50;
        }

        /// <summary>The counter in the bottom-right corner of the scene's "HUD" canvas.</summary>
        public static DataPointCounter AddHud(DataPoints wallet)
        {
            GameObject hud = GameObject.Find("HUD");
            if (hud == null || wallet == null) return null;
            Transform old = hud.transform.Find("Data Points");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject("Data Points", typeof(RectTransform));
            go.transform.SetParent(hud.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-48f, 40f);
            rect.sizeDelta = new Vector2(400f, 90f);
            var counter = go.AddComponent<DataPointCounter>();
            EditorAssetUtility.Set(counter, "source", wallet);
            return counter;
        }

        // ---------- Install into the existing project ----------

        [MenuItem("Conquest/Install Data Points", priority = 4)]
        public static void Install()
        {
            var log = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene open = SceneManager.GetSceneAt(i);
                if (open.isDirty && !string.IsNullOrEmpty(open.path)) EditorSceneManager.SaveScene(open);
            }

            // 1. Prefabs: the player's wallet, and what each enemy is worth.
            EditPrefab(PlayerPrefab, AttachToPlayer);
            log.Add("Player_Knight: DataPoints, DataPointLoss");
            foreach ((string prefab, int amount) in Rewards)
            {
                if (!EditPrefab(prefab, root => SetReward(root, amount))) continue;
                log.Add($"{System.IO.Path.GetFileNameWithoutExtension(prefab)}: worth {amount}");
            }

            // 2. The counter on the HUD of the playable scenes.
            string active = SceneManager.GetActiveScene().path;
            foreach (string path in Scenes)
            {
                if (!System.IO.File.Exists(path)) continue;
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var wallet = Object.FindAnyObjectByType<DataPoints>(FindObjectsInactive.Include);
                if (AddHud(wallet) == null)
                {
                    log.Add($"{path}: no player or no HUD found");
                    continue;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)}: data point counter");
            }
            if (!string.IsNullOrEmpty(active) && SceneManager.GetActiveScene().path != active)
                EditorSceneManager.OpenScene(active, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            string report = "Conquest: data points installed.\n- " + string.Join("\n- ", log);
            System.IO.Directory.CreateDirectory("Captures");
            System.IO.File.WriteAllText("Captures/datapoints_install.txt", report);
            Debug.Log(report);
        }

        private static bool EditPrefab(string path, System.Action<GameObject> edit)
        {
            if (!System.IO.File.Exists(path)) return false;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            edit(contents);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            return true;
        }
    }
}
