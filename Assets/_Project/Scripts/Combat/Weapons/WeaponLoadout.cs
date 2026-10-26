using System;
using UnityEngine;

namespace Conquest.Combat
{
    /// <summary>
    /// The weapons a character can wield on the same hand bone, and which one is out.
    /// Equipping shows that weapon's model, hides the others and hands its blade points to the
    /// <see cref="MeleeHitDetector"/>; other systems follow <see cref="Equipped"/> (the character
    /// switches to the weapon's animation set and moveset, the trail to its blade).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponLoadout : MonoBehaviour
    {
        [Serializable]
        public sealed class Weapon
        {
            public string name = "Weapon";
            [Tooltip("Shown while this weapon is equipped.")]
            public GameObject model;
            [Tooltip("Points along the blade, from hilt to tip.")]
            public Transform[] bladePoints = new Transform[0];
            [Tooltip("Radius of the hit sweep around the blade points.")]
            [Min(0.01f)] public float hitRadius = 0.15f;

            [Header("Moveset")]
            [Tooltip("Optional animation set for this weapon, e.g. an override controller with two-handed clips. " +
                     "Empty = the character's own animations.")]
            public RuntimeAnimatorController animations;
            [Tooltip("Optional. Replaces the character's light attack chain while equipped.")]
            public AttackData lightAttack;
            [Tooltip("Optional. Replaces the character's heavy attack while equipped.")]
            public AttackData heavyAttack;

            public Transform Hilt => bladePoints.Length > 0 ? bladePoints[0] : null;
            public Transform Tip => bladePoints.Length > 0 ? bladePoints[bladePoints.Length - 1] : null;
        }

        [SerializeField] private MeleeHitDetector detector;
        [SerializeField] private Weapon[] weapons = new Weapon[0];
        [SerializeField, Tooltip("Index of the weapon equipped when play starts.")]
        private int equipped;

        public Weapon Current { get; private set; }
        public int CurrentIndex => equipped;
        public int Count => weapons.Length;

        /// <summary>Raised after a weapon is equipped.</summary>
        public event Action<Weapon> Equipped;

        private void Awake()
        {
            if (detector == null) detector = GetComponent<MeleeHitDetector>();
            if (weapons.Length > 0) Equip(equipped);
        }

        public void Equip(int index)
        {
            if (weapons.Length == 0) return;
            equipped = Mathf.Clamp(index, 0, weapons.Length - 1);
            Current = weapons[equipped];

            for (int i = 0; i < weapons.Length; i++)
                if (weapons[i].model != null) weapons[i].model.SetActive(i == equipped);

            if (detector != null) detector.SetSamplePoints(Current.bladePoints, Current.hitRadius);
            Equipped?.Invoke(Current);
        }

        public void Equip(string weaponName)
        {
            int index = Array.FindIndex(weapons, w => w.name == weaponName);
            if (index >= 0) Equip(index);
        }

        /// <summary>Cycles to the next weapon.</summary>
        public void EquipNext() => Equip((equipped + 1) % Mathf.Max(1, weapons.Length));
    }
}
