using Conquest.CameraSystem;
using Conquest.CameraSystem.Cinemachine;
using Conquest.Targeting;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace Conquest.EditorTools
{
    /// <summary>Adds the Cinemachine isometric rig and the camera mode switch to the sandbox scene.</summary>
    [InitializeOnLoad]
    internal static class IsometricRigBuilder
    {
        static IsometricRigBuilder() => SandboxSceneBuilder.SceneBuilt += Build;

        public static void Build(GameObject player, Camera camera)
        {
            var lockOn = player.GetComponent<LockOnController>();

            var brain = camera.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.5f);

            var rigGo = new GameObject("Isometric Camera Rig");
            var group = new GameObject("Lock-On Group").AddComponent<CinemachineTargetGroup>();
            group.transform.SetParent(rigGo.transform);

            CinemachineCamera follow = CreateCamera(rigGo.transform, "CM Iso Follow", player.transform, 10);
            CinemachineCamera framing = CreateCamera(rigGo.transform, "CM Iso Lock-On", group.transform, 0);
            var groupFraming = framing.gameObject.AddComponent<CinemachineGroupFraming>();
            groupFraming.SizeAdjustment = CinemachineGroupFraming.SizeAdjustmentModes.ZoomOnly;
            groupFraming.FramingSize = 0.6f;
            groupFraming.OrthoSizeRange = new Vector2(6.5f, 14f);

            var rig = rigGo.AddComponent<IsometricCameraRig>();
            EditorAssetUtility.Set(rig, "followCamera", follow);
            EditorAssetUtility.Set(rig, "lockOnCamera", framing);
            EditorAssetUtility.Set(rig, "lockOnGroup", group);
            EditorAssetUtility.Set(rig, "player", player.transform);
            EditorAssetUtility.Set(rig, "lockOn", lockOn);

            var switcher = camera.gameObject.AddComponent<CameraModeSwitcher>();
            EditorAssetUtility.Set(switcher, "thirdPerson", camera.GetComponent<ThirdPersonCamera>());
            EditorAssetUtility.Set(switcher, "brain", brain);
            EditorAssetUtility.Set(switcher, "isometric", rig);
            EditorAssetUtility.Set(switcher, "lockOn", lockOn);
            EditorAssetUtility.Set(switcher, "player", player.transform);
        }

        private static CinemachineCamera CreateCamera(Transform parent, string name, Transform follow, int priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.rotation = Quaternion.Euler(35f, 45f, 0f);

            var cam = go.AddComponent<CinemachineCamera>();
            cam.Follow = follow;
            cam.Priority = priority;
            cam.Lens = LensSettings.Default;
            cam.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            cam.Lens.OrthographicSize = 6.5f;
            cam.Lens.NearClipPlane = 0.1f;
            cam.Lens.FarClipPlane = 100f;

            go.AddComponent<CinemachineShakeExtension>();

            var composer = go.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 25f;
            composer.Damping = new Vector3(0.4f, 0.4f, 0.4f);
            composer.TargetOffset = new Vector3(0f, 1f, 0f);
            return cam;
        }
    }
}
