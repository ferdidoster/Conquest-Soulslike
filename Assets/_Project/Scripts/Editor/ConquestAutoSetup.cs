using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Click-free setup driven by marker files in the project root, so the project can be
    /// prepared without anyone opening a menu. Each marker is deleted when its step runs.
    ///   ConquestInstallPackages.pending  installs the optional packages listed below
    ///   ConquestAutoSetup.pending        runs Conquest > Build Knight Sandbox
    /// </summary>
    [InitializeOnLoad]
    internal static class ConquestAutoSetup
    {
        private const string BuildMarker = "ConquestAutoSetup.pending";
        private const string InstallMarker = "ConquestInstallPackages.pending";

        /// <summary>Package id and a type that only exists once the package is installed.</summary>
        private static readonly (string id, string probeType)[] OptionalPackages =
        {
            ("com.unity.cinemachine", "Unity.Cinemachine.CinemachineCamera, Unity.Cinemachine"),
        };

        private static readonly System.Collections.Generic.HashSet<string> Failed =
            new System.Collections.Generic.HashSet<string>();
        private static AddRequest _request;
        private static string _requestedId;

        static ConquestAutoSetup()
        {
            if (File.Exists(BuildMarker) || File.Exists(InstallMarker)) EditorApplication.delayCall += TryRun;
        }

        private static void TryRun()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }

            if (File.Exists(InstallMarker))
            {
                if (!InstallNextPackage()) return; // still working; called again when done
                File.Delete(InstallMarker);
            }

            if (File.Exists(BuildMarker))
            {
                File.Delete(BuildMarker);
                Debug.Log("Conquest: building the knight sandbox automatically...");
                ConquestSetupMenu.Build(confirm: false);
            }
        }

        /// <summary>Returns true once every optional package is installed (or failed to install).</summary>
        private static bool InstallNextPackage()
        {
            if (_request != null)
            {
                if (!_request.IsCompleted)
                {
                    EditorApplication.delayCall += TryRun;
                    return false;
                }

                if (_request.Status == StatusCode.Failure)
                {
                    Failed.Add(_requestedId);
                    Debug.LogError($"Conquest: installing {_requestedId} failed: {_request.Error?.message}");
                }
                else
                    Debug.Log($"Conquest: installed {_request.Result.packageId}");
                _request = null;
                // A successful install recompiles scripts and reloads; the static constructor resumes.
                EditorApplication.delayCall += TryRun;
                return false;
            }

            foreach ((string id, string probeType) in OptionalPackages)
            {
                if (Type.GetType(probeType) != null || Failed.Contains(id)) continue;
                Debug.Log($"Conquest: installing {id}...");
                _requestedId = id;
                _request = Client.Add(id);
                EditorApplication.delayCall += TryRun;
                return false;
            }

            return true;
        }
    }
}
