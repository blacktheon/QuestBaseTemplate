# Immrsiv Quest Base Template

**A complete, batteries-included VR development template for Meta Quest 3 — fork it and build, or import one package into your own project and let the setup window do the rest.**

![Unity](https://img.shields.io/badge/Unity-6000.3.19f1-black?logo=unity&logoColor=white)
![URP](https://img.shields.io/badge/URP-17.3.0-1a7fc1)
![Meta XR SDK](https://img.shields.io/badge/Meta%20XR%20SDK-205.0.0-0866ff)
![Platform](https://img.shields.io/badge/Target-Quest%203%20%C2%B7%20Quest%20Link-2ea44f)
![Internal](https://img.shields.io/badge/Use-Internal-orange)

This repository is two things at once:

1. **A ready-to-run Quest project** — clone it, open it in Unity, press Play with Quest Link.
2. **The home of `com.immrsiv.quest-setup`** — a UPM package that turns *any* Unity 6 URP project into the same fully-configured Quest setup with minimum clicks.

---

## 🚀 Two ways to start

### Option A — Fork the template (new projects)

```text
1. Fork or clone this repository
2. Open it with Unity 6000.3.19f1
3. Done. Every setting, package and sample is already in place.
```

### Option B — Import the package (existing Unity 6 URP projects)

```text
1. Package Manager → [+] → "Install package from git URL…"

   https://github.com/blacktheon/QuestBaseTemplate.git?path=Packages/com.immrsiv.quest-setup

2. The Quest Setup window opens automatically
   (or open it any time via  Immrsiv → Quest Setup)

3. Click  Fix All  — one Fix All is enough. The window resumes itself
   across the build-target switch and sample-import reloads.
```

The setup window audits your project against this template's reference configuration and shows exactly what differs. **Nothing changes without a click** (one exception: evaluating the Meta checklist registers our documented render-mode policy ignore in Meta's settings).

---

## 📦 What you get

### Packages, resolved automatically

| Package | Version | How it's installed |
|---|---|---|
| **Meta XR All-in-One SDK** | **205.0.0** | UPM dependency |
| OpenXR Plugin | 1.17.0 | UPM dependency |
| Universal Render Pipeline | 17.3.0 | UPM dependency |
| Input System | 1.19.0 | UPM dependency |

> UPM dependencies guarantee *at least* these versions. Check 12 (advisory) warns when a project has drifted from the blessed baseline.

Plus two curated sample sets, imported for you by the setup window:

| Sample | Contents |
|---|---|
| **Interaction Example Scenes** | Meta XR Interaction SDK example scenes with Immrsiv fixes (hand + button interaction) |
| **Showcase Samples** | Locomotion (climbing), Sliders and Handles, Throwing |

### Project configuration — the checks

| # | Check | Enforced state |
|---|---|---|
| 1 | Active build target: Android | Active build target must be Android; Fix switches it and the window resumes after the domain reload |
| 2 | Machine: VIVE OpenXR implicit layers disabled | VIVE Hub's implicit OpenXR layers disabled for the editor process (they break Quest Link XR init) |
| 3 | XR Plug-in Management: OpenXR enabled | OpenXR loader + initialize-on-startup for Standalone (Link) and Android (headset) |
| 4 | OpenXR: Meta XR feature group + features + Touch Controller | Meta XR feature group + Meta XR Feature + Touch Controller profile on both platforms |
| 5 | Meta Project Setup Tool: no outstanding Required/Recommended issues | All auto-fixable Required and Recommended items resolved for both Standalone and Android; manual items excluded (see below) |
| 6 | Unity XR Project Validation: no outstanding fixable issues | All fixable XR validation rules resolved for both Standalone and Android; manual-only rules reported in details |
| 7 | OpenXR render mode: Multi Pass on Standalone, Single Pass on Android | **Multi Pass on Standalone** (fixes Meta sample shaders rendering left-eye-only over Link); Single Pass Instanced on Android |
| 8 | Color space: Linear | Linear color space |
| 9 | Android player: minSdk 32, ARM64, IL2CPP, Vulkan only, ASTC, Landscape Left | minSdk 32 · ARM64 · IL2CPP · Vulkan only · ASTC textures · Landscape Left |
| 10 | Immrsiv Quest Setup samples imported | Both Immrsiv sample sets imported (with duplicate-GUID guard against Meta's own "Example Scenes") |
| 11 | TextMesh Pro essential resources imported | TMP Essential Resources imported *(advisory)* |
| 12 | Baseline versions: blessed Unity stream and package versions | Unity 6000.3 stream + blessed package versions *(advisory — reports drift, never auto-changes versions)* |

#### Manual steps Meta requires

The package deliberately does not set these project-specific values:

- **Data Use Checkup** — complete this in the Meta Developer Dashboard for your app.
- **App ID / Android package name** — set your app's Meta App ID and Unity package name (`Edit → Project Settings → Meta XR`). These will always show as outstanding in Meta's Project Setup Tool until you fill them in.

#### Render mode policy

| Platform | Mode | Reason |
|---|---|---|
| **Standalone (Quest Link)** | Multi Pass | Fixes Meta sample shaders rendering in the left eye only. Non-negotiable; Meta's "Stereo Rendering Instancing" recommendation is suppressed via Meta's own ignore mechanism so Meta's tab shows 0 outstanding. |
| **Android (headset)** | Single Pass Instanced | Device default; matches Meta recommendation. |

---

## 🗂 Repository layout

```text
├─ Assets/
│  ├─ Samples/…/Example Scenes/     Curated Meta interaction examples (source of the packaged sample)
│  ├─ ShowcaseSamples/              Locomotion · SlidersandHandles · Throwing (source of the packaged sample)
│  └─ Scenes/                       Template scenes (controller + hand tracking)
├─ Packages/
│  └─ com.immrsiv.quest-setup/      ★ The distributable package
│     ├─ Editor/Setup/              Quest Setup window + checks + batch CLI + sample sync tool
│     └─ Samples~/                  ExampleScenes · ShowcaseSamples (synced snapshots)
└─ docs/superpowers/                Design spec & implementation plan
```

---

## ⚠️ Notes & gotchas

- **Don't import the package samples inside this template project.** The originals live in `Assets/` (the samples check knows this and skips itself here). Importing copies would create duplicate-GUID conflicts.
- **Never import Meta's own "Example Scenes" sample in a consumer project** — the packaged "Interaction Example Scenes" are the same scenes (with fixes) under the same GUIDs. One or the other, not both.
- **Editor testing (Quest Link):** put the headset on with Link active, then press Play. If XR fails to initialize with VIVE Hub installed, that's what check #1 fixes.
- **Blessing a new Meta SDK:** update the versions in `package.json`, re-test this template, run `Immrsiv → Quest Setup Dev → Sync Samples From This Project`, update `CHANGELOG.md`, tag a release.
- **CI / automation:** every check is scriptable in batch mode — `SetupCLI.RunChecks` (exit 1 on failure) and `SetupCLI.FixAll`. Always pass `-buildTarget Android` on the Unity command line; the check fails loudly with that instruction if the batch target is wrong.
- **CI / batch mode:** Meta's Project Setup fixes now apply synchronously — run `SetupCLI.FixAll` then `SetupCLI.RunChecks` to verify.

## 📚 More documentation

- Package details: [Packages/com.immrsiv.quest-setup/README.md](Packages/com.immrsiv.quest-setup/README.md)
- Design spec: [docs/superpowers/specs/](docs/superpowers/specs/)
- Implementation plan: [docs/superpowers/plans/](docs/superpowers/plans/)

---

<p align="center"><sub>Maintained by <b>Eugene</b> · Immrsiv · internal use</sub></p>
