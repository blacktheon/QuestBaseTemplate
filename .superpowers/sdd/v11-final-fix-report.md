# v1.1 Final Fix Wave — Report

Branch: `quest-setup-v1.1`

---

## Item 1 — Window help text

**File:** `Packages/com.immrsiv.quest-setup/Editor/Setup/QuestSetupWindow.cs` (~line 52–55)

Replaced the stale second sentence:
> "Sample imports reload scripts: keep clicking Fix All until every row is green (usually twice)."

With:
> "Click Fix All once - the window resumes itself across the build-target switch and sample-import reloads."

Also appended the "one exception" softener to "Nothing changes without a click":
> "(one exception: evaluating the Meta checklist registers our documented render-mode policy ignore in Meta's settings)"

---

## Item 2 — Meta fixes complete synchronously

**File:** `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/MetaProjectSetupCheck.cs`

### FixTasks signature found

File: `Library/PackageCache/com.meta.xr.sdk.core@c0efcbf2ba70/Editor/OVRProjectSetup/OVRProjectSetup.cs` **line 563**

```
internal static void FixTasks(
    BuildTargetGroup buildTargetGroup,
    Func<IEnumerable<OVRConfigurationTask>, List<OVRConfigurationTask>> filter = null,
    LogMessages logMessages = LogMessages.Disabled,   // int 0
    bool blocking = true,
    Action<OVRConfigurationTaskProcessor> onCompleted = null)
```

### Blocking semantics

`OVRConfigurationTaskProcessorQueue.cs` lines 83–94: when `processor.Blocking == true`, `Enqueue()` calls `Update()` immediately (synchronous pump) rather than deferring to `EditorApplication.update`. This guarantees fixes complete before `Fix()` returns.

### Change made

Replaced the `FixAllAsync` invocation with a reflection call to `OVRProjectSetup.FixTasks` (`BindingFlags.Static | BindingFlags.NonPublic`) passing `blocking: true` for both groups. The `LogMessages` enum (nested type on `OVRProjectSetup`, value `Disabled=0`) is resolved via `GetNestedType` with fallback to `Enum.ToObject(logMessagesType, 0)` or raw `int 0`. Degradation contract preserved: if reflection fails, throws `InvalidOperationException("...fix manually via Meta > Tools > Project Setup Tool")`.

Updated class summary comment to reflect the synchronous blocking path.

### Verification evidence

```
RunCommand: MetaProjectSetupCheck.Fix() completed without exception - FixTasks MethodInfo lookup: FOUND
```

Fix() is callable without throwing. With 0 outstanding tasks FixTasks is a no-op, proving the reflection path succeeds end-to-end.

---

## Item 3 — FixAllRunner stranded-Pending fix

**File:** `Packages/com.immrsiv.quest-setup/Editor/Setup/FixAllRunner.cs`

In `Continue()`'s progress branch (the `else: progress made` path), added explicit continuation scheduling instead of relying on domain reload:

```csharp
else if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
    EditorApplication.delayCall += Continue;
// (if compiling/updating, the [InitializeOnLoad] continuation takes over after the reload)
```

Budget decrement already bounds re-entry.

### Verification evidence

```
RunCommand: FixAllRunner.Start() completed. SessionState Pending = False
RunCommand: Item 3 verification: Pending=false after Start() on already-passing project.
```

`Start()` → `Continue()` → `failingBefore == 0` → `Finish()` sets `Pending=false` synchronously. No stranded state.

---

## Item 4 — OptionalRulePrefs keys per-project

**File:** `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/ProjectValidationCheck.cs`
**File:** `Packages/com.immrsiv.quest-setup/Editor/Setup/QuestSetupWindow.cs`

Extracted the per-project suffix pattern (`Application.dataPath` with `/`, `\`, `:` replaced by `_`) into `OptionalRulePrefs.ProjectKey` (internal static property). Changed `PoseControlKey` and `StickControlKey` from `public const string` to `public static string` properties that append `"." + ProjectKey`. Updated `SetupKeys.ShownKey` to delegate to `OptionalRulePrefs.ProjectKey` — single implementation, no duplication.

No migration needed (keys unreleased).

### Verification evidence

```
PoseControlKey:  Immrsiv.QuestSetup.Optional.PoseControl.C__Work_Unity_DSTA_QuestBaseTemplate_Assets
StickControlKey: Immrsiv.QuestSetup.Optional.StickControl.C__Work_Unity_DSTA_QuestBaseTemplate_Assets
Project suffix present in PoseControlKey: True
```

---

## Item 5 — Docs truth-ups

### Root README (`README.md`)

- CI batch bullet: replaced "run `SetupCLI.FixAll` twice (or `FixAll` then `RunChecks`)" with "run `SetupCLI.FixAll` then `SetupCLI.RunChecks` to verify".
- "Nothing changes without a click." → "Nothing changes without a click (one exception: evaluating the Meta checklist registers our documented render-mode policy ignore in Meta's settings)."

### Package README (`Packages/com.immrsiv.quest-setup/README.md`)

- Batch mode bullet: replaced "Run FixAll twice in batch mode — Meta Project Setup fixes apply asynchronously." with "Run `SetupCLI.FixAll` then `SetupCLI.RunChecks` to verify — Meta Project Setup fixes now complete synchronously."

---

## Harness result

**12 checks, failures: 0, console clean.**

```
[QuestSetup] PASS | Active build target: Android | Active build target is Android.
[QuestSetup] PASS | Machine: VIVE OpenXR implicit layers disabled for this editor | VIVE Hub layers present; kill-switch env vars active in this editor.
[QuestSetup] PASS | XR Plug-in Management: OpenXR enabled (Standalone + Android) | OpenXR loader active and initialize-on-startup on for Standalone and Android.
[QuestSetup] PASS | OpenXR: Meta XR feature group + Meta XR Feature + Touch Controller profile (both platforms) | Meta XR feature group on; Meta XR Feature and Touch Controller profile enabled on both platforms.
[QuestSetup] PASS | Meta Project Setup Tool: no outstanding Required/Recommended issues | Android: 0 outstanding; Standalone: 0 outstanding (1 policy-ignored).
[QuestSetup] PASS | Unity XR Project Validation: no outstanding fixable issues | Standalone: 0 fixable (2 manual); Android: 0 fixable (2 manual).
[QuestSetup] PASS | OpenXR render mode: Multi Pass on Standalone (Link one-eye fix), Single Pass on Android | Standalone Multi Pass, Android Single Pass Instanced.
[QuestSetup] PASS | Color space: Linear | Color space is Linear (want Linear).
[QuestSetup] PASS | Android player: minSdk 32, ARM64, IL2CPP, Vulkan only, ASTC, Landscape Left | All Android player settings match.
[QuestSetup] PASS | Immrsiv Quest Setup samples imported | Template project: sample originals live in Assets/ (import skipped by design).
[QuestSetup] PASS | TextMesh Pro essential resources imported | TMP essentials present.
[QuestSetup] PASS | Baseline versions: blessed Unity stream and package versions | Unity stream and all baseline package versions match the blessed baseline.
```

**Meta check PASS details:** `"Android: 0 outstanding; Standalone: 0 outstanding (1 policy-ignored)."`
