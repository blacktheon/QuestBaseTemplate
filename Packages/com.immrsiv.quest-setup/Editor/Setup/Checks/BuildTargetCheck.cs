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

        public override bool Evaluate(out string details)
        {
            var active = EditorUserBuildSettings.activeBuildTarget;
            details = active == BuildTarget.Android
                ? "Active build target is Android."
                : $"Active build target is {active} (want Android)." +
                  (Application.isBatchMode ? " Batch mode: relaunch with -buildTarget Android." : "");
            return active == BuildTarget.Android;
        }

        public override void Fix()
        {
            if (Application.isBatchMode)
                throw new System.InvalidOperationException(
                    "Cannot switch build target in batch mode - relaunch Unity with -buildTarget Android.");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            // Triggers a domain reload; FixAllRunner's [InitializeOnLoad] continuation resumes.
        }
    }
}
