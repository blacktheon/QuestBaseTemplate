using UnityEditor;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Quest builds target Android; every later per-target fix (and both
    /// parity tools) evaluate against the ACTIVE target, so this runs first.
    /// The switch triggers a domain reload — FixAllRunner resumes the remaining
    /// fixes afterwards. In batch mode the switch is not performed: pass
    /// -buildTarget Android on the Unity command line instead.</summary>
    public class BuildTargetCheck : SetupCheck
    {
        public override string Label => "Active build target: Android";

        static bool AndroidModuleInstalled =>
            BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);

        public override bool Evaluate(out string details)
        {
            var active = EditorUserBuildSettings.activeBuildTarget;
            if (active == BuildTarget.Android)
            {
                details = "Active build target is Android.";
                return true;
            }
            details = $"Active build target is {active} (want Android)." +
                      (!AndroidModuleInstalled
                          ? " Android Build Support is NOT installed - add it to this editor via Unity Hub first."
                          : "") +
                      (Application.isBatchMode ? " Batch mode: relaunch with -buildTarget Android." : "");
            return false;
        }

        public override void Fix()
        {
            if (Application.isBatchMode)
                throw new System.InvalidOperationException(
                    "Cannot switch build target in batch mode - relaunch Unity with -buildTarget Android.");
            if (!AndroidModuleInstalled)
                throw new System.InvalidOperationException(
                    "Android Build Support is not installed for this editor. Install it via Unity Hub (Installs > your editor > Add modules > Android Build Support), restart, then run Fix All again.");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new System.InvalidOperationException(
                    "SwitchActiveBuildTarget(Android) failed - see the console for Unity's reason, then run Fix All again.");
            // Triggers a domain reload; FixAllRunner's [InitializeOnLoad] continuation resumes.
        }
    }
}
