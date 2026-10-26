using System.Linq;
using Conquest.Combat;
using Conquest.Feedback;
using Conquest.Stats;
using Conquest.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Builds the combat feel assets (blood, sparks, swoosh trail, hit sounds, the hit feedback
    /// profile) and wires them onto characters and the sandbox HUD.
    /// </summary>
    [InitializeOnLoad]
    public static class CombatFeedbackBuilder
    {
        public const string Folder = "Assets/_Project/Art/VFX/Combat";
        public const string AudioFolder = "Assets/_Project/Audio/Combat";
        public const string ProfilePath = "Assets/_Project/Data/Feedback/HitFeedback.asset";

        /// <summary>Bump when the generated profile defaults change; untouched profiles are refreshed.</summary>
        private const int ProfileVersion = 2;

        private static readonly Color BloodRed = new Color(0.56f, 0.04f, 0.07f);
        private static readonly Color BloodDark = new Color(0.4f, 0.02f, 0.05f);

        public sealed class Assets
        {
            public HitFeedbackProfile Profile;
            public ParticleSystem BloodBurst;
            public Material BloodSplat;
            public Material Trail;
            public Material Shield;
            public AudioClip ShieldUp;
            public ParticleSystem MuzzleFlash;
            /// <summary>The small smoke puff where a laser hits a character (see LaserCannon).</summary>
            public ParticleSystem LaserSmoke;
            public Material Tracer;
            public AudioClip[] GunShots;
            public AudioClip[] LightSwings;
            public AudioClip[] HeavySwings;
        }

        static CombatFeedbackBuilder() => SandboxSceneBuilder.SceneBuilt += AddHudEffects;

        public static Assets BuildAssets()
        {
            EditorAssetUtility.EnsureFolder(Folder);

            Material splat = SplatMaterial("Blood_Splat", BloodRed, BloodDark, edgeNoise: 0.2f, satellites: true,
                vertexColor: false, shadows: true);
            Material droplet = SplatMaterial("Blood_Droplet", Color.white, new Color(0.8f, 0.8f, 0.8f), 0f, false,
                vertexColor: true, shadows: false);
            Material spark = SplatMaterial("Spark", Color.white, Color.white, 0f, false, vertexColor: true,
                shadows: false);
            Material trail = TrailMaterial();
            Material shield = ShieldMaterial();

            var assets = new Assets
            {
                BloodSplat = splat,
                Trail = trail,
                Shield = shield,
                BloodBurst = BloodBurst(droplet),
                LightSwings = Clips("Swing_Light_1", "Swing_Light_2"),
                HeavySwings = Clips("Swing_Heavy_1"),
                ShieldUp = Clips("Shield_Up").FirstOrDefault(),
                Tracer = TracerMaterial(),
                GunShots = Clips("Gun_Shot"),
                MuzzleFlash = Sparks("VFX_MuzzleFlash", spark, 10, 0.5f, new Color(1f, 0.92f, 0.6f),
                    new Color(1f, 0.45f, 0.1f)),
                LaserSmoke = Smoke("VFX_LaserSmoke", spark),
            };
            // Blocks land on the energy shield, so they spark cyan.
            ParticleSystem blockSparks = Sparks("VFX_BlockSparks", spark, 16, 0.5f, new Color(0.8f, 0.97f, 1f),
                new Color(0.15f, 0.55f, 1f));
            ParticleSystem parryFlash = Sparks("VFX_ParryFlash", spark, 30, 1.25f, new Color(0.88f, 0.96f, 1f),
                new Color(0.55f, 0.75f, 1f));
            assets.Profile = Profile(blockSparks, parryFlash);
            AssetDatabase.SaveAssets();
            return assets;
        }

        /// <summary>
        /// Adds hit flash, blood, hit feedback, the swoosh trail, swing sounds, the guard's energy
        /// shield and, for characters with a <see cref="ParryGun"/>, its muzzle flash, tracer and bang.
        /// </summary>
        public static void Attach(GameObject root, Assets assets, int characterLayer)
        {
            if (assets == null) return;

            Transform model = root.transform.Find("Model");
            var flash = root.AddComponent<HitFlash>();

            var blood = root.AddComponent<BloodSpray>();
            EditorAssetUtility.Set(blood, "burst", assets.BloodBurst);
            EditorAssetUtility.Set(blood, "splatMaterial", assets.BloodSplat);
            EditorAssetUtility.Set(blood, "groundMask", ~(1 << characterLayer) & ~(1 << 2)); // not characters, not Ignore Raycast
            EditorAssetUtility.Set(blood, "poolAnchor", EditorAssetUtility.FindDeep(root.transform, "Spine"));

            var feedback = root.AddComponent<HitFeedback>();
            EditorAssetUtility.Set(feedback, "profile", assets.Profile);
            EditorAssetUtility.Set(feedback, "visual", model);
            EditorAssetUtility.Set(feedback, "flash", flash);
            EditorAssetUtility.Set(feedback, "blood", blood);

            var detector = root.GetComponent<MeleeHitDetector>();
            var trail = root.AddComponent<WeaponTrail>();
            EditorAssetUtility.Set(trail, "detector", detector);
            var loadout = root.GetComponent<WeaponLoadout>();
            WeaponLoadout.Weapon equipped = EquippedWeapon(loadout);
            EditorAssetUtility.Set(trail, "loadout", loadout);
            EditorAssetUtility.Set(trail, "inner", equipped?.Hilt ?? EditorAssetUtility.FindDeep(root.transform, "Blade_Base"));
            EditorAssetUtility.Set(trail, "outer", equipped?.Tip ?? EditorAssetUtility.FindDeep(root.transform, "Blade_Tip"));
            EditorAssetUtility.Set(trail, "material", assets.Trail);
            EditorAssetUtility.Set(trail, "clockSource", model != null ? model.GetComponent<Animator>() : null);

            var shield = root.AddComponent<EnergyShield>();
            EditorAssetUtility.Set(shield, "material", assets.Shield);
            EditorAssetUtility.Set(shield, "raiseSound", assets.ShieldUp);

            var swing = root.AddComponent<SwingSound>();
            EditorAssetUtility.Set(swing, "detector", detector);
            EditorAssetUtility.SetArray(swing, "lightSwings", assets.LightSwings.Cast<Object>().ToArray());
            EditorAssetUtility.SetArray(swing, "heavySwings", assets.HeavySwings.Cast<Object>().ToArray());

            var gun = root.GetComponent<ParryGun>();
            if (gun == null) return;
            var gunFeedback = root.AddComponent<GunFeedback>();
            EditorAssetUtility.Set(gunFeedback, "gun", gun);
            EditorAssetUtility.Set(gunFeedback, "hitFeedback", feedback);
            EditorAssetUtility.Set(gunFeedback, "muzzleFlash", assets.MuzzleFlash);
            EditorAssetUtility.Set(gunFeedback, "tracerMaterial", assets.Tracer);
            EditorAssetUtility.SetArray(gunFeedback, "shotSounds", assets.GunShots.Cast<Object>().ToArray());
        }

        /// <summary>The weapon a built (not yet playing) loadout will equip, read from its serialized data.</summary>
        private static WeaponLoadout.Weapon EquippedWeapon(WeaponLoadout loadout)
        {
            if (loadout == null) return null;
            var so = new SerializedObject(loadout);
            SerializedProperty list = so.FindProperty("weapons");
            int index = so.FindProperty("equipped").intValue;
            if (index < 0 || index >= list.arraySize) return null;
            SerializedProperty points = list.GetArrayElementAtIndex(index).FindPropertyRelative("bladePoints");
            var weapon = new WeaponLoadout.Weapon { bladePoints = new Transform[points.arraySize] };
            for (int i = 0; i < points.arraySize; i++)
                weapon.bladePoints[i] = (Transform)points.GetArrayElementAtIndex(i).objectReferenceValue;
            return weapon;
        }

        /// <summary>Red screen-edge flash when the player is hurt, behind the HUD bars.</summary>
        private static void AddHudEffects(GameObject player, Camera camera)
        {
            GameObject hud = GameObject.Find("HUD");
            if (hud == null) return;

            var go = new GameObject("Damage Vignette", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(hud.transform, false);
            go.transform.SetAsFirstSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            // Invisible until the vignette flashes (an untouched Image would cover the screen in white).
            var image = go.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;

            var vignette = go.AddComponent<DamageVignette>();
            EditorAssetUtility.Set(vignette, "source", player.GetComponent<Health>());
        }

        // ---------- Materials ----------

        internal static Material SplatMaterial(string name, Color color, Color core, float edgeNoise, bool satellites,
            bool vertexColor, bool shadows)
        {
            Material material = LoadMaterial(name, "Conquest/Flat Splat");
            if (material == null) return null;
            material.SetColor("_BaseColor", color);
            material.SetColor("_CoreColor", core);
            material.SetFloat("_EdgeNoise", edgeNoise);
            material.SetFloat("_Satellites", satellites ? 1f : 0f);
            material.SetFloat("_UseVertexColor", vertexColor ? 1f : 0f);
            material.SetFloat("_ReceiveShadows", shadows ? 1f : 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material TrailMaterial()
        {
            Material material = LoadMaterial("Weapon_Trail", "Conquest/Flat Trail");
            if (material == null) return null;
            material.SetFloat("_Bands", 3f);
            material.SetColor("_EdgeColor", new Color(0.18f, 0.1f, 0.12f, 0.75f));
            material.SetFloat("_EdgeWidth", 0.07f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The gun's tracer: the trail shader as one solid band, without the ink edge.</summary>
        private static Material TracerMaterial()
        {
            Material material = LoadMaterial("Gun_Tracer", "Conquest/Flat Trail");
            if (material == null) return null;
            material.SetFloat("_Bands", 1f);
            material.SetFloat("_EdgeWidth", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material ShieldMaterial()
        {
            Material material = LoadMaterial("Energy_Shield", "Conquest/Energy Shield");
            if (material == null) return null;
            material.SetColor("_BaseColor", new Color(0.1f, 0.5f, 1f));
            material.SetColor("_RimColor", new Color(0.45f, 0.9f, 1f));
            material.SetFloat("_FillAlpha", 0.05f);
            material.SetFloat("_RimAlpha", 0.95f);
            material.SetFloat("_RimPower", 2.6f);
            material.SetFloat("_ScanAlpha", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadMaterial(string name, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"Conquest: shader '{shaderName}' not found; is Assets/_Project/Art/Shaders imported?");
                return null;
            }

            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            return material;
        }

        // ---------- Particle effects ----------

        private static ParticleSystem BloodBurst(Material droplet)
        {
            var root = new GameObject("VFX_BloodBurst");
            ParticleSystem drops = Emitter(root, droplet, count: 26, lifetime: (0.35f, 0.65f), speed: (3.5f, 8f),
                size: (0.07f, 0.16f), gravity: 2.4f, coneAngle: 28f, stretch: true);
            var main = drops.main;
            main.startColor = new ParticleSystem.MinMaxGradient(BloodRed, BloodDark);

            var chunksGo = new GameObject("Chunks");
            chunksGo.transform.SetParent(root.transform, false);
            ParticleSystem chunks = Emitter(chunksGo, droplet, count: 8, lifetime: (0.22f, 0.4f), speed: (1.5f, 3.5f),
                size: (0.15f, 0.3f), gravity: 1.5f, coneAngle: 45f, stretch: false);
            var chunkMain = chunks.main;
            chunkMain.startColor = new ParticleSystem.MinMaxGradient(BloodRed, BloodDark);

            return SavePrefab(root);
        }

        internal static ParticleSystem Sparks(string name, Material spark, int count, float flashSize, Color hot,
            Color cool)
        {
            var root = new GameObject(name);
            ParticleSystem sparks = Emitter(root, spark, count, lifetime: (0.12f, 0.3f), speed: (5f, 11f),
                size: (0.03f, 0.065f), gravity: 1.2f, coneAngle: 55f, stretch: true);
            var main = sparks.main;
            main.startColor = hot;
            var colour = sparks.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(cool, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);

            var flashGo = new GameObject("Flash");
            flashGo.transform.SetParent(root.transform, false);
            ParticleSystem flash = Emitter(flashGo, spark, count: 1, lifetime: (0.07f, 0.07f), speed: (0f, 0f),
                size: (flashSize, flashSize), gravity: 0f, coneAngle: 0f, stretch: false);
            var flashMain = flash.main;
            flashMain.startColor = Color.Lerp(hot, Color.white, 0.6f);
            var flashSizeOverLife = flash.sizeOverLifetime;
            flashSizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.3f));

            return SavePrefab(root);
        }

        /// <summary>A small grey puff that drifts up, swells and fades: laser impacts.</summary>
        private static ParticleSystem Smoke(string name, Material material)
        {
            var root = new GameObject(name);
            ParticleSystem smoke = Emitter(root, material, 7, lifetime: (0.45f, 0.8f), speed: (0.3f, 0.9f),
                size: (0.12f, 0.22f), gravity: -0.08f, coneAngle: 35f, stretch: false);
            var main = smoke.main;
            main.duration = 0.9f;
            main.startColor = new Color(0.62f, 0.6f, 0.57f, 0.85f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var size = smoke.sizeOverLifetime;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.7f));
            var colour = smoke.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(0.8f, 0.78f, 0.74f), 0f), new GradientColorKey(new Color(0.45f, 0.43f, 0.41f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
            var velocity = smoke.limitVelocityOverLifetime;
            velocity.enabled = true;
            velocity.dampen = 0.15f;
            velocity.limit = 0.2f;
            return SavePrefab(root);
        }

        internal static ParticleSystem Emitter(GameObject go, Material material, int count, (float, float) lifetime,
            (float, float) speed, (float, float) size, float gravity, float coneAngle, bool stretch)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.Item1, lifetime.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(8, count * 2);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            short min = (short)Mathf.Max(1, Mathf.RoundToInt(count * 0.8f));
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, min, (short)count) });

            var shape = ps.shape;
            shape.enabled = coneAngle > 0f;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = 0.04f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.04f;
                renderer.lengthScale = 1.6f;
            }
            else renderer.renderMode = ParticleSystemRenderMode.Billboard;

            return ps;
        }

        internal static ParticleSystem SavePrefab(GameObject root)
        {
            string path = $"{Folder}/{root.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ParticleSystem>();
        }

        // ---------- Audio and profile ----------

        private static AudioClip[] Clips(params string[] names) => names
            .Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioFolder}/{n}.wav"))
            .Where(c => c != null)
            .ToArray();

        private static HitFeedbackProfile Profile(ParticleSystem blockSparks, ParticleSystem parryFlash)
        {
            EditorAssetUtility.EnsureFolder(System.IO.Path.GetDirectoryName(ProfilePath)?.Replace('\\', '/'));
            var profile = AssetDatabase.LoadAssetAtPath<HitFeedbackProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<HitFeedbackProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            else if (profile.defaultsVersion >= ProfileVersion) return profile;

            profile.entries = new[]
            {
                new HitFeedbackEntry
                {
                    result = HitResult.Damaged, hitStop = 0.09f, shudder = 0.05f,
                    trauma = 0.32f, kick = 0.14f, zoomPunch = 0.015f,
                    bleed = true, bloodAmount = 1f, flashColor = Color.white, flashDuration = 0.08f,
                    sounds = Clips("Hit_Flesh_1", "Hit_Flesh_2", "Hit_Flesh_3"), volume = 0.9f,
                },
                new HitFeedbackEntry
                {
                    result = HitResult.Killed, hitStop = 0.16f, shudder = 0.07f,
                    trauma = 0.55f, kick = 0.25f, zoomPunch = 0.05f,
                    slowMotionScale = 0.25f, slowMotionDuration = 0.55f,
                    bleed = true, bloodAmount = 1.8f, bloodPool = true, flashColor = Color.white, flashDuration = 0.1f,
                    sounds = Clips("Hit_Flesh_Heavy"), volume = 1f, pitch = new Vector2(0.9f, 1f),
                },
                new HitFeedbackEntry
                {
                    result = HitResult.Blocked, hitStop = 0.07f, attackerHitStop = 1.2f, shudder = 0.025f,
                    trauma = 0.22f, kick = 0.08f,
                    effect = blockSparks, effectTowardAttacker = true,
                    sounds = Clips("Shield_Hit_1", "Shield_Hit_2"), volume = 1f, pitch = new Vector2(0.94f, 1.06f),
                },
                new HitFeedbackEntry
                {
                    result = HitResult.GuardBroken, hitStop = 0.13f, shudder = 0.05f,
                    trauma = 0.5f, kick = 0.2f, zoomPunch = 0.04f,
                    effect = blockSparks, effectScale = 1.6f, effectTowardAttacker = true,
                    flashColor = Color.white, flashDuration = 0.08f,
                    sounds = Clips("Shield_Break"), volume = 1f,
                },
                new HitFeedbackEntry
                {
                    result = HitResult.Parried, hitStop = 0.15f, shudder = 0f,
                    trauma = 0.4f, kick = 0.1f, zoomPunch = 0.06f,
                    slowMotionScale = 0.3f, slowMotionDuration = 0.4f,
                    effect = parryFlash, effectTowardAttacker = true,
                    sounds = Clips("Parry"), volume = 0.9f, pitch = new Vector2(0.97f, 1.03f),
                },
            };
            profile.defaultsVersion = ProfileVersion;
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
