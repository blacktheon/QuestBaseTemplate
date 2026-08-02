using UnityEditor;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Batch entries: RunChecks exits 1 if any non-advisory check fails;
    /// FixAll fixes every failing check (advisory included) then exits 0.
    /// RunChecksInternal is the reusable, non-exiting core (also used by verification).</summary>
    public static class SetupCLI
    {
        public static int RunChecksInternal()
        {
            int failures = 0;
            foreach (var check in SetupCheckRegistry.CreateAll())
            {
                bool ok;
                string details;
                try { ok = check.Evaluate(out details); }
                catch (System.Exception e) { ok = false; details = e.Message; }
                var tag = ok ? "PASS" : check.IsAdvisory ? "WARN" : "FAIL";
                Debug.Log($"[QuestSetup] {tag} | {check.Label} | {details}");
                if (!ok && !check.IsAdvisory) failures++;
            }
            Debug.Log($"[QuestSetup] done, {failures} failure(s).");
            return failures;
        }

        public static void RunChecks() => EditorApplication.Exit(RunChecksInternal() == 0 ? 0 : 1);

        public static void FixAll()
        {
            foreach (var check in SetupCheckRegistry.CreateAll())
            {
                if (check is BuildTargetCheck) continue; // batch: -buildTarget Android on the command line
                bool ok;
                try { ok = check.Evaluate(out _); } catch { ok = false; }
                if (ok) continue;
                Debug.Log($"[QuestSetup] fixing: {check.Label}");
                try { check.Fix(); }
                catch (System.Exception e) { Debug.LogError($"[QuestSetup] fix failed: {check.Label}: {e}"); }
            }
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
    }
}
