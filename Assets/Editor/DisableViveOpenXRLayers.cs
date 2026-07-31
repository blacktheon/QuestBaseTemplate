using System;
using UnityEditor;

/// <summary>
/// VIVE Hub / VIVE Business Streaming installs implicit OpenXR API layers
/// (XR_APILAYER_VIVE_*) system-wide. On this machine they make xrCreateInstance
/// fail with XR_ERROR_EXTENSION_DEPENDENCY_NOT_ENABLED when running over
/// Meta Quest Link, so XR never starts in Play Mode.
/// This disables them for the Unity Editor process only, via the layers' own
/// disable_environment kill switches, before XR Plug-in Management initializes.
/// Remove this file if the project should run on VIVE hardware instead.
/// </summary>
[InitializeOnLoad]
internal static class DisableViveOpenXRLayers
{
    static DisableViveOpenXRLayers()
    {
        Environment.SetEnvironmentVariable("DISABLE_XR_APILAYER_VIVE_HAND_TRACKING_1", "1");
        Environment.SetEnvironmentVariable("DISABLE_XR_APILAYER_VIVE_FACIAL_TRACKING_1", "1");
        Environment.SetEnvironmentVariable("DISABLE_XR_APILAYER_VIVE_MR_1", "1");
        Environment.SetEnvironmentVariable("DISABLE_XR_APILAYER_VIVE_XRTRACKER_1", "1");
    }
}
