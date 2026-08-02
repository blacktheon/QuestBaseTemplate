# Immrsiv Quest Setup v1.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the three gaps found in the real install test: active-build-target switching with Fix All resume, Meta XR Project Setup Tool parity, and XR Project Validation parity — per the v1.1 amendment in `docs/superpowers/specs/2026-07-31-quest-setup-package-design.md` (read the amendment; it holds the policy decisions).

**Architecture:** Three new/extended `SetupCheck`s in the existing engine (`Packages/com.immrsiv.quest-setup/Editor/Setup/`), plus a small `FixAllRunner` that persists Fix All progress in `SessionState` and resumes after domain reloads (build-target switch, sample imports). Registry order becomes the ownership mechanism: Meta + Unity validation fixers run before our named checks so our checks own the final state (notably `RenderModeCheck` re-asserting Multi Pass on Standalone).

**Tech Stack:** Unity 6000.3 editor scripting; Meta XR SDK 205 `OVRProjectSetup` internals via reflection (pattern proven in `MetaProjectSetupCheck`); `Unity.XR.CoreUtils.Editor.BuildValidator` / OpenXR validation APIs (implementer verifies installed surface before coding, Task-5 style).

## Global Constraints

All v1.0 global constraints still bind (see `docs/superpowers/plans/2026-07-31-quest-setup-package.md` Global Constraints — namespace `Immrsiv.QuestSetup.Editor`, idempotent Fix ending in `SaveAssets`, plain-sentence commits with the `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` trailer, live-editor harness verification per `.superpowers/sdd/global-context.md`). Additionally for v1.1:

- **Registry order (verbatim from spec §D):** BuildTarget → ViveLayerKillSwitch → XRLoader → OpenXRFeatures → MetaProjectSetup → ProjectValidation → RenderMode → ColorSpace → AndroidPlayer → SamplesImported → TmpEssentials → BaselineVersions (12 checks).
- **Render-mode policy (final, non-negotiable):** Android = `SinglePassInstanced`; Standalone = `MultiPass`. Meta's Standalone stereo-instancing recommendation is suppressed via Meta's own per-task ignore store; the equivalent Unity-validation rule (if present) is excluded from our FixIt loop on Standalone. `RenderModeCheck` stays after both parity checks.
- **Manual-only Meta items** (Data Use Checkup, app ID / Android package name): excluded from counts, documented as "manual, see docs" in the READMEs. The package never sets a project-specific package name.
- **Optional validation rules** (`PoseControl → InputSystem.XR`, `Vector2Control → StickControl`): opt-in via two EditorPrefs-backed window toggles (`Immrsiv.QuestSetup.Optional.PoseControl`, `Immrsiv.QuestSetup.Optional.StickControl`), default OFF, excluded from Fix All and CLI unless toggled.
- **Headless CLI policy:** batch runs pass `-buildTarget Android` on the command line; `SetupCLI` never calls `SwitchActiveBuildTarget` in batch mode.
- Package version bumps to **1.1.0**; CHANGELOG entry dated 2026-08-02.
- **Template caveat:** if this template's active target is currently Standalone, running the new BuildTargetCheck Fix here triggers a full Android reimport (minutes, ~100 MB of samples). That is expected one-time template alignment; wait for the editor to settle (poll `Unity_GetConsoleLogs`) before continuing.

---

### Task 1: BuildTargetCheck + FixAllRunner resume infrastructure

**Files:**
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/BuildTargetCheck.cs`
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/FixAllRunner.cs`
- Modify: `Packages/com.immrsiv.quest-setup/Editor/Setup/SetupCheck.cs` (registry: insert `new BuildTargetCheck(),` FIRST)
- Modify: `Packages/com.immrsiv.quest-setup/Editor/Setup/QuestSetupWindow.cs` (Fix All button routes through `FixAllRunner.Start()`)
- Modify: `Packages/com.immrsiv.quest-setup/Editor/Setup/SetupCLI.cs` (FixAll: skip BuildTargetCheck fix in batch — its Evaluate failure message carries the `-buildTarget Android` instruction)

**Interfaces:**
- Produces: `class BuildTargetCheck : SetupCheck`; `static class FixAllRunner { static void Start(); }` — Start persists `SessionState` state (`Immrsiv.QuestSetup.FixAll.Pending` bool, `Immrsiv.QuestSetup.FixAll.Budget` int), runs one fix pass over failing checks in registry order, and an `[InitializeOnLoad]` continuation re-runs after every domain reload while Pending, decrementing Budget (start 6) and clearing Pending when no non-advisory check fails, when Budget hits 0, or when a pass makes no progress (failing count unchanged and no reload occurred). Window's Fix All calls `FixAllRunner.Start()`.

- [ ] **Step 1: Write BuildTargetCheck.cs**

```csharp
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
```

- [ ] **Step 2: Write FixAllRunner.cs**

```csharp
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
            // else: progress made; stay Pending for the post-reload continuation.
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
```

- [ ] **Step 3: Wire the window and CLI.** In `QuestSetupWindow.OnGUI`, replace the Fix All button's inline loop with `FixAllRunner.Start();` (keep the disabled-scope logic). In `SetupCLI.FixAll`, before fixing each check add: `if (check is BuildTargetCheck) continue; // batch: -buildTarget Android on the command line` (its Evaluate already prints the instruction via RunChecks logging).

- [ ] **Step 4: Registry** — insert `new BuildTargetCheck(),` as the FIRST entry.

- [ ] **Step 5: Verify in the template.** Run the harness: if the template's active target is already Android, expect the new check PASS and `failures: 0`. If it is Standalone, run the check's `Fix()` via a RunCommand reflection script, wait out the platform reimport (poll `Unity_GetConsoleLogs` until quiet, then re-run the harness) and expect PASS; the switch is committed template alignment. Then exercise the resume path once: run `FixAllRunner.Start()` via reflection — expect the `[QuestSetup] Fix All complete: all required checks pass.` log.

- [ ] **Step 6: Commit** (`git add Packages/com.immrsiv.quest-setup ProjectSettings` — ProjectSettings only if the switch changed it).

---

### Task 2: Meta XR Project Setup Tool parity (extend MetaProjectSetupCheck)

**Files:**
- Modify: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/MetaProjectSetupCheck.cs`

**Interfaces:**
- Consumes: existing reflection plumbing (GetTasks via reflection; Level via `OptionalLambdaType.GetValue(group)`; IsDone delegate via DynamicInvoke; public `FixAllAsync`).
- Produces: Evaluate counts ALL outstanding auto-fixable, non-ignored tasks (Required + Recommended) per group with per-level detail text; the Standalone stereo-instancing recommendation is programmatically ignored (Meta's own ignore store) with a documented reason.

- [ ] **Step 1: Investigate the installed API (required before coding).** In `Library/PackageCache/com.meta.xr.sdk.core@*/Editor/OVRProjectSetup/` read `Tasks/OVRConfigurationTask.cs` and neighbors to confirm, and record in your report with file:line:
  1. The **ignore** mechanism the tool's Ignore button uses (look for `IsIgnored`, `SetIgnored`, or an ignored-uid store in `OVRProjectSetupSettings`) — signature and persistence.
  2. How to detect a task **has an automatic fix** (a `FixAction`/`fix` delegate property — name and null semantics).
  3. A stable way to identify the Standalone stereo-instancing recommendation task (its `Uid`, message text, or the field it asserts) — capture the exact match key.
  4. Whether `FixAllAsync` skips ignored tasks and unfixable tasks (read `FixTasks` at ~line 563).

- [ ] **Step 2: Extend the check.** Keep the existing degradation contract ("…fix manually via Meta > Tools > Project Setup Tool."). Changes:
  - Evaluate: per group, count tasks where NOT done AND NOT ignored AND has an automatic fix; split counts by level. Details like `"Android: 0 outstanding; Standalone: 0 outstanding (1 policy-ignored, N manual)."` Manual (unfixable) tasks are counted separately and never fail the check; list their labels when nonzero so the window shows what "manual, see docs" refers to.
  - Before counting, ensure the policy ignore: if the Standalone stereo-instancing task (match key from Step 1.3) is not already ignored, ignore it via the API from Step 1.1 and log `"[QuestSetup] Ignoring Meta's Single Pass Instanced recommendation on Standalone: Multi Pass is required for correct two-eye rendering over Quest Link (see README)."` This runs in Evaluate (idempotent, cheap) so the suppression exists before any count.
  - Fix: unchanged public `FixAllAsync` per group (it must skip ignored tasks per Step 1.4 — if it does not, use the internal `FixTasks` overload with the filtered task list instead, via reflection).
  - Update the class summary comment to describe the parity scope and the policy ignore.

- [ ] **Step 3: Verify in the template.** Harness → the Meta check line must PASS with 0 outstanding on both groups and show the policy-ignored count. Also verify idempotency: run the harness twice; the ignore log line must appear at most once (already-ignored short-circuit).

- [ ] **Step 4: Commit.**

---

### Task 3: ProjectValidationCheck (Unity XR Project Validation parity)

**Files:**
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/ProjectValidationCheck.cs`
- Modify: `Packages/com.immrsiv.quest-setup/Editor/Setup/SetupCheck.cs` (registry: insert after `MetaProjectSetupCheck`, before `RenderModeCheck`)
- Modify: `Packages/com.immrsiv.quest-setup/Editor/Setup/QuestSetupWindow.cs` (two opt-in toggles, drawn under the checklist: "Optional input-system migrations")

**Interfaces:**
- Consumes: `Unity.XR.CoreUtils.Editor.BuildValidator` (and/or `UnityEditor.XR.OpenXR.OpenXRProjectValidation`) — implementer verifies the installed surface first.
- Produces: `class ProjectValidationCheck : SetupCheck`; `public static class OptionalRulePrefs { const string PoseControlKey = "Immrsiv.QuestSetup.Optional.PoseControl"; const string StickControlKey = "Immrsiv.QuestSetup.Optional.StickControl"; }` (booleans via `EditorPrefs`, default false) — the window reads/writes these keys.

- [ ] **Step 1: Diagnostic listing (required before coding).** Write a throwaway RunCommand script that, for BOTH `BuildTargetGroup.Standalone` and `BuildTargetGroup.Android`, enumerates the registered validation rules and logs for each: message, category, `FixItAutomatic`, error-vs-warning, and whether it currently fails. Verify in the process which API the Project Validation window actually aggregates in this project (`Unity.XR.CoreUtils.Editor.BuildValidator.GetCurrentValidationIssues(...)` — confirm exact signature in `Library/PackageCache/com.unity.xr.core-utils@*/Editor/BuildValidation/BuildValidator.cs`; check whether OpenXR/Meta feature rules flow through it or need `OpenXRProjectValidation.GetCurrentValidationIssues` additionally). Paste the full listing into your report — the exclusion match keys in Step 2 come from these REAL messages.

- [ ] **Step 2: Write the check.** Shape:
  - Evaluate (both groups): count failing rules that are fixable (`FixItAutomatic` true, `FixIt != null`) and not excluded. Exclusions: (a) the optional input-system rules — match by the message substrings `"PoseControl"` and `"Vector2Control"` (verify against Step 1's real messages) unless the corresponding `OptionalRulePrefs` toggle is on; (b) any Standalone rule asserting single-pass-instanced rendering (match key from Step 1's listing) — excluded with the render-mode policy reason. Details like `"Standalone: 0 fixable (1 policy-excluded, 2 optional off, 3 manual); Android: 0 fixable."`
  - Fix (both groups): invoke `FixIt()` for every failing, fixable, non-excluded rule, in listing order; log each rule fixed; `AssetDatabase.SaveAssets()` at the end. Rules whose FixIt triggers a reload are safe: FixAllRunner resumes.
  - Class summary comment documents the exclusion policy and that manual rules are surfaced, not fixed.
- [ ] **Step 3: Window toggles.** In `QuestSetupWindow.OnGUI`, after the scroll view and before the Fix All button, draw a small section: label "Optional input-system migrations (may break existing input code)", two `EditorGUILayout.ToggleLeft` bound to the `OptionalRulePrefs` EditorPrefs keys. Changing a toggle calls `RefreshAll()`.
- [ ] **Step 4: Verify in the template.** Harness → new check PASS with 0 fixable on both groups (run its Fix first if the template itself has outstanding fixable rules — that is template alignment; commit any resulting settings changes). Toggle one optional pref on via RunCommand, re-evaluate: check may go red listing the optional rule as now-fixable — toggle back off, confirm green (do not apply optional fixes to the template).
- [ ] **Step 5: Commit** (include any template settings changed by validation fixes).

---

### Task 4: Registry order, docs, version 1.1.0, template end-to-end

**Files:**
- Modify: `SetupCheck.cs` (final order per Global Constraints — 12 checks), `package.json` (version 1.1.0), `CHANGELOG.md`, root `README.md`, package `README.md`

**Interfaces:**
- Consumes: everything above; the real check Labels for the README table.

- [ ] **Step 1: Registry final order** (verify against Global Constraints verbatim; `RenderModeCheck` must sit AFTER `MetaProjectSetupCheck` and `ProjectValidationCheck`).
- [ ] **Step 2: Docs.**
  - Root README: checks table rebuilt from the real registry (12 rows, labels/coverage true to code); "usually twice" wording replaced with the resume behavior ("one Fix All — the window resumes itself across the build-target switch and sample-import reloads"); CI bullet gains `-buildTarget Android`; a new "Manual steps Meta requires" subsection (Data Use Checkup, app ID / package name) and a short "Render mode policy" note (Multi Pass on Standalone for Link, SPI on Android, Meta recommendation ignored by design).
  - Package README: same three updates in brief.
  - CHANGELOG: `## [1.1.0] - 2026-08-02` with the three features + policy notes.
  - package.json: `"version": "1.1.0"`.
- [ ] **Step 3: Template end-to-end.** Run `FixAllRunner.Start()` via reflection in the template; after it settles expect the `Fix All complete` log and harness `failures: 0` with 12 checks. Console sweep for errors.
- [ ] **Step 4: Commit.**

---

## Self-Review Notes

- Spec §A→Task 1, §B→Task 2, §C→Task 3, §D+§E→Task 4 (acceptance items that need a fresh consumer project + headset remain the human's manual matrix, restated at handoff).
- Investigation-checkpoint pattern (read installed source before coding, record file:line evidence) is deliberately reused from v1.0 Task 5, which proved it out.
- Known uncertainties, flagged in-task: Meta ignore-store API shape (T2 Step 1), validation aggregation API + real rule messages (T3 Step 1), template's current active build target (T1 Step 5 handles both cases).
