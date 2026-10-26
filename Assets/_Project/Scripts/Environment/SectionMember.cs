using UnityEngine;

namespace Conquest.Environment
{
    /// <summary>
    /// Adds a piece to more sections of its <see cref="LevelSections"/>, e.g. a wall or a gate
    /// between two rooms that both rooms should show.
    /// </summary>
    public sealed class SectionMember : MonoBehaviour
    {
        [SerializeField] private string[] sections = { "Hub" };

        public string[] Sections => sections;
    }
}
