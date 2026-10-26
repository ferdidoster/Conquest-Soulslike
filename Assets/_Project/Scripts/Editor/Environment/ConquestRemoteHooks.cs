using System.IO;
using Conquest.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Marker files in the project root that the editor picks up even while it is in the
    /// background, so art can be iterated on from outside Unity:
    ///   ConquestRefresh.pending   re-imports changed files (scripts, shaders, models)
    ///   ConquestRebuild.pending   rebuilds the desert kit prefabs and the Sandbox scene
    ///   ConquestCapture.pending   renders reference shots of the Sandbox arena into Captures/
    /// The same captures are available from Conquest > Capture Arena Shots.
    /// </summary>
    [InitializeOnLoad]
    internal static class ConquestRemoteHooks
    {
        private const string RefreshMarker = "ConquestRefresh.pending";
        private const string RebuildMarker = "ConquestRebuild.pending";
        private const string CaptureMarker = "ConquestCapture.pending";
        private const string CaptureFolder = "Captures";

        private static double _nextPoll;

        static ConquestRemoteHooks() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (File.Exists(RefreshMarker))
            {
                File.Delete(RefreshMarker);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return; // a script change reloads the domain; the rest runs on the next poll
            }

            if (File.Exists(RebuildMarker))
            {
                File.Delete(RebuildMarker);
                ConquestSetupMenu.Build(confirm: false);
                return;
            }

            if (File.Exists(CaptureMarker))
            {
                File.Delete(CaptureMarker);
                CaptureArenaShots();
            }
        }

        [MenuItem("Conquest/Capture Arena Shots", priority = 20)]
        public static void CaptureArenaShots()
        {
            if (EditorSceneManager.GetActiveScene().path != ConquestSetupMenu.SandboxScenePath)
                EditorSceneManager.OpenScene(ConquestSetupMenu.SandboxScenePath, OpenSceneMode.Single);

            Transform arena = FindArena();
            Directory.CreateDirectory(CaptureFolder);

            // Arena space: temple toward +Z, player enters from -Z.
            Shot(arena, "hero", new Vector3(-2f, 3.2f, -27f), new Vector3(0f, 7f, 12f), 55f, 0f);
            Shot(arena, "hero_floor", new Vector3(-9f, 2.0f, -15f), new Vector3(2f, 1.5f, 8f), 60f, 0f);
            IsoShot(arena, "iso_gameplay", new Vector3(0f, 1f, -8f), 6.5f);
            IsoShot(arena, "iso_temple", new Vector3(0f, 1f, 10f), 6.5f);
            IsoShot(arena, "iso_overview", new Vector3(0f, 1f, 2f), 26f);
            Debug.Log($"Conquest: arena shots written to {Path.GetFullPath(CaptureFolder)}.");
        }

        private static Transform FindArena()
        {
            SpawnPoint spawn = Object.FindFirstObjectByType<SpawnPoint>();
            if (spawn != null) return spawn.transform.parent != null ? spawn.transform.parent.parent : null;
            return null;
        }

        private static void Shot(Transform arena, string name, Vector3 localPos, Vector3 localTarget, float fov, float ortho)
        {
            Vector3 pos = arena != null ? arena.TransformPoint(localPos) : localPos;
            Vector3 target = arena != null ? arena.TransformPoint(localTarget) : localTarget;
            Render(name, pos, Quaternion.LookRotation(target - pos), fov, ortho);
        }

        /// <summary>Matches the isometric Cinemachine rig: 35 degree pitch, 45 degree yaw, 25 m back.</summary>
        private static void IsoShot(Transform arena, string name, Vector3 localTarget, float orthoSize)
        {
            Vector3 target = arena != null ? arena.TransformPoint(localTarget) : localTarget;
            Quaternion rotation = Quaternion.Euler(35f, 45f, 0f);
            Render(name, target - rotation * Vector3.forward * 40f, rotation, 0f, orthoSize);
        }

        private static void Render(string name, Vector3 position, Quaternion rotation, float fov, float orthoSize)
        {
            const int width = 1600, height = 900;
            var go = new GameObject("Capture Camera") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetPositionAndRotation(position, rotation);
            var camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.Skybox;
            if (orthoSize > 0f)
            {
                camera.orthographic = true;
                camera.orthographicSize = orthoSize;
            }
            else camera.fieldOfView = fov;

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(Path.Combine(CaptureFolder, name + ".png"), texture.EncodeToPNG());
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(go);
        }
    }
}
