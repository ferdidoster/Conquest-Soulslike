using System.Collections.Generic;
using System.IO;
using Conquest.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Bakes the enemies' NavMesh for the open scene into Resources/NavMesh/&lt;scene&gt;.asset, where
    /// <see cref="LevelNavMesh"/> picks it up at runtime. Runs every time Play mode starts in a scene with
    /// AI brains (so a rebuilt level is never stale) and from Conquest &gt; Bake Enemy NavMesh before a build.
    /// The level meshes are not Read/Write, so the bake reads temporary readable copies of them.
    /// </summary>
    [InitializeOnLoad]
    internal static class EnemyNavMeshBaker
    {
        private const string Folder = "Assets/_Project/Resources/NavMesh";

        static EnemyNavMeshBaker() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            if (Object.FindAnyObjectByType<MeleeAIBrain>() == null && Object.FindAnyObjectByType<TurretAIBrain>() == null)
                return;
            Bake(log: false);
        }

        [MenuItem("Conquest/Bake Enemy NavMesh", priority = 22)]
        private static void BakeMenu() => Bake(log: true);

        public static bool Bake(bool log)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.name)) return false;

            var timer = System.Diagnostics.Stopwatch.StartNew();
            var sources = new List<NavMeshBuildSource>();
            if (!LevelNavMesh.CollectSources(sources, out Bounds bounds))
            {
                Debug.LogWarning($"Conquest: nothing to bake a NavMesh from in {scene.name}.");
                return false;
            }

            var copies = new Dictionary<Mesh, Mesh>();
            try
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    NavMeshBuildSource source = sources[i];
                    if (source.shape != NavMeshBuildSourceShape.Mesh || !(source.sourceObject is Mesh mesh) || mesh.isReadable)
                        continue;
                    if (!copies.TryGetValue(mesh, out Mesh copy))
                    {
                        copy = Object.Instantiate(mesh);
                        copy.hideFlags = HideFlags.HideAndDontSave;
                        copies[mesh] = copy;
                    }

                    source.sourceObject = copy;
                    sources[i] = source;
                }

                NavMeshData data = LevelNavMesh.Build(sources, bounds);
                if (data == null)
                {
                    Debug.LogWarning($"Conquest: NavMesh bake failed for {scene.name}.");
                    return false;
                }

                Save(data, scene.name);
                if (log)
                    Debug.Log($"Conquest: baked the {scene.name} enemy NavMesh ({sources.Count} colliders) in {timer.ElapsedMilliseconds} ms.");
                return true;
            }
            finally
            {
                foreach (Mesh copy in copies.Values) Object.DestroyImmediate(copy);
            }
        }

        private static void Save(NavMeshData data, string sceneName)
        {
            Directory.CreateDirectory(Folder);
            string path = $"{Folder}/{sceneName}.asset";
            data.name = $"{sceneName} NavMesh";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing != null)
            {
                // keep the asset (and its GUID); just swap the contents
                EditorUtility.CopySerialized(data, existing);
                Object.DestroyImmediate(data);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(data, path);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
