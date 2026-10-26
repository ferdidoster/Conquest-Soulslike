using UnityEngine;
using UnityEngine.Rendering;

namespace Conquest.Environment
{
    /// <summary>
    /// The look of a level's air: sky, sun, ambient light and distance haze. It writes the
    /// scene's RenderSettings whenever it is enabled or edited, so dropping the
    /// Desert_Atmosphere prefab into any scene gives it the desert mood.
    /// </summary>
    [ExecuteAlways]
    public sealed class AtmosphereSettings : MonoBehaviour
    {
        [SerializeField] private Material skybox;
        [SerializeField] private Light sun;

        [Header("Ambient")]
        [SerializeField] private Color skyColor = new Color(0.6f, 0.7f, 0.88f);
        [SerializeField] private Color equatorColor = new Color(0.8f, 0.68f, 0.64f);
        [SerializeField] private Color groundColor = new Color(0.5f, 0.36f, 0.36f);

        [Header("Haze")]
        [SerializeField] private bool fog = true;
        [SerializeField] private Color fogColor = new Color(0.78f, 0.73f, 0.86f);
        [SerializeField] private float fogStart = 45f;
        [SerializeField] private float fogEnd = 300f;

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        public void Apply()
        {
            if (skybox != null) RenderSettings.skybox = skybox;
            if (sun != null) RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = skyColor;
            RenderSettings.ambientEquatorColor = equatorColor;
            RenderSettings.ambientGroundColor = groundColor;

            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }
    }
}
