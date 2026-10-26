using Conquest.Targeting;
using UnityEngine;

namespace Conquest.UI
{
    /// <summary>Screen-space marker that follows the current lock-on target.</summary>
    public sealed class LockOnReticle : MonoBehaviour
    {
        [SerializeField] private LockOnController lockOn;
        [SerializeField] private RectTransform marker;
        [SerializeField] private UnityEngine.Camera viewCamera;

        public void Bind(LockOnController controller, RectTransform markerRect, UnityEngine.Camera cam)
        {
            lockOn = controller;
            marker = markerRect;
            viewCamera = cam;
        }

        private void LateUpdate()
        {
            if (marker == null) return;
            if (viewCamera == null) viewCamera = UnityEngine.Camera.main;

            bool show = lockOn != null && lockOn.HasTarget && viewCamera != null;
            Vector3 screen = show ? viewCamera.WorldToScreenPoint(lockOn.CurrentTarget.LockPoint.position) : Vector3.zero;
            show &= screen.z > 0f;

            marker.gameObject.SetActive(show);
            if (show) marker.position = screen;
        }
    }
}
