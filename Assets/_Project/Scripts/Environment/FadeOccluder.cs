using UnityEngine;

namespace Conquest.Environment
{
    /// <summary>
    /// Marks a tall piece (wall, pillar, big machine) that the camera may hide when it stands
    /// between the camera and the player. Hidden pieces keep casting shadows and colliding.
    /// </summary>
    public sealed class FadeOccluder : MonoBehaviour
    {
        [Tooltip("A wall (or locked door) that LevelSections cuts away when it faces the camera. Off for tall " +
                 "props, which are only cut while they stand between the camera and the player.")]
        [SerializeField] private bool isWall = true;

        private Renderer[] _renderers;

        public bool IsWall => isWall;
        private UnityEngine.Rendering.ShadowCastingMode[] _modes;
        private bool _hidden;

        public bool Hidden
        {
            get => _hidden;
            set
            {
                if (_hidden == value) return;
                _hidden = value;
                Cache();
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null)
                        _renderers[i].shadowCastingMode = value
                            ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                            : _modes[i];
            }
        }

        private void Cache()
        {
            if (_renderers != null) return;
            _renderers = GetComponentsInChildren<Renderer>();
            _modes = new UnityEngine.Rendering.ShadowCastingMode[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _modes[i] = _renderers[i].shadowCastingMode;
        }
    }
}
