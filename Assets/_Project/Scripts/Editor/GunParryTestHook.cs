using System;
using System.IO;
using System.Text;
using Conquest.AI;
using Conquest.Character;
using Conquest.Character.States;
using Conquest.Combat;
using Conquest.Core.Input;
using Conquest.Player;
using Conquest.Stats;
using Conquest.Targeting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Play test for the gun parry in the open scene: stands the enemy 3.2 m in front of the player
    /// under scripted input, fires once while it idles (should only chip) and once as it swings
    /// (should parry and stagger it), pressing Left Ctrl on a virtual keyboard. Logs each shot and
    /// renders the moments into Captures/GunParry. Runs from Conquest > Test Gun Parry, or in the
    /// background when ConquestGunParryTest.pending exists in the project root.
    /// </summary>
    [InitializeOnLoad]
    internal static class GunParryTestHook
    {
        private const string Marker = "ConquestGunParryTest.pending";
        private const string Folder = "Captures/GunParry";
        private const int Width = 1200;
        private const int Height = 700;
        private const float Distance = 3.2f;

        /// <summary>Drives the enemy: stands still and attacks on request.</summary>
        private sealed class ScriptedInput : ICharacterInput
        {
            public Vector3 MoveDirection => Vector3.zero;
            public bool IsBlockHeld => false;
            public bool IsSprintHeld => false;
            public bool IsHeld(CharacterAction action) => false;
            public event Action<CharacterAction> ActionRequested;
            public void Request(CharacterAction action) => ActionRequested?.Invoke(action);
        }

        private sealed class FixedTarget : ITargetProvider
        {
            public ITargetable CurrentTarget { get; set; }
            public bool HasTarget => CurrentTarget != null;
        }

        private enum Phase { Settle, EarlyShot, EarlyWait, Swing, TimedShot, StaggerWait, Done }

        private static double _nextPoll;
        private static bool _running;
        private static Phase _phase;
        private static float _phaseStart;
        private static bool _ctrlDown;
        private static bool _logged;
        private static int _captureNext = -1;
        private static string _captureName;
        private static PlayTestInput _input;
        private static Keyboard _keyboard;
        private static ActionCharacter _player;
        private static ActionCharacter _enemy;
        private static ScriptedInput _enemyInput;
        private static Camera _camera;
        private static readonly StringBuilder Log = new StringBuilder();

        static GunParryTestHook()
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

        [MenuItem("Conquest/Test Gun Parry", priority = 24)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Log.Clear();
            _logged = false;
            _input = new PlayTestInput();
            _running = true;
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
            var brain = Object.FindAnyObjectByType<MeleeAIBrain>();
            _enemy = brain != null ? brain.GetComponent<ActionCharacter>() : null;
            if (_player == null || _enemy == null || _player.Gun == null)
            {
                Log.AppendLine($"Need a player with a ParryGun and an enemy (player {_player != null}, " +
                               $"gun {_player != null && _player.Gun != null}, enemy {_enemy != null}).");
                EditorApplication.ExitPlaymode();
                return;
            }

            foreach (ActionCharacter other in Object.FindObjectsByType<ActionCharacter>(FindObjectsSortMode.None))
                if (other != _player && other != _enemy) other.gameObject.SetActive(false);

            // The enemy stands still in front of the player and attacks only when told to.
            brain.enabled = false;
            _enemyInput = new ScriptedInput();
            _enemy.SetInput(_enemyInput);
            _enemy.SetTargetProvider(new FixedTarget { CurrentTarget = _player.GetComponent<Targetable>() });
            _player.SetTargetProvider(new FixedTarget { CurrentTarget = _enemy.GetComponent<Targetable>() });

            Transform p = _player.transform;
            Vector3 forward = p.forward;
            forward.y = 0f;
            forward.Normalize();
            _enemy.Motor.SetCollisionEnabled(false);
            _enemy.transform.position = p.position + forward * Distance;
            _enemy.Motor.SetCollisionEnabled(true);
            _enemy.Motor.FaceImmediately(-forward);

            _player.Gun.Fired += OnFired;
            _keyboard = InputSystem.AddDevice<Keyboard>("GunParryTestKeyboard");
            _camera = new GameObject("Gun Parry Test Camera").AddComponent<Camera>();
            _camera.fieldOfView = 34f;
            _camera.enabled = false;

            Log.AppendLine($"enemy {Distance} m ahead; shot fires {_player.Parry.shotAt * _player.Parry.shotDuration:0.000} s after the press");
            Enter(Phase.Settle);
        }

        private static void Enter(Phase phase)
        {
            _phase = phase;
            _phaseStart = Time.time;
            if (phase == Phase.Done) EditorApplication.ExitPlaymode();
        }

        private static void PressCtrl()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.LeftCtrl));
            _ctrlDown = true;
        }

        private static void Tick()
        {
            if (_player == null || _enemy == null) return;
            if (_ctrlDown)
            {
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                _ctrlDown = false;
            }

            if (_captureNext >= 0 && Time.frameCount >= _captureNext)
            {
                _captureNext = -1;
                Capture(_captureName);
            }

            float t = Time.time - _phaseStart;
            switch (_phase)
            {
                case Phase.Settle:
                    if (t >= 1.2f) Enter(Phase.EarlyShot);
                    break;
                case Phase.EarlyShot:
                    Log.AppendLine($"early shot: enemy idle in {StateName(_enemy)}, health {_enemy.GetComponent<Health>().Pool.Current:0}");
                    _captureName = "1_early_shot";
                    PressCtrl();
                    Enter(Phase.EarlyWait);
                    break;
                case Phase.EarlyWait:
                    if (!_logged && t >= 0.4f)
                    {
                        _logged = true;
                        Log.AppendLine($"  0.4 s later: enemy {StateName(_enemy)}, health {_enemy.GetComponent<Health>().Pool.Current:0}");

                    }
                    if (t >= 1.6f) Enter(Phase.Swing);
                    break;
                case Phase.Swing:
                    if (t < 0.02f) break;
                    if (!(_enemy.CurrentState is AttackState))
                    {
                        if (t > 0.05f && t < 0.1f) Log.AppendLine("timed shot: enemy attacks");
                        _enemyInput.Request(CharacterAction.LightAttack);
                        if (t > 1f)
                        {
                            Log.AppendLine($"  enemy never attacked (state {StateName(_enemy)})");
                            Enter(Phase.Done);
                        }
                        break;
                    }
                    if (!_enemy.IsParryableNow) break;
                    Log.AppendLine($"  enemy parryable after {t:0.000} s; pressing Ctrl");
                    _captureName = "2_parry_shot";
                    PressCtrl();
                    Enter(Phase.TimedShot);
                    break;
                case Phase.TimedShot:
                    if (t >= 0.6f)
                    {
                        Log.AppendLine($"  0.6 s after the press: enemy {StateName(_enemy)}, player {StateName(_player)}");
                        _captureName = "3_staggered";
                        _captureNext = Time.frameCount;
                        Enter(Phase.StaggerWait);
                    }
                    break;
                case Phase.StaggerWait:
                    if (t < 1.5f) break;
                    // The game camera eases in from afar at the start, so check the bar at the end.
                    LogHealthBar();
                    CaptureGameView("4_game_view");
                    Enter(Phase.Done);
                    break;
            }
        }

        private static void OnFired(ParryGun.Shot shot)
        {
            Log.AppendLine($"  shot: {shot.Result} (target {(shot.Target != null ? shot.Target.name : "none")}, " +
                           $"{Vector3.Distance(shot.Muzzle, shot.End):0.0} m), enemy state {StateName(_enemy)}, " +
                           $"health {_enemy.GetComponent<Health>().Pool.Current:0}");
            _captureNext = Time.frameCount + 1;
        }

        private static void LogHealthBar()
        {
            Transform bar = _enemy.transform.Find("Health Bar");
            if (bar == null)
            {
                Log.AppendLine("  health bar: missing");
                return;
            }
            var group = bar.GetComponent<CanvasGroup>();
            var canvas = bar.GetComponent<Canvas>();
            Camera main = Camera.main;
            string view = main == null ? "none" :
                $"{main.name} ortho {main.orthographic} fov {main.fieldOfView:0} size {main.orthographicSize:0.0} " +
                $"dist {Vector3.Distance(main.transform.position, bar.position):0.0} mask {main.cullingMask}";
            Log.AppendLine($"  health bar: active {bar.gameObject.activeInHierarchy}, canvas {canvas.enabled} {canvas.renderMode}, " +
                           $"alpha {group.alpha:0.00} (frame {Time.frameCount}, time {Time.time:0.00}, unscaled {Time.unscaledTime:0.00}, " +
                           $"dt {Time.deltaTime:0.000}, unscaled dt {Time.unscaledDeltaTime:0.000}), scale {bar.localScale.x:0.0000}, at {bar.position}, camera {view}");
        }

        private static string StateName(ActionCharacter character) => character.CurrentState?.GetType().Name ?? "none";

        /// <summary>What the player sees: the game camera, with the enemy's overhead health bar.</summary>
        private static void CaptureGameView(string name)
        {
            Camera main = Camera.main;
            if (main != null) Render(main, name);
        }

        private static void Capture(string name)
        {
            Vector3 a = _player.transform.position;
            Vector3 b = _enemy.transform.position;
            Vector3 mid = (a + b) * 0.5f + Vector3.up * 1.1f;
            Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized);
            _camera.transform.position = mid + side * 5.2f + Vector3.up * 0.6f - (b - a).normalized * 0.8f;
            _camera.transform.LookAt(mid);
            Render(_camera, name);
        }

        private static void Render(Camera camera, string name)
        {
            var target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = previousTarget;
            RenderTexture.ReleaseTemporary(target);

            Directory.CreateDirectory(Folder);
            File.WriteAllBytes($"{Folder}/{name}.png", image.EncodeToPNG());
            Object.Destroy(image);
        }

        private static void Finish()
        {
            _running = false;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            _input?.Restore();

            Directory.CreateDirectory(Folder);
            File.WriteAllText($"{Folder}/gun_parry_test.txt", Log.ToString());
            Debug.Log($"Conquest: gun parry test written to {Folder}\n{Log}");
        }
    }
}
