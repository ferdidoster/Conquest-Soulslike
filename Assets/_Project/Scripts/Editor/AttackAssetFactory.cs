using Conquest.Combat;
using Conquest.Core.Utilities;
using UnityEditor;
using UnityEngine;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Creates the default knight moveset. Timings mirror the Blender animations
    /// (Tools/Blender/knight/animations.py, 30 fps). Assets you have tuned yourself are kept;
    /// assets still on older generated defaults are refreshed when <see cref="DefaultsVersion"/> rises.
    /// </summary>
    public static class AttackAssetFactory
    {
        /// <summary>
        /// 4: separate one-handed (knight) and two-handed (greatsword) movesets, both with a chargeable heavy.
        /// 5: greatsword swings step forward with the stomp of the lead foot, then catch up in the recovery.
        /// 6: heavier greatsword: a slow wind-up that hangs before the strike, a fast strike, a dragging
        ///    recovery (<see cref="AttackData.tempo"/>), heavier blows and earlier commitment to the aim.
        /// 7: greatsword cooldown: a short pause after a swing before a new one can start, and a later combo window.
        /// 8: fluid recovery: moving cancels the end of a swing (<see cref="AttackData.moveCancelFrom"/>), the
        ///    greatsword recovery no longer drags after the strike, and each moveset has a roll attack.
        /// 9: a jump attack for each moveset: the heavy's overhead slam, started just before the strike.
        /// </summary>
        private const int DefaultsVersion = 9;

        /// <summary>Forward travel between two clip frames (30 fps), e.g. a step synced to the animation's feet.</summary>
        private readonly struct Step
        {
            public readonly int From, To;
            public readonly float Metres;

            public Step(int from, int to, float metres)
            {
                From = from;
                To = to;
                Metres = metres;
            }
        }

        /// <summary>One-handed sword: quick slashes (Attack_1..3) and an overhead heavy.</summary>
        public static (AttackData light, AttackData heavy) CreateKnightMoveset(string folder)
        {
            EditorAssetUtility.EnsureFolder(folder);

            AttackData a1 = Create(folder, "Attack_1", 32, new TimeWindow(0.36f, 0.53f), 20f, 18f, weight: 1f,
                lunge: 5f);
            AttackData a2 = Create(folder, "Attack_2", 31, new TimeWindow(0.34f, 0.52f), 22f, 18f, weight: 1f,
                lunge: 5f);
            AttackData a3 = Create(folder, "Attack_3", 38, new TimeWindow(0.38f, 0.55f), 30f, 22f, weight: 1.6f,
                lunge: 6f);
            AttackData heavy = Create(folder, "Attack_Heavy", 50, new TimeWindow(0.44f, 0.58f), 45f, 30f,
                weight: 2.2f, lunge: 7f, chargeAt: 21f / 50f);

            Link(a1, a2);
            Link(a2, a3);
            SetRollAttack(a1, CreateFollowUp(folder, "Attack_Roll", a1, startAt: 0.2f, damageScale: 0.85f));
            SetJumpAttack(a1, heavy, CreateFollowUp(folder, "Attack_Jump", heavy, startAt: heavy.activeWindow.start - 0.1f,
                damageScale: 0.9f));
            AssetDatabase.SaveAssets();
            return (a1, heavy);
        }

        /// <summary>
        /// Two-handed ultra greatsword after Elden Ring: shoulder cleave, return sweep, overhead slam, and a
        /// heavy that hauls the blade up and slams it down. Plays the "_2H" clips through the weapon's
        /// animation set, so the assets name the same states as the one-handed moveset.
        /// </summary>
        public static (AttackData light, AttackData heavy) CreateGreatswordMoveset(string folder)
        {
            EditorAssetUtility.EnsureFolder(folder);

            // Each swing lifts the lead foot in the coil and stomps it forward on the strike frame
            // (STEP_LIFT -> DEEP_LUNGE in animations.py); the rear foot catches up during the recovery.
            AttackData a1 = Create(folder, "Attack_1", 44, new TimeWindow(0.36f, 0.50f), 26f, 20f, weight: 1.6f,
                lunge: 0f, step: new Step(14, 18, 0.75f), catchUp: new Step(29, 37, 0.25f), heavy: true);
            AttackData a2 = Create(folder, "Attack_2", 42, new TimeWindow(0.33f, 0.48f), 28f, 20f, weight: 1.6f,
                lunge: 0f, step: new Step(13, 16, 0.7f), catchUp: new Step(26, 34, 0.25f), heavy: true);
            AttackData a3 = Create(folder, "Attack_3", 54, new TimeWindow(0.39f, 0.48f), 38f, 26f, weight: 2.3f,
                lunge: 0f, step: new Step(19, 23, 0.85f), catchUp: new Step(36, 46, 0.3f), heavy: true);
            AttackData heavy = Create(folder, "Attack_Heavy", 64, new TimeWindow(0.41f, 0.48f), 50f, 32f,
                weight: 2.6f, lunge: 0f, chargeAt: 24f / 64f, step: new Step(24, 28, 0.95f),
                catchUp: new Step(44, 55, 0.35f), heavy: true);

            Link(a1, a2);
            Link(a2, a3);
            SetRollAttack(a1, CreateFollowUp(folder, "Attack_Roll", a1, startAt: 0.22f, damageScale: 0.85f));
            SetJumpAttack(a1, heavy, CreateFollowUp(folder, "Attack_Jump", heavy, startAt: heavy.activeWindow.start - 0.08f,
                damageScale: 0.9f));
            AssetDatabase.SaveAssets();
            return (a1, heavy);
        }

        /// <param name="frames">Clip length at 30 fps.</param>
        /// <param name="active">Hit window; the strike starts at its beginning.</param>
        /// <param name="heavy">Weapon heft: <see cref="HeavyTempo"/> and an earlier end to tracking.</param>
        private static AttackData Create(string folder, string name, int frames, TimeWindow active, float damage,
            float stamina, float weight, float lunge, float chargeAt = -1f, Step? step = null, Step? catchUp = null,
            bool heavy = false)
        {
            string path = $"{folder}/{name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (data != null && data.defaultsVersion >= DefaultsVersion) return data;
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<AttackData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.animationState = name;
            data.crossFade = 0.08f;
            data.duration = frames / 30f;
            data.activeWindow = active;
            // A heavy weapon has to settle before the next swing of the combo can follow.
            data.comboWindow = new TimeWindow(active.end + (heavy ? 0.15f : -0.05f), 0.95f);
            data.dodgeCancelFrom = active.end + 0.1f;
            data.moveCancelFrom = Mathf.Max(0.7f, data.comboWindow.start + 0.1f);
            data.trackingUntil = Mathf.Max(0.1f, active.start - (heavy ? 0.16f : 0.08f));
            data.tempo = heavy ? HeavyTempo(active) : new AnimationCurve();
            data.cooldown = heavy ? 0.35f : 0f;
            data.damage = damage;
            data.guardDamage = damage;
            data.staminaCost = stamina;
            data.weight = weight;

            data.chargeable = chargeAt >= 0f;
            if (data.chargeable) data.chargeAt = chargeAt;

            if (step.HasValue)
            {
                data.forwardSpeed = StepCurve(frames, step.Value, catchUp);
                data.defaultsVersion = DefaultsVersion;
                EditorUtility.SetDirty(data);
                return data;
            }

            // Weight shifts back during the coil, then the body drives forward through the strike.
            // A charge point holds still, so the lunge only starts once the charge is released.
            float coil = active.start - 0.1f;
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(coil, -0.6f), new Keyframe(active.start, lunge),
                new Keyframe(active.end, lunge * 0.15f), new Keyframe(active.end + 0.1f, 0f), new Keyframe(1f, 0f));
            if (data.chargeable && chargeAt > coil && chargeAt < active.start) curve.AddKey(chargeAt, 0f);
            data.forwardSpeed = curve;

            data.defaultsVersion = DefaultsVersion;
            EditorUtility.SetDirty(data);
            return data;
        }

        /// <summary>
        /// Playback speed of a heavy weapon: the wind-up drags and hangs for a beat at the top and the strike
        /// snaps through faster than the clip. The blade is reined in with a short check after the strike, then
        /// the recovery plays at the clip's speed so the next action isn't kept waiting.
        /// </summary>
        private static AnimationCurve HeavyTempo(TimeWindow active)
        {
            float s = active.start, e = active.end;
            return new AnimationCurve(
                new Keyframe(0f, 0.85f, 0f, 0f), new Keyframe(s - 0.14f, 0.7f, 0f, 0f),
                new Keyframe(s - 0.05f, 0.5f, 0f, 0f), new Keyframe(s, 1.35f, 0f, 0f),
                new Keyframe(e, 1.2f, 0f, 0f), new Keyframe(e + 0.05f, 0.8f, 0f, 0f),
                new Keyframe(e + 0.15f, 1f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
        }

        /// <summary>
        /// An attack out of a roll or a jump: <paramref name="source"/> started past most of its wind-up
        /// (<paramref name="startAt"/>), at the clip's own speed and without a charge.
        /// </summary>
        private static AttackData CreateFollowUp(string folder, string name, AttackData source, float startAt,
            float damageScale)
        {
            string path = $"{folder}/{name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (data != null && data.defaultsVersion >= DefaultsVersion) return data;
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<AttackData>();
                AssetDatabase.CreateAsset(data, path);
            }

            EditorUtility.CopySerialized(source, data);
            data.name = name; // CopySerialized copies the source's name too
            data.startAt = startAt;
            data.tempo = new AnimationCurve();
            data.chargeable = false;
            data.cooldown = 0f;
            data.trackingUntil = Mathf.Max(data.trackingUntil, startAt + 0.08f);
            data.damage = data.guardDamage = source.damage * damageScale;
            data.next = null;
            data.rollAttack = null;
            data.jumpAttack = null;
            data.defaultsVersion = DefaultsVersion;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void SetRollAttack(AttackData chainStart, AttackData rollAttack)
        {
            if (chainStart.rollAttack == rollAttack) return;
            chainStart.rollAttack = rollAttack;
            EditorUtility.SetDirty(chainStart);
        }

        /// <summary>Either attack button in the air does the jump attack.</summary>
        private static void SetJumpAttack(AttackData light, AttackData heavy, AttackData jumpAttack)
        {
            foreach (AttackData chainStart in new[] { light, heavy })
            {
                if (chainStart.jumpAttack == jumpAttack) continue;
                chainStart.jumpAttack = jumpAttack;
                EditorUtility.SetDirty(chainStart);
            }
        }

        /// <summary>
        /// Speed over normalized time: a small shift back while winding up, then a bump for each step
        /// whose area is the step's distance (flat tangents, so each bump's area is peak x duration / 2).
        /// </summary>
        private static AnimationCurve StepCurve(int frames, Step step, Step? catchUp)
        {
            float duration = frames / 30f;
            var curve = new AnimationCurve(new Keyframe(0f, 0f));
            curve.AddKey(new Keyframe(step.From * 0.6f / frames, -0.4f, 0f, 0f));
            AddBump(curve, step, frames, duration);
            if (catchUp.HasValue) AddBump(curve, catchUp.Value, frames, duration);
            curve.AddKey(new Keyframe(1f, 0f, 0f, 0f));
            return curve;
        }

        private static void AddBump(AnimationCurve curve, Step step, int frames, float duration)
        {
            float from = (float)step.From / frames, to = (float)step.To / frames;
            float seconds = (to - from) * duration;
            curve.AddKey(new Keyframe(from, 0f, 0f, 0f));
            curve.AddKey(new Keyframe((from + to) * 0.5f, 2f * step.Metres / seconds, 0f, 0f));
            curve.AddKey(new Keyframe(to, 0f, 0f, 0f));
        }

        private static void Link(AttackData from, AttackData to)
        {
            if (from.next != null) return;
            from.next = to;
            EditorUtility.SetDirty(from);
        }
    }
}
