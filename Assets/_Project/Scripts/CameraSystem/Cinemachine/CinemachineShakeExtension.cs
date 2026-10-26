using Unity.Cinemachine;
using UnityEngine;

namespace Conquest.CameraSystem.Cinemachine
{
    /// <summary>
    /// Feeds the shared <see cref="CameraShake"/> signal into a Cinemachine camera, so hits shake
    /// the isometric view exactly like the orbit camera. Add it to every CinemachineCamera.
    /// </summary>
    [AddComponentMenu("Conquest/Cinemachine Shake Extension")]
    public sealed class CinemachineShakeExtension : CinemachineExtension
    {
        [SerializeField, Range(0f, 2f), Tooltip("Scales the shared shake for this camera.")]
        private float strength = 1f;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;

            CameraShakeSample shake = CameraShake.Sample(state.GetFinalOrientation());
            state.PositionCorrection += shake.Offset * strength;
            state.OrientationCorrection *= Quaternion.Euler(0f, 0f, shake.Roll * strength);

            // Never let a zoom punch collapse or flip the lens.
            float zoom = Mathf.Clamp(1f - shake.Zoom * strength, 0.5f, 1f);
            if (state.Lens.Orthographic) state.Lens.OrthographicSize *= zoom;
            else state.Lens.FieldOfView *= zoom;
        }
    }
}
