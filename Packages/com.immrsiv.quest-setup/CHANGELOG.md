# Changelog

## [1.1.1] - 2026-08-02

### Fixed
- Samples-imported check is now version-agnostic: Unity stamps sample imports with the package version (`Assets/Samples/Immrsiv Quest Setup/<version>/...`), so after a package update the check stayed red forever and re-importing would have duplicated GUIDs against the old copy. A sample folder under any version now satisfies the check (with a details note naming the stale version and how to refresh); Fix only imports samples that exist under no version.

## [1.1.0] - 2026-08-02

### Added
- **BuildTargetCheck** (check 1): verifies the active build target is Android before any per-target fixes run. Fix All resumes itself across the domain reload triggered by the build-target switch — one click completes the full setup pipeline.
- **ProjectValidationCheck** (check 6): enumerates Unity XR Project Validation rules for both Standalone and Android, auto-fixes every fixable issue, skips render-mode-conflicting rules, and reports manual-only rules in details. Two optional input-system rules (PoseControl, Vector2Control) are exposed as opt-in toggles in the Quest Setup window (default OFF, excluded from Fix All and CLI).
- **MetaProjectSetupCheck** extended to cover both Required and Recommended tasks on both Standalone and Android targets. Tasks without an automatic fix (Data Use Checkup, app ID / package name) are excluded from the outstanding count and documented as manual.

### Changed
- Registry reordered to binding spec §D order: BuildTarget → ViveLayerKillSwitch → XRLoader → OpenXRFeatures → MetaProjectSetup → ProjectValidation → RenderMode → ColorSpace → AndroidPlayer → SamplesImported → TmpEssentials → BaselineVersions. RenderModeCheck now runs **after** Meta and Validation checks so the Standalone Multi Pass policy always wins.
- Fix All window behavior: the runner resumes itself across the build-target switch and sample-import domain reloads — one Fix All click completes the whole pipeline.

### Policy
- **Render mode:** Standalone = Multi Pass (Quest Link one-eye sample-shader fix — non-negotiable); Android = Single Pass Instanced (device, matches Meta recommendation). Meta's "Stereo Rendering Instancing" recommendation on Standalone is suppressed via Meta's own per-task ignore mechanism so Meta's tab shows 0 outstanding.
- **Meta manual items:** Data Use Checkup and app ID / Android package name are project-specific; the package never sets these. They are excluded from the auto-fix count.
- **Optional input rules:** PoseControl → InputSystem.XR and Vector2Control → StickControl are opt-in (default OFF) to avoid breaking existing input code.

## [1.0.0] - 2026-07-31

### Added
- Initial release: Quest Setup window with 9 checks, Fix All, batch CLI.
- Pinned baseline: Meta XR All-in-One SDK 205.0.0, OpenXR 1.17.0, URP 17.3.0, Input System 1.19.0, Unity 6000.3.
- Bundled samples: Interaction Example Scenes (with Immrsiv hand/button interaction fixes), Showcase Samples (Locomotion, Sliders and Handles, Throwing).
- VIVE Hub OpenXR implicit-layer kill switch (editor-process scope).
- Multi Pass render mode enforcement on Standalone (Quest Link one-eye-rendering fix).
- Advisory baseline-versions check (reports Unity/package drift from the blessed baseline; UPM dependencies enforce minimums, not exact pins).
