using System.Collections.Generic;
using System.IO;
using System.Text;
using Conquest.Environment;
using Conquest.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Play-mode check of the lab's section view: opens Lab_Demo, enters play mode, puts the player on
    /// each listed spot (and turns the isometric camera), waits for the view to settle and saves what
    /// the game camera shows to Captures/Lab/play_*.png. Captures/Lab/play_report.txt lists, per spot,
    /// every piece between the camera and the player and whether it was cut away.
    /// Runs in the background when ConquestLabPlay.pending exists in the project root; each line in it
    /// is "name x y z yaw" in level space (Unity), e.g. "hub_w -6 0 0 45".
    /// </summary>
    [InitializeOnLoad]
    internal static class LabPlayTestHook
    {
        private const string Marker = "ConquestLabPlay.pending";
        private const string Folder = "Captures/Lab";
        private const float Settle = 1.2f;

        private static double _nextPoll;
        private static bool _running;
        // name x y z yaw [seconds to wait before the shot, default Settle]
        private static readonly List<(string name, Vector3 pos, float yaw, float wait)> Spots =
            new List<(string, Vector3, float, float)>();
        private static int _index;
        private static double _spotStart;
        private static PlayerInputReader _player;
        private static Transform _level;
        private static LevelSections _sections;
        private static readonly StringBuilder Log = new StringBuilder();

        static LabPlayTestHook()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void Poll()
        {
            if (_running)
            {
                if (EditorApplication.isPlaying)
                {
                    // an exception elsewhere (Error Pause) must not stall the test
                    if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                    Step();
                }

                return;
            }

            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1.5;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Marker) ||
                File.Exists("ConquestRefresh.pending"))
                return;

            string[] lines = File.ReadAllLines(Marker);
            File.Delete(Marker);
            Spots.Clear();
            foreach (string raw in lines)
            {
                string[] p = raw.Trim().Split(' ');
                if (p.Length != 5 && p.Length != 6) continue;
                Spots.Add((p[0], new Vector3(F(p[1]), F(p[2]), F(p[3])), F(p[4]), p.Length == 6 ? F(p[5]) : Settle));
            }

            if (Spots.Count == 0) return;
            if (EditorSceneManager.GetActiveScene().path != LabKitBuilder.ScenePath)
                EditorSceneManager.OpenScene(LabKitBuilder.ScenePath, OpenSceneMode.Single);
            Log.Clear();
            _running = true;
            _index = -1;
            EditorApplication.EnterPlaymode();
        }

        private static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!_running) return;
            if (change == PlayModeStateChange.EnteredPlayMode) Begin();
            if (change == PlayModeStateChange.EnteredEditMode) Finish();
        }

        private static void Begin()
        {
            // keep the game running while the editor is in the background
            Application.runInBackground = true;
            _player = Object.FindAnyObjectByType<PlayerInputReader>();
            GameObject levelGo = GameObject.Find(LabKitBuilder.LevelName);
            _level = levelGo != null ? levelGo.transform : null;
            _sections = levelGo != null ? levelGo.GetComponent<LevelSections>() : null;
            if (_player == null || _level == null)
            {
                Log.AppendLine("No player or no Level_LabDemo in the scene.");
                EditorApplication.ExitPlaymode();
                return;
            }

            // only the view is under test: keep the enemies from fighting
            foreach (CharacterController other in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude))
                if (!other.transform.IsChildOf(_player.transform.root)) other.gameObject.SetActive(false);
            foreach (MonoBehaviour b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
                if (b.GetType().Namespace != null && b.GetType().Namespace.StartsWith("Conquest.AI") &&
                    !b.transform.IsChildOf(_player.transform.root))
                    b.gameObject.SetActive(false);
            Next();
        }

        private static void Next()
        {
            _index++;
            if (_index >= Spots.Count)
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            var (_, pos, yaw, _) = Spots[_index];
            var controller = _player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            _player.transform.position = _level.TransformPoint(pos);
            if (controller != null) controller.enabled = true;
            SetCameraYaw(yaw);
            _spotStart = EditorApplication.timeSinceStartup;
        }

        private static void Step()
        {
            if (_index < 0 || _index >= Spots.Count || EditorApplication.timeSinceStartup - _spotStart < Spots[_index].wait) return;
            Capture(Spots[_index].name);
            Next();
        }

        /// <summary>Turns the isometric rig (Cinemachine assembly, reached by name).</summary>
        private static void SetCameraYaw(float yaw)
        {
            foreach (MonoBehaviour b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (b.GetType().Name != "IsometricCameraRig") continue;
                b.GetType().GetMethod("SetYaw")?.Invoke(b, new object[] { yaw });
            }
        }

        private static void Capture(string name)
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 eye = camera.transform.position;
            Vector3 to = _player.transform.position + Vector3.up * 1.1f;
            Log.AppendLine($"[{name}] section {(_sections != null ? _sections.Current : "-")}" +
                           $" (light {(_sections != null ? _sections.TransitionProgress : 1f):0.00}), player {_level.InverseTransformPoint(_player.transform.position)}, camera yaw {camera.transform.eulerAngles.y:0}");
            var seen = new HashSet<Transform>();
            foreach (RaycastHit hit in Physics.SphereCastAll(eye, 0.4f, (to - eye).normalized, (to - eye).magnitude - 0.6f,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                Transform piece = PieceOf(hit.collider.transform);
                if (piece == null || !seen.Add(piece)) continue;
                Renderer r = piece.GetComponentInChildren<Renderer>();
                bool cut = r != null && (r.renderingLayerMask & LevelSections.CutLayer) != 0;
                bool tall = r != null && r.bounds.max.y - _level.position.y > 1.6f;
                Log.AppendLine($"    {(cut ? "cut   " : tall ? "BLOCKS" : "low   ")} {piece.name} at {_level.InverseTransformPoint(piece.position)}" +
                               $" fade={piece.GetComponent<FadeOccluder>() != null}");
            }

            if (_sections != null) Log.Append(_sections.Describe(_player.transform.position, 14f));

            const int width = 1600, height = 900;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { antiAliasing = 4 };
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = active;
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(Path.Combine(Folder, $"play_{name}.png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            target.Release();
            Object.DestroyImmediate(target);
        }

        /// <summary>The kit piece (direct child of a level group) a collider belongs to.</summary>
        private static Transform PieceOf(Transform t)
        {
            GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
            return root != null && root.transform != _level ? root.transform : null;
        }

        private static void Finish()
        {
            _running = false;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "play_report.txt"), Log.ToString());
            Debug.Log($"Conquest: lab play test done ({Spots.Count} spots).");
        }
    }
}
