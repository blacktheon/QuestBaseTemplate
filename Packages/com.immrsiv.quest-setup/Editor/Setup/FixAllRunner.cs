using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Runs Fix All across domain reloads. The build-target switch and
    /// sample imports reload scripts mid-run; SessionState carries the intent and
    /// the [InitializeOnLoad] continuation finishes the job, so one click completes
    /// the whole setup instead of silently stopping.</summary>
    [InitializeOnLoad]
    public static class FixAllRunner
    {
        const string PendingKey = "Immrsiv.QuestSetup.FixAll.Pending";
        const string BudgetKey = "Immrsiv.QuestSetup.FixAll.Budget";
        const int StartBudget = 6;

        static FixAllRunner()
        {
            if (Application.isBatchMode) return;
            if (!SessionState.GetBool(PendingKey, false)) return;
            EditorApplication.delayCall += Continue;
        }

        public static void Start()
        {
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(BudgetKey, StartBudget);
            Continue();
        }

        static void Continue()
        {
            int budget = SessionState.GetInt(BudgetKey, 0) - 1;
            SessionState.SetInt(BudgetKey, budget);
            if (budget < 0) { Finish("Fix All stopped: pass budget exhausted."); return; }

            var checks = SetupCheckRegistry.CreateAll();
            int failingBefore = CountFailing(checks);
            if (failingBefore == 0) { Finish("Fix All complete: all required checks pass."); return; }

            foreach (var check in checks)
            {
                bool ok;
                try { ok = check.Evaluate(out _); } catch { ok = false; }
                if (ok) continue;
                Debug.Log($"[QuestSetup] Fix All: fixing '{check.Label}'");
                try { check.Fix(); }
                catch (System.Exception e)
                {
                    Debug.LogError($"[QuestSetup] Fix All: fix failed for '{check.Label}': {e}");
                }
                // A fix may schedule a domain reload (build-target switch, sample
                // import). If that happens mid-loop, the continuation restarts the
                // pass; idempotent Evaluate/Fix makes the re-entry safe.
            }
            AssetDatabase.SaveAssets();

            int failingAfter = CountFailing(SetupCheckRegistry.CreateAll());
            if (failingAfter == 0) Finish("Fix All complete: all required checks pass.");
            else if (failingAfter >= failingBefore)
                Finish($"Fix All stopped: {failingAfter} check(s) still failing and no progress was made - see rows above.");
            else if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                EditorApplication.delayCall += Continue;
            // (if compiling/updating, the [InitializeOnLoad] continuation takes over after the reload)
        }

        static int CountFailing(List<SetupCheck> checks)
        {
            int failing = 0;
            foreach (var check in checks)
            {
                bool ok;
                try { ok = check.Evaluate(out _); } catch { ok = false; }
                if (!ok && !check.IsAdvisory) failing++;
            }
            return failing;
        }

        static void Finish(string message)
        {
            SessionState.SetBool(PendingKey, false);
            Debug.Log("[QuestSetup] " + message);
            QuestSetupWindow.Open();
        }
    }
}
