using System.Collections.Generic;
using System.Linq;
using Conquest.Animation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Builds the Animator Controller the runtime expects (see <see cref="AnimatorParameters"/> and
    /// <see cref="AnimatorStates"/>) from the clips inside a character FBX. Action states have no
    /// transitions: gameplay code cross-fades into them and back to locomotion.
    /// </summary>
    public static class AnimatorControllerBuilder
    {
        /// <summary>Suffix of a clip's two-handed variant in the character FBX, e.g. "Idle_2H".</summary>
        public const string TwoHandedSuffix = "_2H";

        /// <summary>The state a clip plays in: its name without a variant suffix.</summary>
        public static string StateName(string clipName) =>
            clipName.EndsWith(TwoHandedSuffix) ? clipName.Substring(0, clipName.Length - TwoHandedSuffix.Length) : clipName;

        /// <summary>
        /// Builds an override of <paramref name="controller"/> that plays each clip's variant with
        /// <paramref name="suffix"/> where the model has one (e.g. the two-handed set); other clips stay.
        /// </summary>
        public static AnimatorOverrideController BuildOverride(AnimatorController controller, string modelPath,
            string suffix, string path)
        {
            Dictionary<string, AnimationClip> clips = LoadClips(modelPath);
            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (overrides == null)
            {
                overrides = new AnimatorOverrideController(controller);
                AssetDatabase.CreateAsset(overrides, path);
            }
            overrides.runtimeAnimatorController = controller;

            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrides.overridesCount);
            overrides.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
            {
                AnimationClip original = pairs[i].Key;
                clips.TryGetValue(CharacterModelPostprocessor.ShortName(original.name) + suffix, out AnimationClip variant);
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, variant);
            }
            overrides.ApplyOverrides(pairs);

            EditorUtility.SetDirty(overrides);
            AssetDatabase.SaveAssets();
            return overrides;
        }
        private static readonly (string clip, Vector2 position)[] LocomotionClips =
        {
            ("Idle", Vector2.zero),
            ("Walk_F", new Vector2(0f, 0.5f)), ("Walk_B", new Vector2(0f, -0.5f)),
            ("Walk_L", new Vector2(-0.5f, 0f)), ("Walk_R", new Vector2(0.5f, 0f)),
            ("Run_F", new Vector2(0f, 1f)), ("Run_B", new Vector2(0f, -1f)),
            ("Run_L", new Vector2(-1f, 0f)), ("Run_R", new Vector2(1f, 0f)),
            ("Sprint", new Vector2(0f, 1.5f)),
        };

        private static readonly string[] ActionClips =
        {
            "Attack_1", "Attack_2", "Attack_3", "Attack_Heavy",
            AnimatorStates.RollForward, AnimatorStates.RollBackward, AnimatorStates.RollLeft, AnimatorStates.RollRight,
            AnimatorStates.Backstep, AnimatorStates.Parry, AnimatorStates.Shoot, AnimatorStates.Jump, AnimatorStates.BlockHit, AnimatorStates.GuardBreak,
            AnimatorStates.HitReact, AnimatorStates.Stagger, AnimatorStates.Death,
        };

        /// <summary>Action states only some models have (the knight's nano-injector); skipped quietly when missing.</summary>
        private static readonly string[] OptionalActionClips = { AnimatorStates.Heal };

        public static AnimatorController Build(string modelPath, string controllerPath, string maskPath)
        {
            Dictionary<string, AnimationClip> clips = LoadClips(modelPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            controller.AddParameter(AnimatorParameters.MoveXName, AnimatorControllerParameterType.Float);
            controller.AddParameter(AnimatorParameters.MoveYName, AnimatorControllerParameterType.Float);
            controller.AddParameter(AnimatorParameters.SpeedName, AnimatorControllerParameterType.Float);
            controller.AddParameter(AnimatorParameters.LockedOnName, AnimatorControllerParameterType.Bool);
            controller.AddParameter(AnimatorParameters.GroundedName, AnimatorControllerParameterType.Bool);

            BuildBaseLayer(controller, clips);
            BuildGuardLayer(controller, clips, CreateUpperBodyMask(model, maskPath));

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void BuildBaseLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController(AnimatorStates.Locomotion, out BlendTree tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = AnimatorParameters.MoveXName;
            tree.blendParameterY = AnimatorParameters.MoveYName;
            foreach ((string clip, Vector2 position) in LocomotionClips)
                tree.AddChild(Require(clips, clip), position);
            machine.defaultState = locomotion;

            for (int i = 0; i < ActionClips.Length; i++)
            {
                string name = ActionClips[i];
                AnimatorState state = machine.AddState(name, new Vector3(500f, i * 60f - 300f, 0f));
                state.motion = Require(clips, name);
                state.writeDefaultValues = true;
            }

            for (int i = 0; i < OptionalActionClips.Length; i++)
                if (clips.TryGetValue(OptionalActionClips[i], out AnimationClip clip))
                    AddActionState(machine, OptionalActionClips[i], clip, ActionClips.Length + i);
        }

        private static void AddActionState(AnimatorStateMachine machine, string name, AnimationClip clip, int slot)
        {
            AnimatorState state = machine.AddState(name, new Vector3(500f, slot * 60f - 300f, 0f));
            state.motion = clip;
            state.writeDefaultValues = true;
        }

        /// <summary>
        /// Adds the optional action states the model has clips for to an existing controller, in place, so
        /// prefabs keep their reference to it (a full <see cref="Build"/> recreates the asset). Returns the
        /// states it added.
        /// </summary>
        public static List<string> AddMissingOptionalStates(AnimatorController controller, string modelPath)
        {
            Dictionary<string, AnimationClip> clips = LoadClips(modelPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            var added = new List<string>();
            for (int i = 0; i < OptionalActionClips.Length; i++)
            {
                string name = OptionalActionClips[i];
                if (!clips.TryGetValue(name, out AnimationClip clip)) continue;
                ChildAnimatorState existing = machine.states.FirstOrDefault(s => s.state.name == name);
                if (existing.state != null)
                {
                    existing.state.motion = clip;
                    continue;
                }
                AddActionState(machine, name, clip, ActionClips.Length + i);
                added.Add(name);
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return added;
        }

        private static void BuildGuardLayer(AnimatorController controller, Dictionary<string, AnimationClip> clips,
            AvatarMask mask)
        {
            controller.AddLayer(AnimatorStates.GuardLayer);
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer guard = layers[layers.Length - 1];
            guard.avatarMask = mask;
            guard.defaultWeight = 0f;
            guard.blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;

            AnimatorState idle = guard.stateMachine.AddState(AnimatorStates.GuardIdle);
            idle.motion = Require(clips, AnimatorStates.GuardIdle);
            guard.stateMachine.defaultState = idle;
        }

        /// <summary>Chest, arms, head and the props: everything above the spine.</summary>
        private static AvatarMask CreateUpperBodyMask(GameObject model, string maskPath)
        {
            var mask = new AvatarMask();
            mask.AddTransformPath(model.transform, true);
            for (int i = 0; i < mask.transformCount; i++)
            {
                string path = mask.GetTransformPath(i);
                mask.SetTransformActive(i, path.Contains("/Chest"));
            }

            AssetDatabase.DeleteAsset(maskPath);
            AssetDatabase.CreateAsset(mask, maskPath);
            return mask;
        }

        private static Dictionary<string, AnimationClip> LoadClips(string modelPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .GroupBy(c => CharacterModelPostprocessor.ShortName(c.name))
                .ToDictionary(g => g.Key, g => g.First());
        }

        private static AnimationClip Require(Dictionary<string, AnimationClip> clips, string name)
        {
            if (clips.TryGetValue(name, out AnimationClip clip)) return clip;
            Debug.LogError($"Animation clip '{name}' is missing from the character model.");
            return null;
        }
    }
}
