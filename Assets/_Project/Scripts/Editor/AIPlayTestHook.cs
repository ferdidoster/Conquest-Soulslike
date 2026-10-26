using System.Collections.Generic;
using System.IO;
using System.Text;
using Conquest.AI;
using Conquest.Environment;
using Conquest.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Play-mode check of enemy sight and pathfinding in Lab_Demo. Runs in the background when
    /// ConquestAIPlay.pending exists in the project root. Each line is a step, in level space:
    ///   "player x y z seconds"   puts the player there and watches for that long
    ///   "open Gate_Id"           opens a gate
    /// Writes Captures/AI/ai_report.txt (every enemy's position, aggro and sight twice a second) and
    /// Captures/AI/navmesh.png (the baked NavMesh from above, with the enemies' tracks).
    /// </summary>
    [InitializeOnLoad]
    internal static class AIPlayTestHook
    {
        private const string Marker = "ConquestAIPlay.pending";
        private const string Folder = "Captures/AI";

        private static double _nextPoll;
        private static bool _running;
        private static readonly List<string[]> Steps = new List<string[]>();
        private static int _index;
        private static float _stepEnd;
        private static float _nextLog;
        private static PlayerInputReader _player;
        private static Transform _level;
        private static readonly List<MonoBehaviour> Brains = new List<MonoBehaviour>();
        private static readonly Dictionary<MonoBehaviour, List<Vector3>> Tracks = new Dictionary<MonoBehaviour, List<Vector3>>();
        private static readonly StringBuilder Log = new StringBuilder();

        static AIPlayTestHook()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void Poll()
        {
            if (_running)
            {
                if (EditorApplication.isPlaying) Step();
                return;
            }

            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1.5;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Marker) ||
                File.Exists("ConquestRefresh.pending"))
                return;

            Steps.Clear();
            foreach (string raw in File.ReadAllLines(Marker))
            {
                string line = raw.Trim();
                if (line.Length > 0 && !line.StartsWith("#")) Steps.Add(line.Split(' '));
            }

            File.Delete(Marker);
            if (Steps.Count == 0) return;
            if (EditorSceneManager.GetActiveScene().path != LabKitBuilder.ScenePath)
                EditorSceneManager.OpenScene(LabKitBuilder.ScenePath, OpenSceneMode.Single);
            Log.Clear();
            _running = true;
            _index = -1;
            // without this the game stops updating while the editor is in the background
            Application.runInBackground = true;
            EditorApplication.EnterPlaymode();
        }

        private static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!_running) return;
            if (change == PlayModeStateChange.EnteredPlayMode) Begin();
            if (change == PlayModeStateChange.ExitingPlayMode) DrawNavMesh();
            if (change == PlayModeStateChange.EnteredEditMode) Finish();
        }

        private static void Begin()
        {
            Application.runInBackground = true;
            _player = Object.FindAnyObjectByType<PlayerInputReader>();
            GameObject levelGo = GameObject.Find(LabKitBuilder.LevelName);
            _level = levelGo != null ? levelGo.transform : null;
            Brains.Clear();
            Tracks.Clear();
            foreach (MonoBehaviour b in Object.FindObjectsByType<MonoBehaviour>())
                if (b is MeleeAIBrain || b is TurretAIBrain)
                {
                    Brains.Add(b);
                    Tracks[b] = new List<Vector3>();
                }

            if (_player == null || _level == null)
            {
                Log.AppendLine("No player or no Level_LabDemo in the scene.");
                EditorApplication.ExitPlaymode();
                return;
            }

            Log.AppendLine($"{Brains.Count} enemies:");
            foreach (MonoBehaviour b in Brains)
                Log.AppendLine($"  {b.name} ({b.GetType().Name}) at {Local(b.transform.position)}");
            Next();
        }

        private static void Next()
        {
            while (true)
            {
                _index++;
                if (_index >= Steps.Count)
                {
                    EditorApplication.ExitPlaymode();
                    return;
                }

                string[] p = Steps[_index];
                if (p[0] == "open" && p.Length == 2)
                {
                    bool ok = Gate.Open(p[1]);
                    Log.AppendLine($"t={Time.time:0.0} open {p[1]}: {(ok ? "ok" : "not found")}");
                    continue;
                }

                if (p[0] == "path" && p.Length == 7)
                {
                    Vector3 from = _level.TransformPoint(new Vector3(F(p[1]), F(p[2]), F(p[3])));
                    Vector3 to = _level.TransformPoint(new Vector3(F(p[4]), F(p[5]), F(p[6])));
                    var path = new NavMeshPath();
                    bool a = NavMesh.SamplePosition(from, out NavMeshHit ha, 1.5f, NavMesh.AllAreas);
                    bool b = NavMesh.SamplePosition(to, out NavMeshHit hb, 1.5f, NavMesh.AllAreas);
                    if (a && b) NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path);
                    float length = 0f;
                    for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                    Log.AppendLine($"t={Time.time:0.0} path {Local(from)} -> {Local(to)}: on mesh {a}/{b}, {path.status}, " +
                                   $"{path.corners.Length} corners, {length:0.0} m (straight {Vector3.Distance(from, to):0.0} m)");
                    continue;
                }

                if (p[0] == "player" && p.Length == 5)
                {
                    var controller = _player.GetComponent<CharacterController>();
                    if (controller != null) controller.enabled = false;
                    _player.transform.position = _level.TransformPoint(new Vector3(F(p[1]), F(p[2]), F(p[3])));
                    if (controller != null) controller.enabled = true;
                    _stepEnd = Time.time + F(p[4]);
                    _nextLog = Time.time + 0.25f;
                    Log.AppendLine($"t={Time.time:0.0} player to {Local(_player.transform.position)} for {p[4]} s" +
                                   $" (NavMesh ready: {LevelNavMesh.IsReady})");
                    return;
                }

                Log.AppendLine($"? {string.Join(" ", p)}");
            }
        }

        private static void Step()
        {
            if (_index < 0 || _index >= Steps.Count) return;
            if (Time.time >= _nextLog)
            {
                _nextLog = Time.time + 0.5f;
                Log.AppendLine($"  t={Time.time:0.0} player {Local(_player.transform.position)}");
                foreach (MonoBehaviour b in Brains)
                {
                    if (b == null) continue;
                    Tracks[b].Add(b.transform.position);
                    string state = b is MeleeAIBrain m
                        ? $"aggro={m.IsAggroed} sees={m.SeesTarget} move={m.MoveDirection.magnitude:0.0}"
                        : $"target={((TurretAIBrain)b).HasTarget}";
                    Log.AppendLine($"    {b.name,-24} {Local(b.transform.position)} {state}");
                }
            }

            if (Time.time >= _stepEnd) Next();
        }

        private static string Local(Vector3 world)
        {
            Vector3 p = _level != null ? _level.InverseTransformPoint(world) : world;
            return $"({p.x:0.0}, {p.y:0.0}, {p.z:0.0})";
        }

        /// <summary>Top-down picture of the NavMesh (grey), enemy tracks (red, start dot) and the player spots (cyan).</summary>
        private static void DrawNavMesh()
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            Log.AppendLine($"NavMesh: {tri.vertices.Length} vertices, {tri.indices.Length / 3} triangles.");
            if (tri.vertices.Length == 0) return;

            var bounds = new Bounds(tri.vertices[0], Vector3.zero);
            foreach (Vector3 v in tri.vertices) bounds.Encapsulate(v);
            bounds.Expand(4f);
            const int size = 1400;
            float scale = size / Mathf.Max(bounds.size.x, bounds.size.z);
            int w = Mathf.CeilToInt(bounds.size.x * scale), h = Mathf.CeilToInt(bounds.size.z * scale);
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(20, 22, 28, 255);

            Vector2 P(Vector3 v) => new Vector2((v.x - bounds.min.x) * scale, (v.z - bounds.min.z) * scale);
            void Dot(Vector2 c, int r, Color32 col)
            {
                for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    int px = (int)c.x + x, py = (int)c.y + y;
                    if (px >= 0 && py >= 0 && px < w && py < h && x * x + y * y <= r * r) pixels[py * w + px] = col;
                }
            }

            for (int t = 0; t < tri.indices.Length; t += 3)
            {
                Vector2 a = P(tri.vertices[tri.indices[t]]), b = P(tri.vertices[tri.indices[t + 1]]),
                    c = P(tri.vertices[tri.indices[t + 2]]);
                int minX = Mathf.Max(0, (int)Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
                int maxX = Mathf.Min(w - 1, (int)Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 1);
                int minY = Mathf.Max(0, (int)Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
                int maxY = Mathf.Min(h - 1, (int)Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + 1);
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var q = new Vector2(x + 0.5f, y + 0.5f);
                    float d1 = Cross(a, b, q), d2 = Cross(b, c, q), d3 = Cross(c, a, q);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) pixels[y * w + x] = new Color32(120, 128, 140, 255);
                }
            }

            foreach (Gate gate in Object.FindObjectsByType<Gate>())
                Dot(P(gate.transform.position), 5, gate.IsOpen ? new Color32(60, 220, 120, 255) : new Color32(255, 200, 40, 255));
            foreach (KeyValuePair<MonoBehaviour, List<Vector3>> track in Tracks)
            {
                if (track.Value.Count == 0) continue;
                Dot(P(track.Value[0]), 6, new Color32(255, 60, 60, 255));
                foreach (Vector3 v in track.Value) Dot(P(v), 2, new Color32(255, 110, 90, 255));
            }

            foreach (string[] p in Steps)
                if (p[0] == "player" && p.Length == 5)
                    Dot(P(_level.TransformPoint(new Vector3(F(p[1]), F(p[2]), F(p[3])))), 6, new Color32(60, 220, 255, 255));

            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(Path.Combine(Folder, "navmesh.png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Log.AppendLine($"navmesh.png: x {bounds.min.x:0}..{bounds.max.x:0}, z {bounds.min.z:0}..{bounds.max.z:0} (world), north up.");
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        private static void Finish()
        {
            _running = false;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "ai_report.txt"), Log.ToString());
            Debug.Log("Conquest: AI play test done.");
        }
    }
}
