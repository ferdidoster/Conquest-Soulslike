using System.Collections.Generic;
using Conquest.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Conquest.EditorTools
{
    /// <summary>One-click setup: Animator Controller, moveset, prefabs, the desert kit and a sandbox scene.</summary>
    public static class ConquestSetupMenu
    {
        private const string Root = "Assets/_Project";
        private const string KnightModel = Root + "/Art/Characters/Knight/Knight.fbx";
        private const string RobotModel = Root + "/Art/Characters/Robot/Robot.fbx"; // the Sentinel enemy
        private const string BruteModel = Root + "/Art/Characters/Brute/Brute.fbx"; // the Bulwark, a heavy enemy
        private const string GunnerModel = Root + "/Art/Characters/Gunner/Gunner.fbx"; // the Warden, a laser gunner
        private const string GunnerPrefab = Root + "/Prefabs/Characters/Enemy_Warden.prefab";
        private const string HeavyEnemyPrefab = Root + "/Prefabs/Characters/Enemy_Bulwark.prefab";
        private const string AnimationFolder = Root + "/Art/Characters/Knight/Animation";
        private const string MaterialFolder = Root + "/Art/Materials";
        private const string MovesetFolder = Root + "/Data/Moveset/Knight";
        private const string GreatswordFolder = Root + "/Data/Moveset/Greatsword";
        private const string PlayerPrefab = Root + "/Prefabs/Characters/Player_Knight.prefab";
        private const string EnemyPrefab = Root + "/Prefabs/Characters/Enemy_Knight.prefab";
        private const string SandboxScene = Root + "/Scenes/Sandbox.unity";
        private const string CharacterLayer = "Character";

        public const string SandboxScenePath = SandboxScene;

        [MenuItem("Conquest/Build Knight Sandbox", priority = 0)]
        public static void BuildEverything() => Build(confirm: true);

        /// <summary>Builds everything; <paramref name="confirm"/> asks first (used by the menu).</summary>
        public static bool Build(bool confirm)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KnightModel);
            if (model == null)
            {
                EditorUtility.DisplayDialog("Conquest", $"Knight model not found at {KnightModel}.\n" +
                                                        "Run Tools/Blender/build_knight.py first.", "OK");
                return false;
            }

            if (confirm && !EditorUtility.DisplayDialog("Conquest",
                    "This (re)builds the knight Animator Controller, prefabs, the desert arena kit and the Sandbox scene. " +
                    "Attack data you already tuned is kept. Continue?", "Build", "Cancel"))
                return false;

            SaveOpenScenesWithPath();

            // Make sure the import rules from CharacterModelPostprocessor are applied.
            AssetDatabase.ImportAsset(KnightModel, ImportAssetOptions.ForceUpdate);

            EditorAssetUtility.EnsureFolder(AnimationFolder);
            AnimatorController controller = AnimatorControllerBuilder.Build(
                KnightModel, AnimationFolder + "/Knight.controller", AnimationFolder + "/UpperBody.mask");

            (AttackData light, AttackData heavy) = AttackAssetFactory.CreateKnightMoveset(MovesetFolder);

            // Two-handed weapons play the "_2H" clips and their own moveset.
            AnimatorOverrideController twoHanded = AnimatorControllerBuilder.BuildOverride(controller, KnightModel,
                AnimatorControllerBuilder.TwoHandedSuffix, AnimationFolder + "/Knight_TwoHanded.overrideController");
            (AttackData greatLight, AttackData greatHeavy) = AttackAssetFactory.CreateGreatswordMoveset(GreatswordFolder);
            var weaponKits = new Dictionary<string, CharacterPrefabBuilder.WeaponKit>
            {
                ["Ultra Greatsword"] = new CharacterPrefabBuilder.WeaponKit
                {
                    Animations = twoHanded, LightAttack = greatLight, HeavyAttack = greatHeavy,
                },
            };
            int layer = EditorAssetUtility.EnsureLayer(CharacterLayer);
            CombatFeedbackBuilder.Assets feedback = CombatFeedbackBuilder.BuildAssets();

            var setup = new CharacterPrefabBuilder.Setup
            {
                Name = "Player_Knight",
                Model = model,
                Controller = controller,
                LightAttack = light,
                HeavyAttack = heavy,
                Faction = Faction.Player,
                CharacterLayer = layer,
                MaterialFolder = MaterialFolder,
                Feedback = feedback,
                Weapon = "Ultra Greatsword",
                WeaponKits = weaponKits,
                Gun = true,
            };
            GameObject player = CharacterPrefabBuilder.BuildPlayer(setup, PlayerPrefab);

            // ---- Enemies ----
            // The Bulwark is the parent of every enemy: it is built from scratch, and every other enemy is a
            // Prefab Variant of Enemy_Bulwark that only overrides what differs (model, animator, brain, stats).
            // New enemies follow the same pattern: CharacterPrefabBuilder.BuildEnemyVariant(heavyEnemy, ...).
            setup.Faction = Faction.Enemy;
            setup.Weapon = "Longsword"; // the knight's original blade
            setup.Gun = false;
            setup.Tint = null;

            setup.Name = "Enemy_Bulwark";
            var brute = AssetDatabase.LoadAssetAtPath<GameObject>(BruteModel);
            if (brute != null)
            {
                AssetDatabase.ImportAsset(BruteModel, ImportAssetOptions.ForceUpdate);
                setup.Model = brute;
            }
            else
            {
                setup.Tint = new Color(0.55f, 0.45f, 0.45f); // no Bulwark model yet: a tinted knight stands in
            }
            GameObject heavyEnemy = CharacterPrefabBuilder.BuildEnemy(setup, HeavyEnemyPrefab);
            SetFloat(heavyEnemy.GetComponent<Conquest.Stats.Health>(), "maxHealth", 250f);
            // The Bulwark raises its guard: about a third of its decisions are a block.
            SetFloat(heavyEnemy.GetComponent<Conquest.AI.MeleeAIBrain>(), "blockChance", 0.35f);
            PrefabUtility.SavePrefabAsset(heavyEnemy); // write the tweaks above into the prefab file
            setup.Tint = null;

            // The Sentinel: the lighter sword robot. A variant with its own model, normal health and no blocking.
            setup.Name = "Enemy_Knight";
            var robot = AssetDatabase.LoadAssetAtPath<GameObject>(RobotModel);
            if (robot != null) AssetDatabase.ImportAsset(RobotModel, ImportAssetOptions.ForceUpdate);
            setup.Model = robot != null ? robot : model;
            GameObject enemy = CharacterPrefabBuilder.BuildEnemyVariant(heavyEnemy, setup, EnemyPrefab, root =>
            {
                SetFloat(root.GetComponent<Conquest.Stats.Health>(), "maxHealth", 100f);
                SetFloat(root.GetComponent<Conquest.AI.MeleeAIBrain>(), "blockChance", 0f);
            });

            DesertArtDirection.ApplyProjectLook(MaterialFolder);
            DesertKitBuilder.Result environment = DesertKitBuilder.IsAvailable ? DesertKitBuilder.Build() : null;

            // The Warden: stationary, shoots lasers from range and draws a blade up close. A variant with the
            // droid model, its own animator (shorter legs) and the turret brain instead of the melee one.
            GameObject gunner = null;
            var gunnerModel = AssetDatabase.LoadAssetAtPath<GameObject>(GunnerModel);
            if (gunnerModel != null)
            {
                AssetDatabase.ImportAsset(GunnerModel, ImportAssetOptions.ForceUpdate);
                setup.Name = "Enemy_Warden";
                setup.Model = gunnerModel;
                setup.Controller = AnimatorControllerBuilder.Build(GunnerModel,
                    AnimationFolder + "/Gunner.controller", AnimationFolder + "/Gunner_UpperBody.mask");
                AudioClip charge = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Combat/Shield_Up.wav");
                AudioClip fire = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Combat/Gun_Shot.wav");
                gunner = CharacterPrefabBuilder.BuildEnemyVariant(heavyEnemy, setup, GunnerPrefab, root =>
                {
                    CharacterPrefabBuilder.MakeGunner(root, layer, charge, fire, feedback?.LaserSmoke);
                    SetFloat(root.GetComponent<Conquest.Stats.Health>(), "maxHealth", 200f);
                });
            }

            SandboxSceneBuilder.Build(player, enemy, SandboxScene, MaterialFolder, environment, heavyEnemy, gunner);
            AssetDatabase.SaveAssets();
            Debug.Log("Conquest: knight sandbox ready. Press Play in Assets/_Project/Scenes/Sandbox.unity.");
            return true;
        }

        private static void SetFloat(Object target, string property, float value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            so.FindProperty(property).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Building opens a new scene, so keep unsaved work in already-saved scenes.</summary>
        private static void SaveOpenScenesWithPath()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path))
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }
        }

        [MenuItem("Conquest/Rebuild Desert Kit Prefabs", priority = 2)]
        public static void BuildDesertKitOnly()
        {
            DesertArtDirection.ApplyProjectLook(MaterialFolder);
            DesertKitBuilder.Build();
        }

        [MenuItem("Conquest/Rebuild Knight Animator Only", priority = 1)]
        public static void BuildAnimatorOnly()
        {
            EditorAssetUtility.EnsureFolder(AnimationFolder);
            AnimatorControllerBuilder.Build(KnightModel, AnimationFolder + "/Knight.controller",
                AnimationFolder + "/UpperBody.mask");
        }
    }
}
