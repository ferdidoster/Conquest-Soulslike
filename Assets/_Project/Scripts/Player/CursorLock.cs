using UnityEngine;

namespace Conquest.Player
{
    /// <summary>Locks and hides the cursor while playing. Escape releases it, clicking re-locks.</summary>
    public sealed class CursorLock : MonoBehaviour
    {
        private void OnEnable() => Lock(true);
        private void OnDisable() => Lock(false);

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Lock(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                Lock(true);
        }

        private static void Lock(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
