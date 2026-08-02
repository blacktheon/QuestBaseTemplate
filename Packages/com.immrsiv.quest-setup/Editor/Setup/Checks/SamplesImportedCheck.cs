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
    /// (same protection VRBase documents for its settings sample).
    /// Import detection is deliberately VERSION-AGNOSTIC: Unity stamps sample
    /// imports with the package version (Assets/Samples/Immrsiv Quest Setup/1.0.0/...)
    /// and its isImported flag only matches the installed version, so after a package
    /// update the stock check would go red forever and a re-import would duplicate
    /// GUIDs against the old copy. A sample folder under ANY version satisfies this
    /// check; Fix only imports samples that exist under NO version.</summary>
    public class SamplesImportedCheck : SetupCheck
    {
        const string PackageName = "com.immrsiv.quest-setup";
        const string ImportRootFolder = "Assets/Samples/Immrsiv Quest Setup";
        static readonly string[] SampleNames = { "Interaction Example Scenes", "Showcase Samples" };

        public override string Label => "Immrsiv Quest Setup samples imported";

        static bool IsEmbeddedTemplate() =>
            PackageInfo.FindForPackageName(PackageName)?.source == PackageSource.Embedded;

        /// <summary>Versions (folder names) under which this sample has a non-empty
        /// import, e.g. ["1.0.0"]. Empty list = never imported.</summary>
        static List<string> ImportedVersions(string sampleName)
        {
            var versions = new List<string>();
            if (!Directory.Exists(ImportRootFolder)) return versions;
            foreach (var versionDir in Directory.GetDirectories(ImportRootFolder))
            {
                var sampleDir = Path.Combine(versionDir, sampleName);
                if (Directory.Exists(sampleDir) && Directory.EnumerateFileSystemEntries(sampleDir).Any())
                    versions.Add(Path.GetFileName(versionDir));
            }
            return versions;
        }

        static string InstalledVersion() => PackageInfo.FindForPackageName(PackageName)?.version;

        static List<Sample> Missing() =>
            Sample.FindByPackage(PackageName, null)
                .Where(s => SampleNames.Contains(s.displayName) && ImportedVersions(s.displayName).Count == 0)
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

            string summary;
            if (missing.Count > 0)
            {
                summary = "Missing: " + string.Join(", ", missing.Select(s => s.displayName));
            }
            else
            {
                var installed = InstalledVersion();
                var staleVersions = SampleNames
                    .SelectMany(ImportedVersions)
                    .Where(v => v != installed)
                    .Distinct()
                    .ToList();
                summary = staleVersions.Count == 0
                    ? "Both Immrsiv sample sets imported."
                    : $"Both Immrsiv sample sets imported (from package {string.Join("/", staleVersions)}; current is {installed}). " +
                      $"Content is unchanged unless the changelog says otherwise - to refresh, delete '{ImportRootFolder}' and click Fix.";
            }
            details = summary + warn;
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
