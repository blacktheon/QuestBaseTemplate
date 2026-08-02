# Quest Setup Package — Design

**Date:** 2026-07-31
**Status:** Approved approach (self-contained UPM package, Approach 2); awaiting spec review
**Owner:** blacktheon

## 1. Overview

A team-internal UPM package (`com.immrsiv.quest-setup`, display name "Immrsiv Quest Setup") that converts any existing Unity 6 URP project into the team's blessed Meta Quest baseline. It provides:

- A **Quest Setup window** (`Tools > Immrsiv > Quest Setup`) showing a checklist of rules with per-item **Fix** buttons and a **Fix All** pipeline.
- **Pinned package installation** — the exact package versions the team has blessed.
- **Bundled samples** — the curated content currently maintained in this template project's `Assets/Samples` and `Assets/ShowcaseSamples`, shipped as UPM package samples.
- **Machine-quirk fixes** discovered in production (VIVE OpenXR layer conflict, editor render mode) baked in as rules.

### Goals

- Any existing Unity 6000.3 URP project reaches "builds and runs on Quest 3, plays in editor over Link" with one Fix All click plus the automatic recompiles between phases.
- Every team project lands on an identical, reproducible baseline (strict version pinning).
- Adding a newly discovered check is a ~20-line rule class.

### Non-goals

- VIVE or other vendor profiles (Quest-only; a future vendor would be a separate package).
- Scene scaffolding / building-block placement (Level C was explicitly descoped).
- Replacing Meta's Project Setup Tool — Meta-owned checks are delegated to it (see rule list).
- Runtime (player) code. The package is entirely editor-only.
- CI/automated distribution infrastructure (git URL + tags is the whole story for now).

## 2. Distribution and versioning

Mirrors the proven `VRBaseTemplate` / `com.immrsiv.vive-vrbase` convention: **the package is embedded in this template repository** at `Packages/com.immrsiv.quest-setup`. The repo is two things at once — a fork-and-go template project, and the host of the distributable package.

- Consumers install via Package Manager → *Install package from git URL* → `https://<host>/<org>/QuestBaseTemplate.git?path=Packages/com.immrsiv.quest-setup` (optionally `#vX.Y.Z`).
- Releases are annotated tags `vX.Y.Z`; `CHANGELOG.md` documents each blessed baseline.
- Updating a project = editing the tag in `Packages/manifest.json` and re-running Fix All.
- Because the package is embedded, developing it happens live inside this template project — no separate repo to sync.

**Blessing a new baseline** (e.g., a new Meta SDK): update `BaselineVersions`, re-sync samples from the template project, verify on a scratch project, tag a release.

## 3. Package layout

Embedded at `QuestBaseTemplate/Packages/com.immrsiv.quest-setup/`:

```
com.immrsiv.quest-setup/
├── package.json                  # name, version, unity 6000.3, samples[] declarations
├── CHANGELOG.md
├── README.md                     # install URL, usage, blessing procedure
├── Editor/
│   ├── Immrsiv.QuestSetup.Editor.asmdef     # editor-only; MAY reference Meta/OpenXR editor
│   │                                        # assemblies — package.json dependencies guarantee
│   │                                        # they are installed before this package compiles
│   └── Setup/
│       ├── SetupCheck.cs         # abstract base + SetupCheckRegistry (team pattern from com.immrsiv.vive-vrbase)
│       ├── QuestSetupWindow.cs   # IMGUI window + once-per-project auto-open (team pattern)
│       ├── SetupCLI.cs           # batch-mode RunChecks / FixAll for CI
│       ├── SampleSyncTool.cs     # dev-only: syncs template sample folders into Samples~
│       └── Checks/
│           ├── ViveLayerKillSwitchCheck.cs  # also hosts the [InitializeOnLoad] env-var fix
│           ├── XRLoaderCheck.cs
│           ├── OpenXRFeaturesCheck.cs
│           ├── RenderModeCheck.cs
│           ├── MetaProjectSetupCheck.cs     # delegates to OVRProjectSetup
│           ├── ColorSpaceCheck.cs
│           ├── AndroidPlayerCheck.cs
│           └── SamplesImportedCheck.cs
└── Samples~/
    ├── ExampleScenes/            # synced from Assets/Samples/<Meta XR Interaction SDK>/205.0.0/Example Scenes (~13 MB)
    └── ShowcaseSamples/          # synced from Assets/ShowcaseSamples (~88 MB)
```

### package.json samples declaration

```json
"samples": [
  { "displayName": "Interaction Example Scenes",
    "description": "Meta XR Interaction SDK example scenes (pinned SDK version).",
    "path": "Samples~/ExampleScenes" },
  { "displayName": "Showcase Samples",
    "description": "Immrsiv showcase scenes: Locomotion, Sliders and Handles, Throwing.",
    "path": "Samples~/ShowcaseSamples" }
]
```

Notes: (a) The interaction assets these samples depend on ship inside the `com.meta.xr.sdk.interaction` package itself (a package.json dependency) — no Meta sample import is required. (b) The bundled Example Scenes are the template's **modified** copies (hand/button interaction fixes) of Meta's "Example Scenes" sample from `com.meta.xr.sdk.interaction.ovr`, with original GUIDs — consumers must not additionally import Meta's own "Example Scenes" sample (the samples check warns if both are present).

## 4. Pinned baseline (initial values)

`BaselineVersions.cs` is the only place versions appear:

| Item | Pinned value |
|---|---|
| Unity | 6000.3 stream (warn if different minor) |
| `com.meta.xr.sdk.all` | 205.0.0 |
| `com.unity.xr.openxr` | 1.17.0 |
| `com.unity.render-pipelines.universal` | 17.3.0 |
| `com.unity.inputsystem` | 1.19.0 |

The Unity version check is a warning-severity rule (can't be auto-fixed); package versions are required-severity and auto-fixable.

## 5. Check engine (team pattern)

Follows the proven `com.immrsiv.vive-vrbase` pattern verbatim:

```csharp
public abstract class SetupCheck
{
    public abstract string Label { get; }
    public virtual bool IsAdvisory => false;   // advisory checks report but never fail Fix All/CI
    public abstract bool Evaluate(out string details);  // details = current vs expected state
    public abstract void Fix();                 // idempotent; may throw → surfaced inline
}

public static class SetupCheckRegistry
{
    public static List<SetupCheck> CreateAll() => new List<SetupCheck> { /* explicit ordered list */ };
}
```

- Explicit ordered registration (no attribute discovery) — order defines Fix All execution order.
- All `Fix()` implementations must be idempotent (safe to re-run; Fix All re-evaluates before fixing).
- Evaluation runs on window open/focus — the same "re-check when the window regains focus" model VRBase uses.

## 6. Dependencies and domain-reload handling (simplified)

The original design assumed a bootstrap pipeline to install the Meta SDK. Not needed: **`com.meta.xr.sdk.all` and all pinned Unity packages resolve from the default registry, so they are plain `package.json` dependencies** — UPM installs them *before* our package compiles. Consequences:

- One editor asmdef that references Meta/OpenXR editor assemblies directly. No version-define split, no `PackageManager.Client` bootstrap, no persisted pipeline state.
- The only operations that trigger domain reloads are sample imports (Phase-3-style checks). Handling matches VRBase's documented UX: **the user clicks Fix All, the editor reloads, they click Fix All again until every row is green** — the window re-evaluates on focus, and idempotent fixes make repeat clicks safe. The README documents "click Fix All until green (usually twice)".

## 7. Initial rule set

**Phase 1 — Packages** (`Packages` category)
1. All pinned packages present at exact versions (Required, auto-fix).
2. Unity editor version matches blessed stream (Warning, manual).

**Phase 2 — Settings**

*XR Settings category (Meta asmdef):*
3. OpenXR loader enabled for Standalone and Android; Initialize XR on Startup on (Required).
4. Meta XR feature group (`com.meta.openxr.featureset.metaxr`) enabled for both platforms; Meta XR Feature + Touch Controller Profile active (Required).
5. **OpenXR Render Mode: Standalone = Multi Pass, Android = default single-pass** (Required) — the fix for Meta sample shaders rendering left-eye-only over Link under URP + Single Pass Instanced.
6. Meta Project Setup Tool reports no outstanding *Required* items; Fix invokes `OVRProjectSetup`'s fix-all for required tasks (Required). This delegates all Meta-owned checks (manifest entries, quality settings, etc.) instead of duplicating them.

*Android category (no Meta dependency):*
7. IL2CPP + ARM64 only (Required).
8. Minimum Android API level 32, target API level = highest installed; ASTC texture compression (Required).
9. Graphics API = Vulkan only on Android (Required).
10. Linear color space (Required).

*Machine category:*
11. **VIVE OpenXR implicit-layer kill switch** (Required, editor-process-scoped). The package's always-compiled assembly contains the `[InitializeOnLoad]` class that sets the four `DISABLE_XR_APILAYER_VIVE_*_1` environment variables — the fix ships with the package itself, no per-project script. The rule reports whether VIVE Hub layers are registered on the machine (registry read) and confirms the kill switch is active; informational green when VIVE Hub isn't installed.

**Phase 3 — Samples**
12. "Interaction Example Scenes" imported at current package version; warns if Meta's own "Example Scenes" sample is also imported (GUID collision) (Recommended, auto-fix).
13. "Showcase Samples" imported at current package version (Recommended, auto-fix).
14. TextMesh Pro essentials present (Recommended, auto-fix).

Rule list is expected to grow; each new discovery (like this week's two) becomes one class in `Rules/` or `RulesMeta/`.

## 8. Window UX

- UI Toolkit `EditorWindow`, menu `Tools > Immrsiv > Quest Setup`.
- Header: package version, blessed baseline summary, **Fix All** button, Re-scan button.
- Body: rules grouped by Category; each row = status icon (green check / yellow warning / red cross / grey "waiting for SDK install"), description, Fix button when unsatisfied and auto-fixable.
- Meta-dependent rules before the SDK is installed appear as a single grey placeholder row ("6 checks become available after Meta SDK installs").
- Footer: collapsible log of actions taken this session (what Fix All changed, mirroring what we'd want in a code review).
- During the pipeline: progress bar with phase name; window survives domain reloads and re-binds.

## 9. Samples pipeline (template → package)

The template project (this repo) remains the authoring home of the curated content:

- `Assets/Samples/` — Meta XR Interaction SDK + Essentials imports (currently 205.0.0), possibly modified/fixed by the team.
- `Assets/ShowcaseSamples/` — Locomotion, SlidersandHandles, Throwing.

A **sync tool** ships in the package's editor assembly (menu visible only when the package is embedded): `Tools > Immrsiv > Quest Setup Dev > Sync Samples From This Project`. It:

1. Copies `Assets/Samples/<Meta XR Interaction SDK>/205.0.0/Example Scenes/*` → `Packages/com.immrsiv.quest-setup/Samples~/ExampleScenes/` and `Assets/ShowcaseSamples/*` → `Samples~/ShowcaseSamples/`, **including `.meta` files** (GUIDs preserved so cross-references survive).
2. Copies folder names byte-for-byte — the Meta folder name contains a zero-width character (`Meta XR Interaction ​SDK`), so names must never be hand-typed.
3. Reports total size and file count for the changelog.

**Decisions:**
- **GUIDs are preserved.** Consequence: a consumer must not import the same samples from Meta's own package UI *and* ours (GUID collisions). README documents: use our samples, they replace Meta's. Our imported path (`Assets/Samples/Immrsiv Quest Setup/...`) also differs from Meta's, making the double-import visible if it happens.
- **Plain git for now, no LFS.** The Interaction samples are tens-to-hundreds of MB; acceptable for an internal repo. If clone times hurt, migrate `Samples~/` to git-LFS in a later release (transparent to consumers).

## 10. Error handling

- Every `Fix()` runs in try/catch; exceptions mark the rule `Error` with the message shown inline — never a silent failure, never an aborted-half-fixed state without the checklist showing exactly where it stopped.
- `Client` (Package Manager) errors surface the raw resolve error text.
- All settings mutations go through `SerializedObject`/documented APIs where available (as validated interactively in this project) and call `AssetDatabase.SaveAssets()` so changes land on disk for version control.
- The window never blocks the editor; long operations are async with the pipeline state machine.

## 11. Testing

- **Batch CLI verification** (team pattern): `SetupCLI.RunChecks` / `SetupCLI.FixAll` batch-mode entry points, so every check is scriptable and CI-able, mirroring VRBase's `SetupCLI`.
- **Template self-verification:** in this template project all checks must evaluate green (it IS the reference configuration); run after every check lands.
- **Manual acceptance matrix** per release, on a scratch copy:
  1. Fresh Unity 6000.3 URP project → Fix All → all green → builds APK → runs on Quest 3 → plays over Link in editor with both eyes rendering.
  2. Already-configured project (this template) → window opens all green, Fix All is a no-op (idempotency check).
  3. Kill the editor mid-Phase-1 → reopen → checklist consistent, Fix All completes.
  4. Machine without VIVE Hub → Machine rule green/informational.
- Deliberately no automated on-device tests (out of scope for v1).

## 12. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Meta SDK internal APIs (`OVRProjectSetup`) change | We're pinned to one SDK version per release; API use is isolated in `MetaProjectSetupRule.cs` |
| Sample GUID collision with Meta's raw samples | Documented rule: our samples replace Meta's; distinct import path makes violations visible |
| Repo size growth from samples | Sync tool reports size; LFS migration path reserved |
| Zero-width chars / exotic folder names | Sync tool copies names programmatically |
| Env-var kill switch runs on machines without VIVE | Harmless no-op (variables reference layers that don't exist) |

## 13. Template repository README

The template repo gets a `README.md` in the exact format of `VRBaseTemplate/README.md` (the team's established style): title + one-line pitch, shields.io badges (Unity, URP, Meta SDK version, target platform, internal-use), "Two ways to start" (fork the template / install the package by git URL with `?path=`), "What you get" tables (packages, samples, the rule checklist), repository layout tree, notes & gotchas, links to package README and specs/plans, maintainer footer. Content adapted to Quest: Meta XR SDK 205, Quest 3 target, Quest Link editor testing, the VIVE-layer and Multi Pass gotchas.

## 14. Consumer walkthrough (acceptance narrative)

1. Developer has an existing URP project. Adds the package by git URL.
2. Window auto-opens on first install (one-time `SessionState` flag), shows mostly red.
3. Clicks **Fix All** → Phase 1 installs pinned packages (recompile) → Phase 2 flips all settings (including Multi Pass on Standalone and the VIVE kill switch activating) → Phase 3 imports both sample sets.
4. Checklist is green. Developer opens a showcase scene, presses Play with Quest Link — both eyes render, hands and controllers work. Builds an APK for device.
5. Total human interaction: add URL, one click, wait.

---

# v1.1 Amendment (2026-08-02) — install-test feedback round

A real install test (git URL into a fresh non-VR Unity 6000.3.19f1 URP project) surfaced three gaps. v1.1 adds three checks and resume infrastructure. Policy decisions below were delegated by the user ("decide policy" / "pick a per-target policy").

## A. Active build target check (new check, FIRST in registry)

- `BuildTargetCheck`: Evaluate = `EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android`; Fix = `EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android)`.
- Runs FIRST — later fixes are per-target and several tools evaluate against the active target.
- **Resume:** the switch triggers a domain reload. Fix All sets a `SessionState` flag before fixing; an `[InitializeOnLoad]` continuation (also listening to `EditorApplication.delayCall` after reload) reopens the window and re-runs Fix All for remaining failing checks until none remain or a fix fails. This replaces the pure "click Fix All again" UX for the switch case (which silently stopped).
- **Headless CLI policy:** batch runs must pass `-buildTarget Android` on the Unity command line (documented in both READMEs); `SetupCLI.FixAll` does NOT switch targets mid-batch — the check FAILs loudly with that instruction if the batch target is wrong.

## B. Meta XR Project Setup Tool parity (extend MetaProjectSetupCheck)

- Evaluate now counts ALL outstanding auto-fixable tasks (Required + Recommended) for BOTH Standalone and Android, not just Required; details shows the per-level count. Fix runs Meta's fix-all (the same API the tool's Fix All/Apply All buttons call) for both targets.
- **Manual-only tasks policy:** tasks with no automatic fix (Data Use Checkup, app ID / Android package name) are EXCLUDED from the count and listed in the README as "manual, see docs". The package never invents a project-specific package name.
- **Render-mode conflict policy (final):** Android = Single Pass Instanced (device, matches Meta recommendation); Standalone = Multi Pass (Quest Link one-eye sample-shader fix — non-negotiable). Meta's "Stereo Rendering Instancing" recommendation on Standalone is suppressed via Meta's own per-task ignore mechanism (persisted, applied programmatically by our check with a documented reason), so Meta's tab shows 0 outstanding. `RenderModeCheck` moves AFTER Meta/validation checks in registry order so our policy always wins the final word.

## C. Unity XR Project Validation parity (new check)

- `ProjectValidationCheck`: enumerates registered validation rules for BOTH build target groups (via `Unity.XR.CoreUtils.Editor.BuildValidator` / OpenXR's project validation API — implementer verifies the installed API surface first, as with Meta reflection) and invokes each failed rule's automatic FixIt — the programmatic Fix All equivalent.
- Skips: rules without an automatic fix (reported as manual), rules that would conflict with the render-mode policy on Standalone (excluded with documented reason), and **[Optional]-flagged input-system rules** (`PoseControl → InputSystem.XR`, `Vector2Control → StickControl`) which can break existing input code.
- Optional rules become two opt-in toggles in the Quest Setup window (EditorPrefs-backed, default OFF, excluded from Fix All and CLI).
- Dedupe/ownership: overlapping settings (aniso, solver iterations, pinch, API layers, hand tracking, D3D11, HDR, MSAA) converge because all involved fixers are idempotent and our named checks (RenderMode, AndroidPlayer, …) run LAST in registry order and own the final state of anything they assert.

## D. Registry order (v1.1)

BuildTarget → ViveLayerKillSwitch → XRLoader → OpenXRFeatures → MetaProjectSetup → ProjectValidation → RenderMode → ColorSpace → AndroidPlayer → SamplesImported → TmpEssentials → BaselineVersions (12 checks).

## E. Acceptance (v1.1)

Fresh non-VR Unity 6 URP project → install package → Fix All (window, with automatic resume across the build-target switch) and separately `SetupCLI.FixAll` with `-buildTarget Android`: active platform Android; Meta XR Project Setup Tool 0 outstanding fixable items on both tabs (policy-suppressed SPI item shows as ignored, not outstanding); Project Validation 0 fixable issues on both tabs (optional input rules visible only when toggled); every item a named green check in the Quest Setup window. Version bumps to 1.1.0 with CHANGELOG entry.
