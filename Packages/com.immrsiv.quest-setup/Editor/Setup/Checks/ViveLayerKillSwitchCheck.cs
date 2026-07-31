using System;
using System.Linq;
using UnityEditor;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>VIVE Hub / VIVE Business Streaming registers implicit OpenXR API layers
    /// (XR_APILAYER_VIVE_*) machine-wide. They make xrCreateInstance fail with
    /// XR_ERROR_EXTENSION_DEPENDENCY_NOT_ENABLED over Meta Quest Link, so XR never
    /// starts in Play Mode. Setting each layer's disable_environment variable in the
    /// editor process turns them off for this editor only. Harmless when VIVE Hub
    /// is not installed.</summary>
    [InitializeOnLoad]
    public static class ViveLayerKillSwitch
    {
        public static readonly string[] EnvVars =
        {
            "DISABLE_XR_APILAYER_VIVE_HAND_TRACKING_1",
            "DISABLE_XR_APILAYER_VIVE_FACIAL_TRACKING_1",
            "DISABLE_XR_APILAYER_VIVE_MR_1",
            "DISABLE_XR_APILAYER_VIVE_XRTRACKER_1",
        };

        static ViveLayerKillSwitch() => Apply();

        public static void Apply()
        {
            foreach (var v in EnvVars)
                Environment.SetEnvironmentVariable(v, "1");
        }

        public static bool ViveLayersRegisteredOnMachine()
        {
#if UNITY_EDITOR_WIN
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit");
            return key != null && key.GetValueNames().Any(n =>
                n.IndexOf("VIVE", StringComparison.OrdinalIgnoreCase) >= 0);
#else
            return false;
#endif
        }
    }

    public class ViveLayerKillSwitchCheck : SetupCheck
    {
        public override string Label => "Machine: VIVE OpenXR implicit layers disabled for this editor";

        public override bool Evaluate(out string details)
        {
            bool allSet = ViveLayerKillSwitch.EnvVars.All(v =>
                System.Environment.GetEnvironmentVariable(v) == "1");
            bool viveInstalled = ViveLayerKillSwitch.ViveLayersRegisteredOnMachine();
            details = !viveInstalled
                ? "No VIVE OpenXR layers registered on this machine (kill switch active anyway)."
                : allSet
                    ? "VIVE Hub layers present; kill-switch env vars active in this editor."
                    : "VIVE Hub layers present and kill switch NOT active.";
            return allSet;
        }

        public override void Fix() => ViveLayerKillSwitch.Apply();
    }
}
