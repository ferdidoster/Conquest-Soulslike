using System.Collections.Generic;
using System.Linq;
using Conquest.AI;
using Conquest.Animation;
using Conquest.Character;
using Conquest.Combat;
using Conquest.Feedback;
using Conquest.Movement;
using Conquest.Player;
using Conquest.Stats;
using Conquest.Targeting;
using Conquest.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Assembles character prefabs from the reusable components. The player and the enemy are
    /// the same character; only the driver (input reader vs. AI brain) and faction differ.
    /// </summary>
    public static class CharacterPrefabBuilder
    {
        public struct Setup
        {
            public string Name;
            public GameObject Model;
            public AnimatorController Controller;
            public AttackData LightAttack;
            public AttackData HeavyAttack;
            public Faction Faction;
            public int CharacterLayer;
            public Color? Tint;
            public string MaterialFolder;
            /// <summary>Combat feel (blood, trail, hit feedback). Optional.</summary>
            public CombatFeedbackBuilder.Assets Feedback;
            /// <summary>Name of the weapon equipped at start (see <see cref="Weapons"/>); null = the first.</summary>
            public string Weapon;
            /// <summary>Per weapon name: its animation set and moveset. Weapons without one use the character's.</summary>
            public Dictionary<string, WeaponKit> WeaponKits;
            /// <summary>Gives the character the parry pistol; otherwise its gun meshes are hidden.</summary>
            public bool Gun;
        }

        public struct WeaponKit
        {
            public RuntimeAnimatorController Animations;
            public AttackData LightAttack;
            public AttackData HeavyAttack;
        }

        /// <summary>
        /// Weapons the generated model can carry (Tools/Blender/knight/weapons.py): display name,
        /// mesh object, blade point prefix and count, and hit radius. Missing ones are skipped.
        /// </summary>
        private static readonly (string name, string mesh, string points, int count, float radius)[] Weapons =
        {
            ("Ultra Greatsword", "Weapon_UltraGreatsword", "Greatsword", 4, 0.24f),
            ("Longsword", "Weapon_Longsword", "Longsword", 3, 0.15f),
        };

        public static GameObject BuildPlayer(Setup setup, string prefabPath)
        {
            GameObject root = BuildCharacter(setup);

            var reader = root.AddComponent<PlayerInputReader>();
            root.AddComponent<SwingShake>(); // heavy swings jolt the player's camera
            var lockOn = root.AddComponent<LockOnController>();
            root.AddComponent<CursorLock>();

            Transform head = EditorAssetUtility.FindDeep(root.transform, "Head");
            EditorAssetUtility.Set(lockOn, "eyes", head);
            EditorAssetUtility.Set(lockOn, "occlusionMask", 1 << 0);
            EditorAssetUtility.Set(reader, "lockOn", lockOn);

            var character = root.GetComponent<ActionCharacter>();
            EditorAssetUtility.Set(character, "inputSource", reader);
            EditorAssetUtility.Set(character, "targetSource", lockOn);
            var characterSo = new SerializedObject(character);
            characterSo.FindProperty("moveCancelsRecovery").boolValue = true; // the stick breaks out of recoveries
            characterSo.ApplyModifiedPropertiesWithoutUndo();

            SmoothBlends(root.GetComponent<CharacterAnimator>());
            NanoInjectorBuilder.AttachToPlayer(root); // the heal, its effects and checkpoint respawning
            DataPointsBuilder.AttachToPlayer(root); // collected from enemies, dropped on death

            return Save(root, prefabPath);
        }

        /// <summary>
        /// Transitions for the player: a short floor on cross-fades so nothing snaps (but rolls and swings
        /// still start at once), an eased guard layer, lightly damped locomotion, and the blend tree picking up
        /// at the character's own speed after an action. Enemies keep the defaults.
        /// </summary>
        private static void SmoothBlends(CharacterAnimator characterAnimator)
        {
            if (characterAnimator == null) return;
            var so = new SerializedObject(characterAnimator);
            so.FindProperty("crossFadeScale").floatValue = 1f;
            so.FindProperty("minCrossFade").floatValue = 0.06f;
            so.FindProperty("easeGuardBlend").boolValue = true;
            so.FindProperty("resetLocomotionOnReturn").boolValue = true;
            so.FindProperty("locomotionDamping").floatValue = 0.1f;
            so.FindProperty("guardBlendTime").floatValue = 0.18f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static GameObject BuildEnemy(Setup setup, string prefabPath)
        {
            GameObject root = BuildCharacter(setup);
            var brain = root.AddComponent<MeleeAIBrain>();
            AddOverheadHealthBar(root);

            var character = root.GetComponent<ActionCharacter>();
            EditorAssetUtility.Set(character, "inputSource", brain);
            EditorAssetUtility.Set(character, "targetSource", brain);
            DataPointsBuilder.SetReward(root, DataPointsBuilder.RewardFor(prefabPath));

            return Save(root, prefabPath);
        }

        /// <summary>A world-space health bar floating over the head (see <see cref="OverheadBar"/>).</summary>
        /// <summary>
        /// Builds an enemy as a Prefab Variant of <paramref name="basePrefab"/> (the Bulwark, the parent of
        /// every enemy). The variant swaps in <see cref="Setup.Model"/> and <see cref="Setup.Controller"/> when
        /// they differ from the base, re-pointing every reference into the old model at the same-named part
        /// of the new one; <paramref name="customize"/> then applies the rest (brain, stats...).
        /// </summary>
        public static GameObject BuildEnemyVariant(GameObject basePrefab, Setup setup, string prefabPath,
            System.Action<GameObject> customize = null)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            root.name = setup.Name;
            if (setup.Model != null) SwapModel(root, setup);
            customize?.Invoke(root);
            DataPointsBuilder.SetReward(root, DataPointsBuilder.RewardFor(prefabPath));
            return Save(root, prefabPath);
        }

        /// <summary>
        /// Turns an enemy into a stationary laser gunner: TurretAIBrain + LaserCannon instead of the melee brain.
        /// Its model needs the "Laser_Muzzle" empty and the "Gunner_Blade" mesh (Tools/Blender/knight/gunner_model.py).
        /// </summary>
        public static void MakeGunner(GameObject root, int characterLayer, AudioClip chargeSound, AudioClip fireSound,
            ParticleSystem impactSmoke = null)
        {
            var melee = root.GetComponent<MeleeAIBrain>();
            if (melee != null) Object.DestroyImmediate(melee);

            var cannon = root.AddComponent<LaserCannon>();
            EditorAssetUtility.Set(cannon, "muzzle", EditorAssetUtility.FindDeep(root.transform, "Laser_Muzzle"));
            EditorAssetUtility.Set(cannon, "hitMask", (1 << characterLayer) | (1 << 0)); // characters and walls
            EditorAssetUtility.Set(cannon, "chargeSound", chargeSound);
            EditorAssetUtility.Set(cannon, "fireSound", fireSound);
            EditorAssetUtility.Set(cannon, "impactSmoke", impactSmoke);

            var brain = root.AddComponent<TurretAIBrain>();
            Transform blade = EditorAssetUtility.FindDeep(root.transform, "Gunner_Blade");
            EditorAssetUtility.Set(brain, "cannon", cannon);
            EditorAssetUtility.Set(brain, "character", root.GetComponent<ActionCharacter>());
            EditorAssetUtility.Set(brain, "blade", blade != null ? blade.gameObject : null);
            if (blade != null) blade.gameObject.SetActive(false);

            var character = root.GetComponent<ActionCharacter>();
            EditorAssetUtility.Set(character, "inputSource", brain);
            EditorAssetUtility.Set(character, "targetSource", brain);
        }

        private static void SwapModel(GameObject root, Setup setup)
        {
            Transform old = root.transform.Find("Model");
            if (old != null && PrefabUtility.GetCorrespondingObjectFromOriginalSource(old.gameObject) == setup.Model)
            {
                if (setup.Controller != null && old.TryGetComponent(out Animator same))
                    same.runtimeAnimatorController = setup.Controller;
                return;
            }

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(setup.Model, root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = FacingCorrection(visual.transform);
            if (setup.Tint.HasValue) Tint(visual, setup.Tint.Value, setup.Name, setup.MaterialFolder);

            Animator baseAnimator = old != null ? old.GetComponent<Animator>() : null;
            if (!visual.TryGetComponent(out Animator animator)) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = setup.Controller != null
                ? setup.Controller
                : baseAnimator != null ? baseAnimator.runtimeAnimatorController : null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (old != null)
            {
                RemapReferences(root.transform, old, visual.transform);
                Object.DestroyImmediate(old.gameObject); // recorded as a removed-GameObject override
            }
            visual.name = "Model";
            EditorAssetUtility.SetLayerRecursively(visual, root.layer);
        }

        /// <summary>
        /// Re-points every serialized reference outside the models that targets something inside
        /// <paramref name="from"/> to the part with the same name inside <paramref name="to"/>
        /// (sockets, bones, the Animator...), or to null when the new model has no such part.
        /// </summary>
        private static void RemapReferences(Transform root, Transform from, Transform to)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.transform.IsChildOf(from) || component.transform.IsChildOf(to))
                    continue;
                var so = new SerializedObject(component);
                SerializedProperty property = so.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object value = property.objectReferenceValue;
                    Transform target = value is GameObject go ? go.transform : value is Component c ? c.transform : null;
                    if (target == null || !target.IsChildOf(from)) continue;

                    Transform match = target == from ? to : EditorAssetUtility.FindDeep(to, target.name);
                    property.objectReferenceValue = match == null ? null
                        : value is GameObject ? match.gameObject
                        : (Object)match.GetComponent(value.GetType());
                    changed = true;
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AddOverheadHealthBar(GameObject root)
        {
            var go = new GameObject("Health Bar", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(root.transform, false);
            var canvasRect = (RectTransform)go.transform;
            canvasRect.localPosition = new Vector3(0f, 2.3f, 0f);
            canvasRect.sizeDelta = new Vector2(120f, 11f);
            canvasRect.localScale = Vector3.one * 0.01f; // 1.2 m wide until OverheadBar sizes it in play
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.enabled = false; // hidden in the editor; OverheadBar switches it on in play

            RectTransform frame = UiRect(canvasRect, "Frame", new Color(0.05f, 0.03f, 0.03f, 0.7f), 0f);
            RectTransform trail = UiRect(frame, "Trail", new Color(0.95f, 0.78f, 0.35f, 0.9f), 1.5f);
            RectTransform fill = UiRect(frame, "Fill", new Color(0.72f, 0.08f, 0.08f, 1f), 1.5f);

            var health = root.GetComponent<Health>();
            var bar = go.AddComponent<ResourceBar>();
            EditorAssetUtility.Set(bar, "source", health);
            EditorAssetUtility.Set(bar, "fill", fill);
            EditorAssetUtility.Set(bar, "trail", trail);
            var overhead = go.AddComponent<OverheadBar>();
            EditorAssetUtility.Set(overhead, "source", health);
        }

        /// <summary>A plain Image stretched over its parent, inset by <paramref name="inset"/>.</summary>
        private static RectTransform UiRect(Transform parent, string name, Color color, float inset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        private static GameObject BuildCharacter(Setup setup)
        {
            var root = new GameObject(setup.Name);

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;

            root.AddComponent<CharacterMotor>();
            root.AddComponent<Health>();
            root.AddComponent<Stamina>();
            var receiver = root.AddComponent<CombatReceiver>();
            EditorAssetUtility.Set(receiver, "faction", (int)setup.Faction);
            root.AddComponent<Hurtbox>();

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(setup.Model, root.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = FacingCorrection(visual.transform);
            if (setup.Tint.HasValue) Tint(visual, setup.Tint.Value, setup.Name, setup.MaterialFolder);

            if (!visual.TryGetComponent(out Animator animator)) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = setup.Controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var characterAnimator = root.AddComponent<CharacterAnimator>();
            EditorAssetUtility.Set(characterAnimator, "animator", animator);

            var detector = root.AddComponent<MeleeHitDetector>();
            EditorAssetUtility.SetArray(detector, "samplePoints", new Object[]
            {
                EditorAssetUtility.FindDeep(root.transform, "Blade_Base"),
                EditorAssetUtility.FindDeep(root.transform, "Blade_Mid"),
                EditorAssetUtility.FindDeep(root.transform, "Blade_Tip"),
            });
            EditorAssetUtility.Set(detector, "hitMask", 1 << setup.CharacterLayer);
            AddWeapons(root, detector, setup.Weapon, setup.WeaponKits);
            AddGun(root, setup.Gun, setup.CharacterLayer);

            var targetable = root.AddComponent<Targetable>();
            EditorAssetUtility.Set(targetable, "lockPoint", EditorAssetUtility.FindDeep(root.transform, "LockPoint"));

            var character = root.AddComponent<ActionCharacter>();
            EditorAssetUtility.Set(character, "characterAnimator", characterAnimator);
            EditorAssetUtility.Set(character, "hitDetector", detector);
            EditorAssetUtility.Set(character, "lightAttack", setup.LightAttack);
            EditorAssetUtility.Set(character, "heavyAttack", setup.HeavyAttack);

            CombatFeedbackBuilder.Attach(root, setup.Feedback, setup.CharacterLayer);

            EditorAssetUtility.SetLayerRecursively(root, setup.CharacterLayer);
            return root;
        }

        /// <summary>
        /// Adds a <see cref="ParryGun"/> using the model's pistol (Tools/Blender/knight/weapons.py:
        /// Gun_Hand, Gun_Holster and the Gun_Muzzle socket), or hides the pistol on characters without one.
        /// </summary>
        private static void AddGun(GameObject root, bool armed, int characterLayer)
        {
            Transform hand = EditorAssetUtility.FindDeep(root.transform, "Gun_Hand");
            Transform holster = EditorAssetUtility.FindDeep(root.transform, "Gun_Holster");
            if (hand != null) hand.gameObject.SetActive(false);
            if (holster != null) holster.gameObject.SetActive(armed);
            if (!armed) return;

            var gun = root.AddComponent<ParryGun>();
            EditorAssetUtility.Set(gun, "muzzle", EditorAssetUtility.FindDeep(root.transform, "Gun_Muzzle"));
            EditorAssetUtility.Set(gun, "handModel", hand != null ? hand.gameObject : null);
            EditorAssetUtility.Set(gun, "holsterModel", holster != null ? holster.gameObject : null);
            EditorAssetUtility.Set(gun, "hitMask", (1 << characterLayer) | (1 << 0)); // characters and walls
        }

        /// <summary>
        /// Adds a <see cref="WeaponLoadout"/> with every weapon found on the model, shows the
        /// equipped one and points the hit detector at its blade. Models without separate weapon
        /// meshes keep the Blade_* sockets on the detector.
        /// </summary>
        private static void AddWeapons(GameObject root, MeleeHitDetector detector, string equippedName,
            Dictionary<string, WeaponKit> kits)
        {
            var found = new List<(string name, GameObject model, Transform[] points, float radius)>();
            foreach ((string name, string mesh, string prefix, int count, float radius) in Weapons)
            {
                Transform model = EditorAssetUtility.FindDeep(root.transform, mesh);
                var points = new Transform[count];
                for (int i = 0; i < count; i++) points[i] = EditorAssetUtility.FindDeep(root.transform, $"{prefix}_{i}");
                if (model == null || points.Any(p => p == null)) continue;
                found.Add((name, model.gameObject, points, radius));
            }
            if (found.Count == 0) return;

            int equipped = Mathf.Max(0, found.FindIndex(w => w.name == equippedName));
            var loadout = root.AddComponent<WeaponLoadout>();
            var so = new SerializedObject(loadout);
            so.FindProperty("detector").objectReferenceValue = detector;
            so.FindProperty("equipped").intValue = equipped;
            SerializedProperty list = so.FindProperty("weapons");
            list.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++)
            {
                SerializedProperty weapon = list.GetArrayElementAtIndex(i);
                weapon.FindPropertyRelative("name").stringValue = found[i].name;
                weapon.FindPropertyRelative("model").objectReferenceValue = found[i].model;
                weapon.FindPropertyRelative("hitRadius").floatValue = found[i].radius;
                if (kits != null && kits.TryGetValue(found[i].name, out WeaponKit kit))
                {
                    weapon.FindPropertyRelative("animations").objectReferenceValue = kit.Animations;
                    weapon.FindPropertyRelative("lightAttack").objectReferenceValue = kit.LightAttack;
                    weapon.FindPropertyRelative("heavyAttack").objectReferenceValue = kit.HeavyAttack;
                }
                SerializedProperty points = weapon.FindPropertyRelative("bladePoints");
                points.arraySize = found[i].points.Length;
                for (int p = 0; p < found[i].points.Length; p++)
                    points.GetArrayElementAtIndex(p).objectReferenceValue = found[i].points[p];
                found[i].model.SetActive(i == equipped);
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // The prefab already shows, animates and hits with the equipped weapon before play starts.
            if (kits != null && kits.TryGetValue(found[equipped].name, out WeaponKit equippedKit) &&
                equippedKit.Animations != null)
                root.GetComponentInChildren<Animator>().runtimeAnimatorController = equippedKit.Animations;
            EditorAssetUtility.SetArray(detector, "samplePoints", found[equipped].points.Cast<Object>().ToArray());
            var detectorSo = new SerializedObject(detector);
            detectorSo.FindProperty("radius").floatValue = found[equipped].radius;
            detectorSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Rotates the model so the character faces +Z (Unity forward) regardless of how the FBX
        /// axes were converted. In the bind pose the sword points straight ahead, so its
        /// direction tells us where the model is looking.
        /// </summary>
        private static Quaternion FacingCorrection(Transform model)
        {
            Transform blade = EditorAssetUtility.FindDeep(model, "Blade_Base");
            Transform tip = EditorAssetUtility.FindDeep(model, "Blade_Tip");
            if (blade == null || tip == null) return Quaternion.identity;

            Vector3 facing = model.InverseTransformDirection(tip.position - blade.position);
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-6f) return Quaternion.identity;

            float yaw = Vector3.SignedAngle(facing, Vector3.forward, Vector3.up);
            return Quaternion.Euler(0f, Mathf.Round(yaw / 90f) * 90f, 0f);
        }

        private static void Tint(GameObject visual, Color tint, string prefix, string folder)
        {
            EditorAssetUtility.EnsureFolder(folder);
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    string path = $"{folder}/{prefix}_{materials[i].name}.mat";
                    var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (copy == null)
                    {
                        copy = new Material(materials[i]);
                        copy.color = materials[i].color * tint;
                        AssetDatabase.CreateAsset(copy, path);
                    }

                    materials[i] = copy;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static GameObject Save(GameObject root, string path)
        {
            EditorAssetUtility.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
