using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Renders the player character with the in-game shaders, isolated in a preview scene, so its
    /// look can be compared with the concept art without touching any open scene:
    ///   front, side, back and 3/4 of the idle pose, plus a strip of action poses.
    /// Runs from Conquest > Capture Character Shots, or in the background when the marker file
    /// ConquestCharacterCapture.pending exists in the project root. Images go to Captures/Character.
    /// </summary>
    [InitializeOnLoad]
    internal static class CharacterCaptureHook
    {
        private const string Marker = "ConquestCharacterCapture.pending";
        private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/Player_Knight.prefab";
        private const string Folder = "Captures/Character";
        private const int Width = 900, Height = 1300;

        /// <summary>Clip name and normalized time for the action strip.</summary>
        private static readonly (string clip, float time)[] ActionPoses =
        {
            ("Run_F", 0.3f), ("Attack_1", 0.4f), ("Attack_3", 0.35f), ("Attack_Heavy", 0.5f),
            ("Roll_F", 0.35f), ("Guard_Idle", 0f), ("Parry", 0.4f), ("Stagger", 0.3f),
        };

        private static double _nextPoll;
        private static readonly System.Text.StringBuilder Log = new System.Text.StringBuilder();

        static CharacterCaptureHook() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1.5;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Marker))
                return;

            File.Delete(Marker);
            Capture();
        }

        [MenuItem("Conquest/Capture Character Shots", priority = 21)]
        public static void Capture()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (prefab == null)
            {
                Debug.LogError($"Conquest: {PlayerPrefab} not found; build the sandbox first.");
                return;
            }

            Directory.CreateDirectory(Folder);
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var character = (GameObject)Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(character, scene);
                AddLight(scene);

                // Several poses are rendered within one editor frame, so skinning must refresh per render.
                foreach (var skin in character.GetComponentsInChildren<SkinnedMeshRenderer>())
                    skin.forceMatrixRecalculationPerRender = true;

                Animator animator = character.GetComponentInChildren<Animator>();
                Dictionary<string, AnimationClip> clips = animator != null && animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.animationClips.GroupBy(c => AnimatorControllerBuilder.StateName(c.name)).ToDictionary(g => g.Key, g => g.First())
                    : new Dictionary<string, AnimationClip>();

                Posed(animator, clips, "Idle", 0f, () =>
                {
                    Shot(scene, "front", 0f, 2.3f);
                    Shot(scene, "side", 90f, 2.3f);
                    Shot(scene, "back", 180f, 2.3f);
                    Shot(scene, "three_quarter", 35f, 2.3f);
                    Shot(scene, "three_quarter_back", 145f, 2.3f);
                });

                foreach ((string clip, float time) in ActionPoses)
                {
                    Posed(animator, clips, clip, time, () => Shot(scene, "pose_" + clip, 35f, 2.8f));
                }

                Log.AppendLine($"clips: {clips.Count}, animator: {animator?.name} on {animator?.transform.parent?.name}");
                File.WriteAllText(Path.Combine(Folder, "capture_log.txt"), Log.ToString());
                Log.Clear();
                Debug.Log($"Conquest: character shots written to {Path.GetFullPath(Folder)}.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>
        /// Poses the character with one clip through a playable graph (works for Generic rigs in edit mode)
        /// and runs <paramref name="render"/> while the pose holds.
        /// </summary>
        private static void Posed(Animator animator, Dictionary<string, AnimationClip> clips, string name,
            float normalizedTime, System.Action render)
        {
            if (animator == null || !clips.TryGetValue(name, out AnimationClip clip)) return;
            PlayableGraph graph = PlayableGraph.Create("Conquest Capture");
            try
            {
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetTime(clip.length * normalizedTime);
                AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable);
                Transform hand = animator.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Hand.R");
                Vector3 before = hand != null ? hand.position : Vector3.zero;
                graph.Evaluate();
                Log.AppendLine($"{name}@{normalizedTime}: clip {clip.length:0.00}s, hand {(hand != null)} {before} -> {(hand != null ? hand.position : Vector3.zero)}");
                render();
            }
            finally
            {
                graph.Destroy();
            }
        }

        private static void AddLight(Scene scene)
        {
            var go = new GameObject("Sun");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.88f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
        }

        /// <summary>Orthographic shot around the character; yaw 0 looks at its face (it faces +Z).</summary>
        private static void Shot(Scene scene, string name, float yaw, float orthoSize)
        {
            var go = new GameObject("Capture Camera");
            SceneManager.MoveGameObjectToScene(go, scene);
            Vector3 target = new Vector3(0f, 1.0f, 0f);
            Quaternion rotation = Quaternion.Euler(4f, 180f + yaw, 0f);
            go.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 8f, rotation);

            var camera = go.AddComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = orthoSize * 0.55f; // half the frame height, with some headroom
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.86f, 0.84f, 0.8f);

            var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), texture.EncodeToPNG());

            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(go);
        }
    }
}
