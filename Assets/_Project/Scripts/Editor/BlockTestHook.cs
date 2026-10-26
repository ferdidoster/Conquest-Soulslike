using System;
using System.IO;
using System.Text;
using Conquest.Character;
using Conquest.Combat;
using Conquest.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Play test for the guard and its energy shield in the open scene: enters play mode with the
    /// other characters switched off, holds block on a virtual mouse, lands a blocked hit, drains
    /// stamina and breaks the guard, rendering a close-up at each step into Captures/Block and a log
    /// to Captures/Block/block_test.txt. Runs from Conquest > Test Block, or in the background when
    /// ConquestBlockTest.pending exists in the project root.
    /// </summary>
    [InitializeOnLoad]
    internal static class BlockTestHook
    {
        private const string Marker = "ConquestBlockTest.pending";
        private const string Folder = "Captures/Block";
        private const int Size = 900;

        private sealed class Step
        {
            public string Name;
            public float Duration;
            public bool Block;
            public Action Start;
            public float StartAt;
            public bool Started;
            public float CaptureAt = -1f;
        }

        private static Step[] _steps;
        private static double _nextPoll;
        private static bool _running;
        private static int _step;
        private static float _stepStart;
        private static bool _captured;
        private static PlayTestInput _input;
        private static Mouse _mouse;
        private static ActionCharacter _player;
        private static Camera _camera;
        private static readonly StringBuilder Log = new StringBuilder();

        static BlockTestHook()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void Poll()
        {
            if (_running)
            {
                if (EditorApplication.isPlaying) Tick();
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

        [MenuItem("Conquest/Test Block", priority = 23)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Log.Clear();
            _input = new PlayTestInput();
            _running = true;
            _step = -1;
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
            var reader = Object.FindAnyObjectByType<PlayerInputReader>();
            _player = reader != null ? reader.GetComponent<ActionCharacter>() : null;
            if (_player == null)
            {
                Log.AppendLine("No player (PlayerInputReader + ActionCharacter) in the open scene.");
                EditorApplication.ExitPlaymode();
                return;
            }

            foreach (ActionCharacter other in Object.FindObjectsByType<ActionCharacter>(FindObjectsSortMode.None))
                if (other != _player) other.gameObject.SetActive(false);

            _mouse = InputSystem.AddDevice<Mouse>("BlockTestMouse");
            _camera = new GameObject("Block Test Camera").AddComponent<Camera>();
            _camera.fieldOfView = 38f;
            _camera.enabled = false;

            _steps = new[]
            {
                new Step { Name = "idle", Duration = 0.8f, CaptureAt = 0.6f },
                new Step { Name = "raised", Duration = 0.8f, Block = true, CaptureAt = 0.6f },
                new Step { Name = "blocked_hit", Duration = 0.6f, Block = true, CaptureAt = 0.07f, Start = () => Hit(15f) },
                new Step { Name = "low_stamina", Duration = 0.8f, Block = true, CaptureAt = 0.5f, Start = DrainStamina },
                new Step { Name = "guard_break", Duration = 1.3f, Block = true, StartAt = 0.15f, CaptureAt = 0.25f, Start = () => Hit(500f) },
                new Step { Name = "lowered", Duration = 0.6f, CaptureAt = 0.5f },
            };
            NextStep();
        }

        private static void Hit(float guardDamage)
        {
            Transform t = _player.transform;
            var hit = new DamageInfo(5f, guardDamage, null, t.position + t.forward * 0.6f + Vector3.up * 1.3f,
                -t.forward, canBeParried: false, weight: 1.5f);
            HitResult result = _player.Receiver.ReceiveHit(hit);
            Log.AppendLine($"  hit with guard damage {guardDamage} (guarding {_player.Receiver.IsGuarding}, " +
                           $"state {_player.CurrentState?.GetType().Name}): {result}");
        }

        private static void DrainStamina()
        {
            _player.Stamina.Spend(_player.Stamina.Pool.Current - 12f);
            Log.AppendLine($"  stamina drained to {_player.Stamina.Pool.Current:0}");
        }

        private static void NextStep()
        {
            _step++;
            if (_step >= _steps.Length)
            {
                EditorApplication.ExitPlaymode();
                return;
            }

            Step step = _steps[_step];
            _stepStart = Time.time;
            _captured = false;
            InputSystem.QueueStateEvent(_mouse, step.Block ? new MouseState().WithButton(MouseButton.Right) : new MouseState());
        }

        private static void Tick()
        {
            if (_player == null || _steps == null || _step < 0 || _step >= _steps.Length) return;

            Step step = _steps[_step];
            float t = Time.time - _stepStart;
            if (!step.Started && t >= step.StartAt)
            {
                step.Started = true;
                step.Start?.Invoke();
            }

            if (!_captured && step.CaptureAt >= 0f && t >= step.CaptureAt)
            {
                _captured = true;
                Transform shell = _player.transform.Find("Energy Shield");
                var shellRenderer = shell != null ? shell.GetComponent<Renderer>() : null;
                Log.AppendLine($"{_step}_{step.Name}: guarding {_player.Receiver.IsGuarding}, " +
                               $"state {_player.CurrentState?.GetType().Name}, stamina {_player.Stamina.Pool.Current:0}, " +
                               $"shield {(shellRenderer == null ? "missing" : shellRenderer.enabled ? "visible" : "hidden")}");
                Capture($"{_step}_{step.Name}");
            }

            if (t >= step.Duration) NextStep();
        }

        private static void Capture(string name)
        {
            Transform t = _player.transform;
            Vector3 focus = t.position + Vector3.up * 1.1f;
            _camera.transform.position = focus + t.forward * 3.6f - t.right * 2.0f + Vector3.up * 0.7f;
            _camera.transform.LookAt(focus);

            var target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            _camera.targetTexture = target;
            _camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            _camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);

            Directory.CreateDirectory(Folder);
            File.WriteAllBytes($"{Folder}/{name}.png", image.EncodeToPNG());
            Object.Destroy(image);
        }

        private static void Finish()
        {
            _running = false;
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            _mouse = null;
            _input?.Restore();

            Directory.CreateDirectory(Folder);
            File.WriteAllText($"{Folder}/block_test.txt", Log.ToString());
            Debug.Log($"Conquest: block test written to {Folder}\n{Log}");
        }
    }
}
