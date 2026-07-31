using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Imports this package's two bundled sample sets in consumer projects.
    /// In the template itself (package embedded) the originals live directly in
    /// Assets/, so the check auto-passes - importing here would duplicate GUIDs
    /// (same protection VRBase documents for its settings sample).</summary>
    public class SamplesImportedCheck : SetupCheck
    {
        const string PackageName = "com.immrsiv.quest-setup";
        static readonly string[] SampleNames = { "Interaction Example Scenes", "Showcase Samples" };

        public override string Label => "Immrsiv Quest Setup samples imported";

        static bool IsEmbeddedTemplate() =>
            PackageInfo.FindForPackageName(PackageName)?.source == PackageSource.Embedded;

        static List<Sample> Missing() =>
            Sample.FindByPackage(PackageName, null)
                .Where(s => SampleNames.Contains(s.displayName) && !s.isImported)
                .ToList();

        static bool MetaExampleScenesAlsoImported()
        {
            // Our ExampleScenes bundle keeps Meta's GUIDs; importing Meta's own
            // "Example Scenes" sample alongside creates GUID conflicts.
            if (!Directory.Exists("Assets/Samples")) return false;
            // Match by prefix rather than full literal name: Meta's real folder name
            // contains a zero-width character before "SDK", so the full string cannot
            // safely be typed here. Prefix "Meta XR Interaction" is stable and unique.
            return Directory.GetDirectories("Assets/Samples").Any(d =>
                Path.GetFileName(d).StartsWith("Meta XR Interaction") &&
                !Path.GetFileName(d).Contains("Essentials"));
        }

        public override bool Evaluate(out string details)
        {
            if (IsEmbeddedTemplate())
            {
                details = "Template project: sample originals live in Assets/ (import skipped by design).";
                return true;
            }
            var missing = Missing();
            var warn = MetaExampleScenesAlsoImported()
                ? " WARNING: Meta's own 'Example Scenes' sample is imported - remove it or our copy to avoid GUID conflicts."
                : "";
            details = (missing.Count == 0
                ? "Both Immrsiv sample sets imported."
                : "Missing: " + string.Join(", ", missing.Select(s => s.displayName))) + warn;
            return missing.Count == 0;
        }

        public override void Fix()
        {
            foreach (var sample in Missing())
            {
                UnityEngine.Debug.Log($"[QuestSetup] importing sample '{sample.displayName}'");
                sample.Import();
            }
        }
    }
}
