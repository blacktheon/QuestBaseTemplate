# Immrsiv Quest Setup

Turns any Unity 6 URP project into the Immrsiv Meta Quest baseline.

- **Install:** Package Manager → Install package from git URL → `https://github.com/blacktheon/QuestBaseTemplate.git?path=Packages/com.immrsiv.quest-setup`
- **Use:** the Quest Setup window opens on first install (or `Immrsiv → Quest Setup`). Click **Fix All** until every row is green — sample imports reload scripts in between, so it usually takes two clicks.
- **What it enforces:** pinned Meta XR SDK 205.0.0 + OpenXR 1.17.0 (UPM dependencies — minimum versions; an advisory check reports drift from the blessed baseline), OpenXR loaders and Meta features on both platforms, Multi Pass render mode on Standalone (Quest Link one-eye-rendering fix), Linear color space, Quest-ready Android player settings, the VIVE-Hub OpenXR layer kill switch, and imports the two curated sample sets.
- **Batch mode:** `-executeMethod Immrsiv.QuestSetup.Editor.SetupCLI.RunChecks` (exit 1 on failure) / `...SetupCLI.FixAll`. Run FixAll twice in batch mode — Meta Project Setup fixes apply asynchronously.
- **Dev (template only):** `Immrsiv → Quest Setup Dev → Sync Samples From This Project` refreshes `Samples~` from the template's `Assets` folders.

See the [repository README](../../README.md) for the full checklist and gotchas.
