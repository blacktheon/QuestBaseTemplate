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
│   ├── Immrsiv.QuestSetup.Editor.asmdef     # editor-only; NO Meta/OpenXR references
│   ├── Core/
│   │   ├── BaselineVersions.cs   # single source of truth for pinned versions
│   │   ├── QuestSetupRuleAttribute.cs
│   │   ├── IQuestSetupRule.cs
│   │   ├── RuleRegistry.cs       # TypeCache-based discovery
│   │   ├── FixAllPipeline.cs     # phase state machine, domain-reload resume
│   │   └── PackageInstaller.cs   # PackageManager.Client wrapper
│   ├── Rules/                    # rules compilable WITHOUT the Meta SDK
│   │   ├── PinnedPackagesRule.cs
│   │   ├── AndroidBuildSettingsRules.cs
│   │   ├── GraphicsApiRule.cs
│   │   ├── ColorSpaceRule.cs
│   │   ├── SamplesImportedRules.cs
│   │   └── ViveLayerKillSwitch.cs   # also hosts the [InitializeOnLoad] env-var fix
│   ├── RulesMeta/
│   │   ├── Immrsiv.QuestSetup.Editor.MetaRules.asmdef
│   │   │     # references Meta/OpenXR asmdefs; uses versionDefines +
│   │   │     # defineConstraints so it only compiles once the SDK is installed
│   │   ├── XrLoaderRules.cs
│   │   ├── OpenXrFeatureRules.cs
│   │   ├── RenderModeRules.cs
│   │   └── MetaProjectSetupRule.cs  # delegates to OVRProjectSetup
│   └── Window/
│       ├── QuestSetupWindow.cs
│       └── QuestSetupWindow.uxml/.uss (UI Toolkit)
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

Note: `Meta XR Interaction SDK Essentials` (the large shared-asset sample both sets depend on) is **not** bundled — a Phase 3 rule imports it from Meta's own package via the Package Manager sample API, keeping canonical GUIDs and our repo small.

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

## 5. Rule engine

```csharp
public interface IQuestSetupRule
{
    string Id { get; }              // stable, e.g. "openxr.rendermode.standalone"
    string Category { get; }        // "Packages", "XR Settings", "Android", "Samples", "Machine"
    string Description { get; }
    RuleSeverity Severity { get; }  // Required | Recommended
    SetupPhase Phase { get; }       // Packages | Settings | Samples
    bool IsApplicable();            // e.g. skip Android rules if module absent
    RuleStatus Evaluate();          // Satisfied | Unsatisfied | Error(message)
    void Fix();                     // idempotent; may throw → surfaced as Error
}
```

- Rules are discovered with `TypeCache.GetTypesWithAttribute<QuestSetupRuleAttribute>()` — no manual registration; rules in the Meta asmdef simply appear once that assembly compiles.
- All `Fix()` implementations must be idempotent (safe to re-run; Fix All re-evaluates before fixing).
- Evaluation runs on window open/focus and after domain reloads; results are cached per compilation.

## 6. Fix All pipeline and domain-reload resilience

The hard part: installing packages and importing samples trigger recompiles/domain reloads, so Fix All cannot be a single method call.

- `FixAllPipeline` persists `{running, currentPhase}` in `SessionState`.
- An `[InitializeOnLoad]` hook checks that state after every domain reload and continues the pipeline.
- **Phase 1 — Packages:** diff `manifest.json` against `BaselineVersions`; apply via `Client.AddAndRemove` (single batched resolve). Unity recompiles; pipeline resumes.
- **Phase 2 — Settings:** run all Settings-phase rules in declaration order. The Meta-dependent rules now exist because the Meta asmdef compiled after Phase 1.
- **Phase 3 — Samples:** import both package samples via `UnityEditor.PackageManager.UI.Sample.FindByPackage(...)` / `Sample.Import()`; resume after the import's reload and re-evaluate everything.
- Pipeline halts (with the failing rule highlighted) if a Required rule's Fix throws; Recommended failures are logged and skipped.
- If the user closes the editor mid-pipeline, `SessionState` clears and the pipeline simply stops — re-running Fix All from the (partially green) checklist is always safe because rules are idempotent.

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
12. "Meta XR Interaction SDK Essentials" imported from Meta's own package (Required for the bundled samples to work, auto-fix via Package Manager sample API).
13. "Interaction Example Scenes" imported at current package version (Recommended, auto-fix).
14. "Showcase Samples" imported at current package version (Recommended, auto-fix).
15. TextMesh Pro essentials present (Recommended, auto-fix).

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

- **EditMode tests** (in package `Tests/`): baseline diff logic (manifest vs pinned list), rule registry discovery, pipeline state machine transitions (simulated, no real package installs).
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
