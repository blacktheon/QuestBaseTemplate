# Immrsiv Quest Setup

Turns any Unity 6 URP project into the Immrsiv Meta Quest baseline.

- **Install:** Package Manager → Install package from git URL → `https://github.com/blacktheon/QuestBaseTemplate.git?path=Packages/com.immrsiv.quest-setup`
- **Use:** the Quest Setup window opens on first install (or `Immrsiv → Quest Setup`). Click **Fix All** — one click is enough. The window resumes itself across the build-target switch and sample-import reloads.
- **What it enforces (12 checks):** active build target = Android; VIVE Hub OpenXR layer kill switch; OpenXR loaders on Standalone + Android; Meta XR feature group + features + Touch Controller on both platforms; Meta Project Setup Tool 0 outstanding Required/Recommended (manual items excluded — see below); Unity XR Project Validation 0 fixable issues on both targets; Multi Pass render mode on Standalone (Quest Link one-eye-rendering fix) and Single Pass Instanced on Android; Linear color space; Quest-ready Android player settings; both Immrsiv sample sets imported; TMP Essential Resources; blessed baseline versions.
- **Batch mode:** always pass `-buildTarget Android` to Unity. `-executeMethod Immrsiv.QuestSetup.Editor.SetupCLI.RunChecks` (exit 1 on failure) / `...SetupCLI.FixAll`. Run `SetupCLI.FixAll` then `SetupCLI.RunChecks` to verify — Meta Project Setup fixes now complete synchronously.
- **Manual steps Meta requires:** Data Use Checkup and app ID / Android package name are project-specific and must be set manually in the Meta Developer Dashboard and `Edit → Project Settings → Meta XR`.
- **Render mode policy:** Standalone = Multi Pass (non-negotiable Link fix; Meta's SPI recommendation is suppressed via Meta's own ignore mechanism); Android = Single Pass Instanced.
- **Dev (template only):** `Immrsiv → Quest Setup Dev → Sync Samples From This Project` refreshes `Samples~` from the template's `Assets` folders.

See the [repository README](../../README.md) for the full checklist and gotchas.
