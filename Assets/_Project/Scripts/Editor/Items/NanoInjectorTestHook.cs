using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Conquest.Character;
using Conquest.Character.States;
using Conquest.Combat;
using Conquest.Environment;
using Conquest.Player;
using Conquest.Stats;
using Conquest.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Play test for the nano-injector. Opens a scene (Sandbox, or the path written in the marker file),
    /// sends the enemies away and drives the player with a virtual keyboard and gamepad: a heal on R
    /// while holding W and clicking attack (he must not move or swing), a heal on the gamepad's West
    /// button, a hit before the slam (the charge is kept) and after it (the repair goes on), an empty
    /// injector, resting at the checkpoint and dying and respawning. Logs every check to
    /// Captures/Injector/injector_test.txt and renders the moments into Captures/Injector.
    /// Runs from Conquest > Test Nano-Injector, or in the background when ConquestInjectorTest.pending exists.
    /// </summary>
    [InitializeOnLoad]
    internal static class NanoInjectorTestHook
    {
        private const string Marker = "ConquestInjectorTest.pending";
        private const string Folder = "Captures/Injector";
        private const int Width = 1200;
        private const int Height = 760;

        private static double _nextPoll;
        private static bool _running;
        private static string _returnScene;
        private static IEnumerator _script;
        private static float _waitUntil;
        private static PlayTestInput _input;
        private static Keyboard _keyboard;
        private static Gamepad _gamepad;
        private static Mouse _mouse;
        private static ActionCharacter _player;
        private static Health _health;
        private static NanoInjector _injector;
        private static PlayerRespawn _respawn;
        private static ChargeCounter _counter;
        private static Camera _camera;
        private static int _empties, _draws, _stows;
        private static int _passed, _failed;
        private static readonly StringBuilder Log = new StringBuilder();

        static NanoInjectorTestHook()
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

            string scene = File.ReadAllText(Marker).Trim();
            File.Delete(Marker);
            Run(scene);
        }

        [MenuItem("Conquest/Test Nano-Injector", priority = 25)]
        public static void RunMenu() => Run(null);

        public static void Run(string scenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (string.IsNullOrEmpty(scenePath)) scenePath = ConquestSetupMenu.SandboxScenePath;

            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var open = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (open.isDirty && !string.IsNullOrEmpty(open.path)) EditorSceneManager.SaveScene(open);
            }
            _returnScene = EditorSceneManager.GetActiveScene().path;
            if (_returnScene != scenePath) EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Log.Clear();
            Log.AppendLine($"scene {scenePath}");
            _passed = _failed = 0;
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
            _injector = _player != null ? _player.Injector : null;
            if (_player == null || _injector == null)
            {
                Log.AppendLine($"FAIL setup: player {_player != null}, NanoInjector {_injector != null}");
                _failed++;
                EditorApplication.ExitPlaymode();
                return;
            }

            _health = _player.GetComponent<Health>();
            _respawn = _player.GetComponent<PlayerRespawn>();
            _counter = Object.FindAnyObjectByType<ChargeCounter>();
            _empties = _draws = _stows = 0;
            _injector.Empty += () => _empties++;
            _injector.Drawn += () => _draws++;
            _injector.Stowed += () => _stows++;

            // Only the player is under test; the enemies would interrupt the heals.
            foreach (ActionCharacter other in Object.FindObjectsByType<ActionCharacter>(FindObjectsSortMode.None))
                if (other != _player) other.gameObject.SetActive(false);

            _keyboard = InputSystem.AddDevice<Keyboard>("InjectorTestKeyboard");
            _gamepad = InputSystem.AddDevice<Gamepad>("InjectorTestGamepad");
            _mouse = InputSystem.AddDevice<Mouse>("InjectorTestMouse");
            _camera = new GameObject("Injector Test Camera").AddComponent<Camera>();
            _camera.fieldOfView = 30f;
            _camera.nearClipPlane = 0.05f;
            _camera.enabled = false;

            _script = Script();
            _waitUntil = 0f;
        }

        private static void Tick()
        {
            if (_script == null || Time.time < _waitUntil) return;
            if (!_script.MoveNext())
            {
                _script = null;
                EditorApplication.ExitPlaymode();
                return;
            }
            if (_script.Current is float seconds) _waitUntil = Time.time + seconds;
        }

        // ---------- The test ----------

        private static IEnumerator Script()
        {
            yield return 1.0f;
            Checkpoint start = Checkpoint.At(_player.transform.position);
            Check("a checkpoint at the spawn", start != null, start != null ? start.name : "none");
            Check("charges full at start", _injector.Charges == _injector.MaxCharges, $"{_injector.Charges}/{_injector.MaxCharges}");
            Check("HUD pips match", _counter != null && _counter.LitPips == _injector.Charges && _counter.PipCount == _injector.MaxCharges,
                _counter != null ? $"{_counter.LitPips} lit of {_counter.PipCount}" : "no ChargeCounter");
            CaptureHud("0_hud_full");

            // Step away from the checkpoint so resting does not refill between tests.
            Vector3 away = _player.transform.position + _player.transform.forward * 4f;
            _player.Motor.Teleport(away, _player.transform.rotation);
            yield return 0.5f;

            // 1. Keyboard heal, holding W and clicking attack: committed, no movement, no swing.
            _health.TakeDamage(_health.Pool.Max * 0.7f);
            yield return 0.3f;
            float hp0 = _health.Pool.Current;
            int charges0 = _injector.Charges;
            Vector3 pos0 = _player.transform.position;
            Quaternion rot0 = _player.transform.rotation;
            Keys(Key.R);
            yield return null;
            Keys(Key.W);
            float maxSpeed = 0f;
            bool attacked = false;
            bool clicked = false;
            var states = new List<string>();
            float started = Time.time;
            float injectTime = _injector.InjectAt * _injector.Duration;
            bool shotDraw = false, shotSlam = false, shotRepair = false;
            Vector3 healEnd = pos0;
            float turned = 0f;
            while (Time.time - started < _injector.Duration + 0.15f)
            {
                float t = Time.time - started;
                Note(states);
                if (_player.CurrentState is AttackState) attacked = true;
                if (_player.CurrentState is HealState)
                {
                    maxSpeed = Mathf.Max(maxSpeed, _player.Motor.PlanarVelocity.magnitude);
                    healEnd = _player.transform.position;
                    turned = Mathf.Max(turned, Quaternion.Angle(rot0, _player.transform.rotation));
                }
                else if (t > 0.2f) Keys(); // the heal is over: let go of W
                if (!clicked && t > 0.3f)
                {
                    clicked = true;
                    QueueMouse(true);
                }
                else if (clicked) QueueMouse(false);
                if (!shotDraw && t >= 0.4f) { shotDraw = true; CaptureClose("1_draw"); }
                if (!shotSlam && t >= injectTime + 0.02f) { shotSlam = true; CaptureClose("2_slam"); }
                if (!shotRepair && t >= injectTime + 0.35f) { shotRepair = true; CaptureClose("3_repair"); CaptureHud("3_hud_repair"); }
                yield return null;
            }
            float drift = Vector3.Distance(Flat(pos0), Flat(healEnd));
            Check("R starts the heal", states.Contains(nameof(HealState)), string.Join(" > ", states));
            Check("committed: no movement while W is held", drift < 0.05f && maxSpeed < 0.05f,
                $"drift {drift:0.000} m, max speed {maxSpeed:0.00} m/s");
            Check("committed: no turning", turned < 1f, $"{turned:0.0} deg");
            Check("committed: attack click ignored during the heal", !attacked, attacked ? "swung" : "no swing");
            Keys();
            yield return _injector.HealTime;
            float healed = _health.Pool.Current - hp0;
            float expected = _health.Pool.Max * 0.45f;
            Check("one charge used", _injector.Charges == charges0 - 1, $"{charges0} -> {_injector.Charges}");
            Check("healed 45% of max", Mathf.Abs(healed - expected) < 0.5f, $"{hp0:0} -> {_health.Pool.Current:0} (+{healed:0.0}, expected +{expected:0.0})");
            Check("HUD pips follow", _counter != null && _counter.LitPips == _injector.Charges, _counter != null ? $"{_counter.LitPips} lit" : "-");
            Check("cartridge shown and put away", _draws == 1 && _stows == 1, $"drawn {_draws}, stowed {_stows}");

            // 2. Gamepad West (X / Square).
            yield return 0.5f;
            _health.TakeDamage(30f);
            int charges1 = _injector.Charges;
            InputSystem.QueueStateEvent(_gamepad, new GamepadState(GamepadButton.West));
            yield return null;
            InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            yield return 0.2f;
            bool padHeal = _player.CurrentState is HealState;
            yield return _injector.Duration + 0.2f;
            Check("gamepad West starts the heal", padHeal && _injector.Charges == charges1 - 1,
                $"state {(padHeal ? "HealState" : "other")}, charges {charges1} -> {_injector.Charges}");

            // 3. Hit before the slam: interrupted, charge kept.
            yield return 0.6f;
            int charges2 = _injector.Charges;
            float hp2 = _health.Pool.Current;
            Keys(Key.R);
            yield return null;
            Keys();
            yield return 0.3f;
            bool wasHealing = _player.CurrentState is HealState;
            Hit(10f);
            yield return 0.1f;
            string afterHit = _player.CurrentState?.GetType().Name;
            yield return 1.2f;
            Check("hit before the slam interrupts and keeps the charge",
                wasHealing && afterHit == nameof(ReactionState) && _injector.Charges == charges2,
                $"healing {wasHealing}, then {afterHit}, charges {charges2} -> {_injector.Charges}, hp {hp2:0} -> {_health.Pool.Current:0}");
            Check("cartridge put away on the interruption", _draws == _stows, $"drawn {_draws}, stowed {_stows}");

            // 4. Hit after the slam: the repair goes on.
            yield return 0.5f;
            int charges3 = _injector.Charges;
            Keys(Key.R);
            yield return null;
            Keys();
            yield return injectTime + 0.1f;
            Hit(10f);
            yield return 0.05f;
            float hpAfterHit = _health.Pool.Current;
            bool repairing = _injector.IsRepairing;
            yield return _injector.HealTime + 0.2f;
            Check("hit after the slam: charge spent, repair continues",
                _injector.Charges == charges3 - 1 && repairing && _health.Pool.Current > hpAfterHit + 10f,
                $"charges {charges3} -> {_injector.Charges}, repairing {repairing}, hp {hpAfterHit:0} -> {_health.Pool.Current:0}");

            // 5. Use up the rest, then press with an empty injector.
            yield return 0.6f;
            while (_injector.Charges > 0)
            {
                Keys(Key.R);
                yield return null;
                Keys();
                yield return _injector.Duration + 0.3f;
            }
            int empties = _empties;
            Keys(Key.R);
            yield return null;
            Keys();
            yield return 0.25f;
            Check("empty injector: no heal, just the click", !(_player.CurrentState is HealState) && _empties == empties + 1,
                $"state {_player.CurrentState?.GetType().Name}, empty clicks {_empties - empties}");
            Check("HUD shows no charges", _counter != null && _counter.LitPips == 0, _counter != null ? $"{_counter.LitPips} lit" : "-");
            CaptureHud("4_hud_empty");

            // 6. Rest at the checkpoint: full health and charges.
            _health.TakeDamage(40f);
            if (start != null) _player.Motor.Teleport(start.RespawnPose.position, start.RespawnPose.rotation);
            yield return 0.4f;
            Check("checkpoint refills charges and health",
                _injector.Charges == _injector.MaxCharges && _health.Pool.IsFull,
                $"charges {_injector.Charges}/{_injector.MaxCharges}, hp {_health.Pool.Current:0}/{_health.Pool.Max:0}");
            CaptureClose("5_checkpoint", wide: true);

            // 7. Die away from the checkpoint and respawn at it.
            _player.Motor.Teleport(away, _player.transform.rotation);
            yield return 0.3f;
            _health.TakeDamage(30f);
            Keys(Key.R);
            yield return null;
            Keys();
            yield return _injector.Duration + 0.3f;
            int beforeDeath = _injector.Charges;
            Hit(9999f);
            yield return 0.3f;
            bool dead = _player.IsDead;
            yield return 4.2f;
            Vector3 back = _player.transform.position;
            float fromCheckpoint = start != null ? Vector3.Distance(Flat(back), Flat(start.RespawnPose.position)) : -1f;
            Check("death then respawn at the checkpoint",
                dead && !_player.IsDead && fromCheckpoint >= 0f && fromCheckpoint < 0.3f,
                $"dead {dead}, alive again {!_player.IsDead}, {fromCheckpoint:0.00} m from the checkpoint");
            Check("respawn refills charges and health",
                _injector.Charges == _injector.MaxCharges && _health.Pool.IsFull && beforeDeath == _injector.MaxCharges - 1,
                $"charges before death {beforeDeath}, after {_injector.Charges}, hp {_health.Pool.Current:0}");
            yield return 0.5f;
            _health.TakeDamage(20f);
            Keys(Key.R);
            yield return null;
            Keys();
            yield return 0.3f;
            Check("can heal again after respawning", _player.CurrentState is HealState, _player.CurrentState?.GetType().Name);
            yield return _injector.Duration;
        }

        // ---------- Helpers ----------

        private static void Check(string name, bool ok, string detail)
        {
            if (ok) _passed++;
            else _failed++;
            Log.AppendLine($"{(ok ? "PASS" : "FAIL")} {name}: {detail}");
        }

        private static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));

        private static void QueueMouse(bool down)
        {
            InputSystem.QueueStateEvent(_mouse, new MouseState { buttons = (ushort)(down ? 1 : 0) });
        }

        private static void Hit(float amount)
        {
            Transform p = _player.transform;
            Vector3 point = p.position + Vector3.up * 1.2f + p.forward * 0.4f;
            _player.Receiver.ReceiveHit(new DamageInfo(amount, amount, null, point, -p.forward, canBeParried: false, weight: 1f));
        }

        private static void Note(List<string> states)
        {
            string state = _player.CurrentState?.GetType().Name;
            if (states.Count == 0 || states[states.Count - 1] != state) states.Add(state);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>A close three-quarter view of the knight's right side, where the port is.</summary>
        private static void CaptureClose(string name, bool wide = false)
        {
            Transform p = _player.transform;
            Vector3 chest = p.position + Vector3.up * (wide ? 1.0f : 1.15f);
            Vector3 offset = (p.forward * 1.0f - p.right * 0.75f).normalized * (wide ? 6.5f : 2.6f) + Vector3.up * (wide ? 1.6f : 0.55f);
            _camera.transform.position = chest + offset;
            _camera.transform.LookAt(chest);
            Render(_camera, name);
        }

        /// <summary>The game camera with the HUD drawn into the shot.</summary>
        private static void CaptureHud(string name)
        {
            Camera main = Camera.main;
            GameObject hud = GameObject.Find("HUD");
            Canvas canvas = hud != null ? hud.GetComponent<Canvas>() : null;
            if (main == null) return;
            RenderMode mode = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = main;
                canvas.planeDistance = main.nearClipPlane + 0.5f;
                Canvas.ForceUpdateCanvases();
            }
            Render(main, name);
            if (canvas != null) canvas.renderMode = mode;
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
            _script = null;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            if (_gamepad != null) InputSystem.RemoveDevice(_gamepad);
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            _mouse = null;
            _keyboard = null;
            _gamepad = null;
            _input?.Restore();

            Log.AppendLine($"{_passed} passed, {_failed} failed");
            Directory.CreateDirectory(Folder);
            File.WriteAllText($"{Folder}/injector_test.txt", Log.ToString());
            Debug.Log($"Conquest: nano-injector test written to {Folder}\n{Log}");

            if (!string.IsNullOrEmpty(_returnScene) && EditorSceneManager.GetActiveScene().path != _returnScene)
                EditorSceneManager.OpenScene(_returnScene, OpenSceneMode.Single);
        }
    }
}
