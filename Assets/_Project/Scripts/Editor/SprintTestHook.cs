using System.Collections.Generic;
using System.IO;
using System.Text;
using Conquest.Animation;
using Conquest.Character;
using Conquest.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Smoke test for player movement in the open scene: enters play mode, presses keys on a virtual
    /// keyboard (W, then W + held Space, then W + Shift, then a running jump with E) and logs the knight's speed, sprint input,
    /// animator speed and stamina per phase to Captures/sprint_test.txt, then leaves play mode.
    /// Runs from Conquest > Test Sprint, or in the background when ConquestSprintTest.pending exists
    /// in the project root.
    /// </summary>
    [InitializeOnLoad]
    internal static class SprintTestHook
    {
        private const string Marker = "ConquestSprintTest.pending";
        private const string Report = "Captures/sprint_test.txt";

        private static readonly (string name, float duration, Key[] keys)[] Phases =
        {
            ("settle", 1.0f, new Key[0]),
            ("run (W)", 1.5f, new[] { Key.W }),
            ("hold Space (W + Space)", 1.5f, new[] { Key.W, Key.Space }),
            ("release", 2.0f, new Key[0]),
            ("hold Shift (W + Shift)", 1.5f, new[] { Key.W, Key.LeftShift }),
            ("stop", 0.5f, new Key[0]),
            ("jump (W + E)", 1.2f, new[] { Key.W, Key.E }),
            ("land", 0.8f, new Key[0]),
        };

        private static double _nextPoll;
        private static bool _running;
        private static int _phase;
        private static float _phaseStart;
        private static float _startHeight;
        private static float _peakHeight;
        private static string _states;
        private static Keyboard _keyboard;
        private static ActionCharacter _player;
        private static PlayerInputReader _reader;
        private static readonly List<string> Samples = new List<string>();
        private static readonly StringBuilder Log = new StringBuilder();

        private static PlayTestInput _input;

        static SprintTestHook()
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
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Marker))
                return;

            File.Delete(Marker);
            Run();
        }

        [MenuItem("Conquest/Test Sprint", priority = 22)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Log.Clear();
            Samples.Clear();
            _input = new PlayTestInput();

            _running = true;
            _phase = -1;
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!_running) return;
            if (change == PlayModeStateChange.EnteredPlayMode) Begin();
            if (change == PlayModeStateChange.EnteredEditMode) Finish();
        }

        private static void Begin()
        {
            _reader = Object.FindAnyObjectByType<PlayerInputReader>();
            _player = _reader != null ? _reader.GetComponent<ActionCharacter>() : null;
            if (_player == null)
            {
                Log.AppendLine("No player (PlayerInputReader + ActionCharacter) in the open scene.");
                EditorApplication.ExitPlaymode();
                return;
            }

            // Only movement is under test; keep enemies from attacking the player mid-run.
            foreach (ActionCharacter other in Object.FindObjectsByType<ActionCharacter>(FindObjectsSortMode.None))
                if (other != _player) other.gameObject.SetActive(false);

            _keyboard = InputSystem.AddDevice<Keyboard>("SprintTestKeyboard");
            Log.AppendLine($"scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, " +
                           $"run {_player.Locomotion.runSpeed} m/s, sprint {_player.Locomotion.sprintSpeed} m/s");
            NextPhase();
        }

        private static void NextPhase()
        {
            _phase++;
            if (_phase >= Phases.Length)
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            _phaseStart = Time.time;
            _startHeight = _peakHeight = _player.transform.position.y;
            _states = "";
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Phases[_phase].keys));
            Samples.Clear();
        }

        private static void Step()
        {
            if (_player == null || _phase < 0 || _phase >= Phases.Length) return;

            float t = Time.time - _phaseStart;
            (string name, float duration, Key[] _) = Phases[_phase];
            _peakHeight = Mathf.Max(_peakHeight, _player.transform.position.y);
            string state = _player.CurrentState?.GetType().Name;
            if (!_states.EndsWith(state ?? "")) _states += (_states.Length > 0 ? " > " : "") + state;
            if (t >= duration * 0.5f)
            {
                Animator animator = _player.GetComponentInChildren<Animator>();
                float animSpeed = animator != null ? animator.GetFloat(AnimatorParameters.Speed) : -1f;
                Samples.Add($"{_player.Motor.PlanarVelocity.magnitude:0.00};{_reader.IsSprintHeld};{animSpeed:0.00};" +
                            $"{_player.Stamina.Pool.Current:0};{_player.CurrentState?.GetType().Name}");
            }

            if (t < duration) return;
            Log.AppendLine($"{name}: {Summarize()} | peak {_peakHeight - _startHeight:0.00} m above start | states {_states}");
            NextPhase();
        }

        /// <summary>The second half of a phase: average speed, and the first and last sample.</summary>
        private static string Summarize()
        {
            if (Samples.Count == 0) return "no samples";
            float total = 0f;
            foreach (string s in Samples) total += float.Parse(s.Split(';')[0]);
            return $"avg speed {total / Samples.Count:0.00} m/s over {Samples.Count} frames | " +
                   $"first [{Samples[0]}] last [{Samples[Samples.Count - 1]}] (speed;sprintHeld;animSpeed;stamina;state)";
        }

        private static void Finish()
        {
            _running = false;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            _input?.Restore();

            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, Log.ToString());
            Debug.Log($"Conquest: sprint test written to {Report}\n{Log}");
        }
    }
}
