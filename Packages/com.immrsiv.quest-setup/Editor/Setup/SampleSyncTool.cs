using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Dev-only (menu appears only while the package is embedded in the
    /// template project): snapshots the template's curated sample folders into
    /// Samples~ for distribution. Copies .meta files so GUIDs survive; never
    /// hand-types the Meta folder name (it contains a zero-width character).</summary>
    public static class SampleSyncTool
    {
        const string MenuPath = "Immrsiv/Quest Setup Dev/Sync Samples From This Project";

        [MenuItem(MenuPath, validate = true)]
        static bool Validate() =>
            UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.immrsiv.quest-setup")?.source == PackageSource.Embedded;

        [MenuItem(MenuPath)]
        public static void Sync()
        {
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.immrsiv.quest-setup");
            if (pkg == null) { Debug.LogError("[SampleSync] com.immrsiv.quest-setup package not found."); return; }
            var samplesRoot = Path.Combine(pkg.resolvedPath, "Samples~");

            var sdkDir = Directory.GetDirectories("Assets/Samples")
                .FirstOrDefault(d => Path.GetFileName(d).StartsWith("Meta XR Interaction") &&
                                     !Path.GetFileName(d).Contains("Essentials"));
            if (sdkDir == null) { Debug.LogError("[SampleSync] Interaction SDK sample folder not found under Assets/Samples."); return; }
            var exampleScenes = Directory.GetDirectories(sdkDir).Select(v => Path.Combine(v, "Example Scenes"))
                .FirstOrDefault(Directory.Exists);
            if (exampleScenes == null) { Debug.LogError("[SampleSync] 'Example Scenes' not found in " + sdkDir); return; }
            if (!Directory.Exists("Assets/ShowcaseSamples")) { Debug.LogError("[SampleSync] Assets/ShowcaseSamples not found."); return; }

            CopyTree(exampleScenes, Path.Combine(samplesRoot, "ExampleScenes"));
            CopyTree("Assets/ShowcaseSamples", Path.Combine(samplesRoot, "ShowcaseSamples"));
            Debug.Log("[SampleSync] done.");
        }

        static void CopyTree(string source, string dest)
        {
            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            Directory.CreateDirectory(dest);
            int files = 0;
            long bytes = 0;
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(source, file);
                var target = Path.Combine(dest, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target);
                files++;
                bytes += new FileInfo(file).Length;
            }
            Debug.Log($"[SampleSync] {source} -> {dest}: {files} files, {bytes / 1024.0 / 1024.0:F1} MB");
        }
    }
}
