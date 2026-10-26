using System.IO;
using System.Linq;
using Conquest.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Renders a still of the combat effects without entering Play mode: the player mid-strike,
    /// an enemy reeling, a frozen blood burst, splats on the floor and a spark burst. Written to
    /// Captures/combat_fx.png. Also runs when ConquestFxCapture.pending appears in the project root.
    /// </summary>
    [InitializeOnLoad]
    internal static class CombatFeedbackPreview
    {
        private const string Marker = "ConquestFxCapture.pending";
        private const string KnightModel = "Assets/_Project/Art/Characters/Knight/Knight.fbx";
        private static double _nextPoll;

        static CombatFeedbackPreview() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Marker))
                return;
            // Let a pending rebuild go first so the capture shows the new assets.
            if (File.Exists("ConquestRebuild.pending") || File.Exists("ConquestRefresh.pending")) return;

            File.Delete(Marker);
            Capture();
        }

        [MenuItem("Conquest/Capture Combat Effects", priority = 21)]
        public static void Capture()
        {
            // Keep unsaved work in saved scenes, like the sandbox build does.
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            }

            EditorSceneManager.OpenScene(ConquestSetupMenu.SandboxScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var player = Object.FindFirstObjectByType<PlayerInputReader>();
            if (player == null)
            {
                Debug.LogError("Conquest: no player in the Sandbox scene.");
                return;
            }

            var temp = new GameObject("FX Preview (temporary)");
            try
            {
                Stage(player.transform, temp.transform);
                Directory.CreateDirectory("Captures");
                Vector3 focus = player.transform.position + player.transform.forward * 1.2f + Vector3.up;
                Render("combat_fx", focus, 3.2f);
                Debug.Log($"Conquest: combat effects still written to {Path.GetFullPath("Captures/combat_fx.png")}.");
            }
            finally
            {
                Object.DestroyImmediate(temp);
                // Discard the posed characters.
                EditorSceneManager.OpenScene(ConquestSetupMenu.SandboxScenePath, OpenSceneMode.Single);
            }
        }

        private static void Stage(Transform player, Transform temp)
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(KnightModel).OfType<AnimationClip>().ToArray();
            AnimationClip Clip(string name) => clips.FirstOrDefault(c => AnimatorControllerBuilder.StateName(c.name) == name);

            // Player mid-strike; an enemy two metres ahead, reeling.
            Pose(player, Clip("Attack_Heavy"), 28f / 30f);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/Enemy_Knight.prefab");
            if (enemyPrefab != null)
            {
                GameObject enemy = Object.Instantiate(enemyPrefab, temp);
                enemy.transform.SetPositionAndRotation(player.position + player.forward * 2.1f,
                    Quaternion.LookRotation(-player.forward));
                Pose(enemy.transform, Clip("Stagger"), 5f / 30f);
            }

            Vector3 wound = player.position + player.forward * 1.8f + Vector3.up * 1.2f;
            Vector3 flow = (player.forward + player.right * 0.4f).normalized;

            var burst = AssetDatabase.LoadAssetAtPath<ParticleSystem>($"{CombatFeedbackBuilder.Folder}/VFX_BloodBurst.prefab");
            Freeze(burst, temp, wound, Quaternion.LookRotation(flow + Vector3.up * 0.35f), 1.3f, 0.12f);
            var sparks = AssetDatabase.LoadAssetAtPath<ParticleSystem>($"{CombatFeedbackBuilder.Folder}/VFX_BlockSparks.prefab");
            Freeze(sparks, temp, player.position - player.right * 2.2f + player.forward * 0.5f + Vector3.up * 1.2f,
                Quaternion.LookRotation(-player.forward + Vector3.up * 0.3f), 1f, 0.04f);

            var splat = AssetDatabase.LoadAssetAtPath<Material>($"{CombatFeedbackBuilder.Folder}/Blood_Splat.mat");
            var random = new System.Random(3);
            for (int i = 0; i < 9; i++)
            {
                float distance = 0.3f + (float)random.NextDouble() * 2.2f;
                float side = ((float)random.NextDouble() - 0.5f) * distance * 0.7f;
                Vector3 at = wound + flow * distance + Vector3.Cross(Vector3.up, flow) * side;
                Splat(splat, temp, at, Mathf.Lerp(0.75f, 0.28f, distance / 2.5f));
            }

            Splat(splat, temp, player.position + player.forward * 2.6f, 2f);
        }

        private static void Pose(Transform character, AnimationClip clip, float time)
        {
            Animator animator = character.GetComponentInChildren<Animator>();
            if (clip != null && animator != null) clip.SampleAnimation(animator.gameObject, time);
        }

        private static void Freeze(ParticleSystem prefab, Transform parent, Vector3 position, Quaternion rotation,
            float scale, float time)
        {
            if (prefab == null) return;
            ParticleSystem ps = Object.Instantiate(prefab, position, rotation, parent);
            ps.transform.localScale = Vector3.one * scale;
            ps.Simulate(time, true, true);
        }

        private static void Splat(Material material, Transform parent, Vector3 above, float size)
        {
            bool hit = Physics.Raycast(above + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 5f, 1,
                QueryTriggerInteraction.Ignore);
            if (material == null || !hit) return;

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, true);
            quad.transform.SetPositionAndRotation(ground.point + ground.normal * 0.01f,
                Quaternion.LookRotation(-ground.normal) * Quaternion.Euler(0f, 0f, size * 977f));
            quad.transform.localScale = Vector3.one * size;
            quad.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Render(string name, Vector3 focus, float orthoSize)
        {
            const int width = 1600, height = 900;
            Quaternion rotation = Quaternion.Euler(35f, 45f, 0f);
            var go = new GameObject("Capture Camera") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * 40f, rotation);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthoSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(Path.Combine("Captures", name + ".png"), texture.EncodeToPNG());
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(go);
        }
    }
}
