using UnityEngine;

namespace Conquest.CameraSystem
{
    /// <summary>Camera rotation request for this frame, already in degrees (x = yaw, y = pitch).</summary>
    public interface ILookInput
    {
        Vector2 LookDelta { get; }
    }
}
