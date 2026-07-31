# Immrsiv Quest Setup Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `com.immrsiv.quest-setup`, an embedded UPM package that turns any Unity 6 URP project into the team's blessed Meta Quest baseline via a checklist window, and give this template repo a VRBaseTemplate-style README.

**Architecture:** Mirrors the proven `com.immrsiv.vive-vrbase` pattern (in `C:\Work\Unity\DSTA\VRBaseTemplate\Packages\com.immrsiv.vive-vrbase` — read it when in doubt): a `SetupCheck` abstract base, one file per check, an explicit registry, an IMGUI window with per-row Fix + Fix All, batch CLI entry points. All pinned packages (including `com.meta.xr.sdk.all`) are plain `package.json` dependencies resolved by UPM before our code compiles — no bootstrap phase. Sample imports are the only domain-reload trigger; the UX is "click Fix All until green" (window re-evaluates on focus).

**Tech Stack:** Unity 6000.3 editor scripting (IMGUI, `UnityEditor.XR.Management`, `UnityEditor.XR.OpenXR.Features`, `UnityEditor.PackageManager.UI`), Meta XR SDK 205 (`OVRProjectSetup`), C# reflection for one internal Meta API.

## Global Constraints

- Package name `com.immrsiv.quest-setup`, display name `Immrsiv Quest Setup`, version `1.0.0`, unity `6000.3`. Company name **Immrsiv** everywhere; menus under `Immrsiv/` (matching VRBase's `[MenuItem("Immrsiv/VR Base Setup")]`).
- Namespace for all package code: `Immrsiv.QuestSetup.Editor`. Asmdef name `Immrsiv.QuestSetup.Editor`.
- Pinned dependency versions (verbatim, single source): `com.meta.xr.sdk.all` **205.0.0**, `com.unity.xr.openxr` **1.17.0**, `com.unity.render-pipelines.universal` **17.3.0**, `com.unity.inputsystem` **1.19.0**.
- Enforced settings: OpenXR loader on Standalone + Android; Meta feature set `com.meta.openxr.featureset.metaxr` both platforms; Render Mode **MultiPass on Standalone**, **SinglePassInstanced on Android**; Linear color space; Android = IL2CPP, ARM64 only, minSdk 32, Vulkan only, ASTC (Player + Build Settings), Landscape Left.
- VIVE kill-switch env vars (verbatim): `DISABLE_XR_APILAYER_VIVE_HAND_TRACKING_1`, `DISABLE_XR_APILAYER_VIVE_FACIAL_TRACKING_1`, `DISABLE_XR_APILAYER_VIVE_MR_1`, `DISABLE_XR_APILAYER_VIVE_XRTRACKER_1` — all set to `"1"`.
- **Never hand-type Meta's interaction-SDK sample folder name** — `Meta XR Interaction ​SDK` contains a zero-width character. Discover it by pattern (`Meta XR Interaction*SDK`) or enumerate directories.
- Every `Fix()` must be idempotent, end with `AssetDatabase.SaveAssets()` when it dirtied assets, and throw on failure (the window/CLI catch and surface).
- This template project IS the reference configuration: after each check task, that check must evaluate PASS here.
- Verification environment: the Unity editor for this project is open and reachable via the `Unity_RunCommand` MCP tool. The dynamic command assembly **cannot reference package types directly** — always go through reflection: `System.Type.GetType("Immrsiv.QuestSetup.Editor.SetupCLI, Immrsiv.QuestSetup.Editor")`.
- Commit after every task, from repo root `C:\Work\Unity\DSTA\QuestBaseTemplate`. Plain-sentence commit messages (repo style), ending with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

**Standard verification harness** (used by many steps below; referred to as "run the harness"):

```csharp
using UnityEngine;
using UnityEditor;
using System.Reflection;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh();
        var cli = System.Type.GetType("Immrsiv.QuestSetup.Editor.SetupCLI, Immrsiv.QuestSetup.Editor");
        if (cli == null) { result.LogError("SetupCLI type not found - package did not compile."); return; }
        var m = cli.GetMethod("RunChecksInternal", BindingFlags.Public | BindingFlags.Static);
        int failures = (int)m.Invoke(null, null);
        result.Log("RunChecksInternal failures: {0}", (object)failures);
    }
}
```

Each check's PASS/FAIL line appears in the console; fetch with `Unity_GetConsoleLogs` if needed.

---

### Task 1: Package skeleton (package.json, asmdef, SetupCheck base, registry, CLI)

**Files:**
- Create: `Packages/com.immrsiv.quest-setup/package.json`
- Create: `Packages/com.immrsiv.quest-setup/Editor/Immrsiv.QuestSetup.Editor.asmdef`
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/SetupCheck.cs`
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/SetupCLI.cs`

**Interfaces:**
- Produces: `abstract class SetupCheck { string Label {get;} bool IsAdvisory {get;} bool Evaluate(out string details); void Fix(); }` in namespace `Immrsiv.QuestSetup.Editor`; `SetupCheckRegistry.CreateAll()` returning `List<SetupCheck>`; `SetupCLI.RunChecksInternal()` returning `int` failure count, `SetupCLI.RunChecks()` / `SetupCLI.FixAll()` batch entries. All later tasks add constructors to `SetupCheckRegistry.CreateAll()`.

- [ ] **Step 1: Create package.json**

```json
{
  "name": "com.immrsiv.quest-setup",
  "version": "1.0.0",
  "displayName": "Immrsiv Quest Setup",
  "description": "Internal Quest base template: pinned Meta XR SDK dependencies, curated interaction/showcase samples, and a project setup window that configures any Unity 6 URP project for Meta Quest development.",
  "unity": "6000.3",
  "author": {
    "name": "Eugene",
    "url": "https://github.com/LDRSG/QuestBaseTemplate"
  },
  "dependencies": {
    "com.meta.xr.sdk.all": "205.0.0",
    "com.unity.xr.openxr": "1.17.0",
    "com.unity.render-pipelines.universal": "17.3.0",
    "com.unity.inputsystem": "1.19.0"
  }
}
```

(No `samples` entry yet — Task 7 adds it once `Samples~` exists. If the actual git host org/URL differs, keep the VRBase author-URL style but with the real URL — check `git remote -v`; if no remote, keep this value.)

- [ ] **Step 2: Create the asmdef**

`Packages/com.immrsiv.quest-setup/Editor/Immrsiv.QuestSetup.Editor.asmdef`:

```json
{
    "name": "Immrsiv.QuestSetup.Editor",
    "rootNamespace": "Immrsiv.QuestSetup.Editor",
    "references": [
        "Unity.XR.Management",
        "Unity.XR.Management.Editor",
        "Unity.XR.OpenXR",
        "Unity.XR.OpenXR.Editor",
        "Oculus.VR",
        "Oculus.VR.Editor",
        "Unity.TextMeshPro.Editor"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "autoReferenced": false
}
```

- [ ] **Step 3: Create SetupCheck.cs** (base + registry; registry list starts empty and is appended by Tasks 2–8)

```csharp
using System.Collections.Generic;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>A single project-configuration requirement with a one-click fix.</summary>
    public abstract class SetupCheck
    {
        public abstract string Label { get; }
        /// <summary>Advisory checks report but never fail Fix All / CI.</summary>
        public virtual bool IsAdvisory => false;
        /// <summary>True when the project already satisfies the requirement.
        /// details always describes current vs expected state.</summary>
        public abstract bool Evaluate(out string details);
        public abstract void Fix();
    }

    public static class SetupCheckRegistry
    {
        public static List<SetupCheck> CreateAll() => new List<SetupCheck>
        {
            // Order = Fix All execution order. Tasks 2-8 append here.
        };
    }
}
```

- [ ] **Step 4: Create SetupCLI.cs**

```csharp
using UnityEditor;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Batch entries: RunChecks exits 1 if any non-advisory check fails;
    /// FixAll fixes every failing non-advisory check then exits 0.
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
```

- [ ] **Step 5: Verify the package compiles and is embedded**

Run the harness via `Unity_RunCommand`. Expected: `RunChecksInternal failures: 0` (registry is empty). Then check `Unity_GetConsoleLogs` for zero compile errors. Also verify Unity recognizes the package:

```csharp
var info = UnityEditor.PackageManager.PackageInfo.FindForPackageName("com.immrsiv.quest-setup");
result.Log("source={0} version={1}", (object)info.source, (object)info.version);
```

Expected: `source=Embedded version=1.0.0`.

- [ ] **Step 6: Commit**

```powershell
git add Packages/com.immrsiv.quest-setup Packages/manifest.json Packages/packages-lock.json
git commit -m @'
Add com.immrsiv.quest-setup package skeleton: check engine and batch CLI

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 2: VIVE layer kill switch (check + always-on fix, replaces per-project script)

**Files:**
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/ViveLayerKillSwitchCheck.cs`
- Modify: `Packages/com.immrsiv.quest-setup/Editor/Setup/SetupCheck.cs` (registry: add `new ViveLayerKillSwitchCheck(),`)
- Delete: `Assets/Editor/DisableViveOpenXRLayers.cs` + `.meta` (superseded by the package)

**Interfaces:**
- Consumes: `SetupCheck`, `SetupCheckRegistry` from Task 1.
- Produces: `class ViveLayerKillSwitchCheck : SetupCheck` and `static class ViveLayerKillSwitch { static readonly string[] EnvVars; }`.

- [ ] **Step 1: Create ViveLayerKillSwitchCheck.cs**

```csharp
using System;
using System.Linq;
using UnityEditor;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>VIVE Hub / VIVE Business Streaming registers implicit OpenXR API layers
    /// (XR_APILAYER_VIVE_*) machine-wide. They make xrCreateInstance fail with
    /// XR_ERROR_EXTENSION_DEPENDENCY_NOT_ENABLED over Meta Quest Link, so XR never
    /// starts in Play Mode. Setting each layer's disable_environment variable in the
    /// editor process turns them off for this editor only. Harmless when VIVE Hub
    /// is not installed.</summary>
    [InitializeOnLoad]
    public static class ViveLayerKillSwitch
    {
        public static readonly string[] EnvVars =
        {
            "DISABLE_XR_APILAYER_VIVE_HAND_TRACKING_1",
            "DISABLE_XR_APILAYER_VIVE_FACIAL_TRACKING_1",
            "DISABLE_XR_APILAYER_VIVE_MR_1",
            "DISABLE_XR_APILAYER_VIVE_XRTRACKER_1",
        };

        static ViveLayerKillSwitch() => Apply();

        public static void Apply()
        {
            foreach (var v in EnvVars)
                Environment.SetEnvironmentVariable(v, "1");
        }

        public static bool ViveLayersRegisteredOnMachine()
        {
#if UNITY_EDITOR_WIN
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit");
            return key != null && key.GetValueNames().Any(n =>
                n.IndexOf("VIVE", StringComparison.OrdinalIgnoreCase) >= 0);
#else
            return false;
#endif
        }
    }

    public class ViveLayerKillSwitchCheck : SetupCheck
    {
        public override string Label => "Machine: VIVE OpenXR implicit layers disabled for this editor";

        public override bool Evaluate(out string details)
        {
            bool allSet = ViveLayerKillSwitch.EnvVars.All(v =>
                System.Environment.GetEnvironmentVariable(v) == "1");
            bool viveInstalled = ViveLayerKillSwitch.ViveLayersRegisteredOnMachine();
            details = !viveInstalled
                ? "No VIVE OpenXR layers registered on this machine (kill switch active anyway)."
                : allSet
                    ? "VIVE Hub layers present; kill-switch env vars active in this editor."
                    : "VIVE Hub layers present and kill switch NOT active.";
            return allSet;
        }

        public override void Fix() => ViveLayerKillSwitch.Apply();
    }
}
```

- [ ] **Step 2: Register the check** — in `SetupCheckRegistry.CreateAll()` the list becomes:

```csharp
        public static List<SetupCheck> CreateAll() => new List<SetupCheck>
        {
            // Order = Fix All execution order. Tasks 2-8 append here.
            new ViveLayerKillSwitchCheck(),
        };
```

- [ ] **Step 3: Delete the superseded project script**

Delete `Assets/Editor/DisableViveOpenXRLayers.cs` and `Assets/Editor/DisableViveOpenXRLayers.cs.meta` from disk (PowerShell `Remove-Item`, not the editor API — editor deletes prompt UI over MCP). If `Assets/Editor` is then empty, delete the folder and its `.meta` too.

- [ ] **Step 4: Verify**

Run the harness. Expected console line: `[QuestSetup] PASS | Machine: VIVE OpenXR implicit layers disabled for this editor | VIVE Hub layers present; kill-switch env vars active in this editor.` and `failures: 0`. (PASS even right after deleting the old script, because the package's `[InitializeOnLoad]` re-applied the env vars on the recompile.)

- [ ] **Step 5: Commit**

```powershell
git add -A Packages/com.immrsiv.quest-setup Assets/Editor
git commit -m @'
Add VIVE layer kill-switch check; the package now replaces the per-project script

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 3: XR loader check

**Files:**
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/XRLoaderCheck.cs`
- Modify: `Packages/com.immrsiv.quest-setup/Editor/Setup/SetupCheck.cs` (registry: append `new XRLoaderCheck(),`)

**Interfaces:**
- Consumes: `SetupCheck` base.
- Produces: `class XRLoaderCheck : SetupCheck`.

- [ ] **Step 1: Create XRLoaderCheck.cs** — direct port of VRBase's `XRLoaderCheck` (proven on Unity 6), plus `InitManagerOnStart`:

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Immrsiv.QuestSetup.Editor
{
    public class XRLoaderCheck : SetupCheck
    {
        const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        static readonly BuildTargetGroup[] Groups =
            { BuildTargetGroup.Standalone, BuildTargetGroup.Android };

        public override string Label => "XR Plug-in Management: OpenXR enabled (Standalone + Android)";

        public override bool Evaluate(out string details)
        {
            var missing = new List<string>();
            foreach (var group in Groups)
            {
                var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
                bool ok = settings != null && settings.Manager != null &&
                          settings.InitManagerOnStart &&
                          settings.Manager.activeLoaders.Any(l =>
                              l != null && l.GetType().FullName == OpenXRLoaderType);
                if (!ok) missing.Add(group.ToString());
            }
            details = missing.Count == 0
                ? "OpenXR loader active and initialize-on-startup on for Standalone and Android."
                : "OpenXR loader/init missing for: " + string.Join(", ", missing);
            return missing.Count == 0;
        }

        public override void Fix()
        {
            var perBuildTarget = GetOrCreatePerBuildTargetSettings();
            foreach (var group in Groups)
            {
                var settings = perBuildTarget.SettingsForBuildTarget(group);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                    settings.name = $"{group} Settings";
                    AssetDatabase.AddObjectToAsset(settings,
                        AssetDatabase.GetAssetPath(perBuildTarget));
                    perBuildTarget.SetSettingsForBuildTarget(group, settings);
                }
                if (settings.Manager == null)
                {
                    var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                    manager.name = $"{group} Providers";
                    AssetDatabase.AddObjectToAsset(manager,
                        AssetDatabase.GetAssetPath(perBuildTarget));
                    settings.AssignedSettings = manager;
                }
                XRPackageMetadataStore.AssignLoader(settings.Manager, OpenXRLoaderType, group);
                settings.InitManagerOnStart = true;
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
        }

        static XRGeneralSettingsPerBuildTarget GetOrCreatePerBuildTargetSettings()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                out XRGeneralSettingsPerBuildTarget perBuildTarget);
            if (perBuildTarget != null) return perBuildTarget;

            perBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            if (!AssetDatabase.IsValidFolder("Assets/XR"))
                AssetDatabase.CreateFolder("Assets", "XR");
            AssetDatabase.CreateAsset(perBuildTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,
                perBuildTarget, true);
            return perBuildTarget;
        }
    }
}
```

- [ ] **Step 2: Append `new XRLoaderCheck(),` to the registry list** (after `ViveLayerKillSwitchCheck`).

- [ ] **Step 3: Verify** — run the harness. Expected: the XR loader line is `PASS` (this template already has OpenXR on both platforms with init-on-start), `failures: 0`.

- [ ] **Step 4: Commit**

```powershell
git add Packages/com.immrsiv.quest-setup
git commit -m @'
Add XR loader check for OpenXR on Standalone and Android

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 4: OpenXR features check + render mode check

**Files:**
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/OpenXRFeaturesCheck.cs`
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/RenderModeCheck.cs`
- Modify: registry (append `new OpenXRFeaturesCheck(), new RenderModeCheck(),`)

**Interfaces:**
- Consumes: `SetupCheck` base.
- Produces: `class OpenXRFeaturesCheck : SetupCheck`, `class RenderModeCheck : SetupCheck`.

- [ ] **Step 1: Create OpenXRFeaturesCheck.cs** — adapted from VRBase's `OpenXRFeaturesCheck` with Meta identifiers:

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Immrsiv.QuestSetup.Editor
{
    public class OpenXRFeaturesCheck : SetupCheck
    {
        // Feature class names captured from this template project's OpenXR settings.
        static readonly Dictionary<BuildTargetGroup, string[]> Expected = new()
        {
            [BuildTargetGroup.Standalone] = new[] { "MetaXRFeature", "OculusTouchControllerProfile" },
            [BuildTargetGroup.Android] = new[] { "MetaXRFeature", "OculusTouchControllerProfile" },
        };

        // The "Meta XR" feature-group checkbox in XR Plug-in Management > OpenXR.
        const string MetaFeatureSetId = "com.meta.openxr.featureset.metaxr";

        public override string Label =>
            "OpenXR: Meta XR feature group + Meta XR Feature + Touch Controller profile (both platforms)";

        static IEnumerable<BuildTargetGroup> FeatureSetDisabled()
        {
            foreach (var group in Expected.Keys)
            {
                var featureSet = OpenXRFeatureSetManager.FeatureSetsForBuildTarget(group)
                    .FirstOrDefault(s => s.featureSetId == MetaFeatureSetId);
                if (featureSet != null && !featureSet.isEnabled)
                    yield return group;
            }
        }

        static IEnumerable<(BuildTargetGroup group, OpenXRFeature feature)> MissingFeatures()
        {
            foreach (var (group, names) in Expected.Select(kv => (kv.Key, kv.Value)))
            {
                FeatureHelpers.RefreshFeatures(group);
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (settings == null) continue;
                var features = settings.GetFeatures();
                foreach (var name in names)
                {
                    var feature = features.FirstOrDefault(f => f != null && f.GetType().Name == name);
                    if (feature != null && !feature.enabled)
                        yield return (group, feature);
                }
            }
        }

        public override bool Evaluate(out string details)
        {
            var problems = FeatureSetDisabled()
                .Select(g => $"{g}: Meta XR feature group unchecked")
                .Concat(MissingFeatures()
                    .Select(m => $"{m.group}:{m.feature.GetType().Name} disabled"))
                .ToList();
            details = problems.Count == 0
                ? "Meta XR feature group on; Meta XR Feature and Touch Controller profile enabled on both platforms."
                : string.Join(", ", problems);
            return problems.Count == 0;
        }

        public override void Fix()
        {
            foreach (var group in FeatureSetDisabled().ToList())
            {
                var featureSet = OpenXRFeatureSetManager.FeatureSetsForBuildTarget(group)
                    .First(s => s.featureSetId == MetaFeatureSetId);
                featureSet.isEnabled = true;
                OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
            }
            foreach (var (group, feature) in MissingFeatures().ToList())
            {
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
```

- [ ] **Step 2: Create RenderModeCheck.cs**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.XR.OpenXR;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Standalone (editor over Quest Link) must use Multi Pass: Meta's sample
    /// shaders (hands, Interaction SDK) are legacy CGPROGRAM shaders that render into
    /// the LEFT EYE ONLY under URP + Single Pass Instanced. Android keeps the efficient
    /// single-pass (multiview) mode where those same shaders work on device.</summary>
    public class RenderModeCheck : SetupCheck
    {
        static readonly Dictionary<BuildTargetGroup, OpenXRSettings.RenderMode> Expected = new()
        {
            [BuildTargetGroup.Standalone] = OpenXRSettings.RenderMode.MultiPass,
            [BuildTargetGroup.Android] = OpenXRSettings.RenderMode.SinglePassInstanced,
        };

        public override string Label =>
            "OpenXR render mode: Multi Pass on Standalone (Link one-eye fix), Single Pass on Android";

        static IEnumerable<(BuildTargetGroup group, OpenXRSettings settings)> Wrong()
        {
            foreach (var (group, mode) in Expected.Select(kv => (kv.Key, kv.Value)))
            {
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (settings != null && settings.renderMode != mode)
                    yield return (group, settings);
            }
        }

        public override bool Evaluate(out string details)
        {
            var wrong = Wrong().Select(w =>
                $"{w.group}: {w.settings.renderMode} (want {Expected[w.group]})").ToList();
            details = wrong.Count == 0
                ? "Standalone Multi Pass, Android Single Pass Instanced."
                : string.Join(", ", wrong);
            return wrong.Count == 0;
        }

        public override void Fix()
        {
            foreach (var (group, settings) in Wrong().ToList())
            {
                settings.renderMode = Expected[group];
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
```

- [ ] **Step 3: Append `new OpenXRFeaturesCheck(),` and `new RenderModeCheck(),` to the registry.**

- [ ] **Step 4: Verify** — run the harness. Expected: both new lines `PASS` (this template has the Meta feature set on and Standalone already Multi Pass from the earlier fix), `failures: 0`. If `OpenXRFeaturesCheck` FAILs because a feature name differs, list actual names first (`OpenXRSettings.GetSettingsForBuildTargetGroup(g).GetFeatures()` → log `GetType().Name`) via `Unity_RunCommand`, correct the `Expected` table, re-verify.

- [ ] **Step 5: Commit**

```powershell
git add Packages/com.immrsiv.quest-setup
git commit -m @'
Add OpenXR feature-set and render-mode checks (Multi Pass on Standalone)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 5: Meta Project Setup Tool delegation check

**Files:**
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/MetaProjectSetupCheck.cs`
- Modify: registry (append `new MetaProjectSetupCheck(),`)

**Interfaces:**
- Consumes: `SetupCheck` base; Meta's `OVRProjectSetup` (public `static Task FixAllAsync(BuildTargetGroup)`; internal `static IEnumerable<OVRConfigurationTask> GetTasks(BuildTargetGroup)` — reflection).
- Produces: `class MetaProjectSetupCheck : SetupCheck`.

- [ ] **Step 1: Read the installed API before coding.** Open `Library/PackageCache/com.meta.xr.sdk.core@*/Editor/OVRProjectSetup/OVRProjectSetup.cs` and confirm: `FixAllAsync` signature at ~line 558, `GetTasks(BuildTargetGroup)` at ~line 310, and on `OVRConfigurationTask` (same folder) the members used below (`IsDone(BuildTargetGroup)`, `Level` returning an enum with a `Required` value — the file `OVRProjectSetupTask.cs` or similar defines it; adjust the two member-name strings in Step 2 if they differ).

- [ ] **Step 2: Create MetaProjectSetupCheck.cs**

```csharp
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using Meta.XR.Editor; // if OVRProjectSetup is global-namespace (it is in SDK 205), delete this line

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Delegates all Meta-owned project checks (manifest entries, quality,
    /// permissions...) to Meta's own Project Setup Tool instead of duplicating them.
    /// Evaluate reads outstanding Required tasks via reflection (GetTasks is internal);
    /// Fix calls the public OVRProjectSetup.FixAllAsync per platform.</summary>
    public class MetaProjectSetupCheck : SetupCheck
    {
        static readonly BuildTargetGroup[] Groups =
            { BuildTargetGroup.Android, BuildTargetGroup.Standalone };

        public override string Label => "Meta Project Setup Tool: no outstanding Required issues";

        static Type SetupType => Type.GetType("OVRProjectSetup, Oculus.VR.Editor");

        public override bool Evaluate(out string details)
        {
            var setupType = SetupType;
            if (setupType == null)
            {
                details = "OVRProjectSetup type not found (Meta SDK API moved) - fix manually via Meta > Tools > Project Setup Tool.";
                return false;
            }
            var getTasks = setupType.GetMethod("GetTasks",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (getTasks == null)
            {
                details = "OVRProjectSetup.GetTasks not found (Meta SDK API moved) - fix manually via Meta > Tools > Project Setup Tool.";
                return false;
            }

            int outstanding = 0;
            foreach (var group in Groups)
            {
                var tasks = ((IEnumerable)getTasks.Invoke(null, new object[] { group })).Cast<object>();
                foreach (var task in tasks)
                {
                    var t = task.GetType();
                    var level = t.GetProperty("Level")?.GetValue(task)?.ToString()
                             ?? t.GetMethod("GetLevel")?.Invoke(task, new object[] { group })?.ToString();
                    var isDone = t.GetMethod("IsDone")?.Invoke(task, new object[] { group });
                    if (level == "Required" && isDone is bool done && !done)
                        outstanding++;
                }
            }
            details = outstanding == 0
                ? "Meta Project Setup Tool reports no outstanding Required tasks."
                : $"{outstanding} Required task(s) outstanding - Fix runs Meta's Fix All.";
            return outstanding == 0;
        }

        public override void Fix()
        {
            var fixAll = SetupType?.GetMethod("FixAllAsync",
                BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(BuildTargetGroup) }, null);
            if (fixAll == null)
                throw new InvalidOperationException(
                    "OVRProjectSetup.FixAllAsync not found - run Meta > Tools > Project Setup Tool manually.");
            foreach (var group in Groups)
                fixAll.Invoke(null, new object[] { group });
            // Async: results land over the next frames; the window re-evaluates on focus.
        }
    }
}
```

(If Step 1 showed `OVRProjectSetup` lives in the global namespace — expected for SDK 205 — remove the `using Meta.XR.Editor;` line; the reflection lookup by assembly-qualified name is namespace-agnostic either way. If `Level` is exposed differently, adjust only the property/method names in Evaluate.)

- [ ] **Step 3: Append `new MetaProjectSetupCheck(),` to the registry.**

- [ ] **Step 4: Verify** — run the harness. Expected: `PASS | Meta Project Setup Tool: no outstanding Required issues` (the user already ran Meta's tool on this template). If it FAILs with reflection messages, return to Step 1 and align member names with the installed SDK source.

- [ ] **Step 5: Commit**

```powershell
git add Packages/com.immrsiv.quest-setup
git commit -m @'
Delegate Meta-owned checks to the Meta Project Setup Tool via one check

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 6: Color space + Android player checks

**Files:**
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/ColorSpaceCheck.cs`
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/AndroidPlayerCheck.cs`
- Modify: registry (append `new ColorSpaceCheck(), new AndroidPlayerCheck(),`)
- Possibly modify: `ProjectSettings/ProjectSettings.asset` (if the template itself needs fixing — commit that too)

**Interfaces:**
- Consumes: `SetupCheck` base.
- Produces: `class ColorSpaceCheck : SetupCheck`, `class AndroidPlayerCheck : SetupCheck`.

- [ ] **Step 1: Create ColorSpaceCheck.cs**

```csharp
using UnityEditor;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    public class ColorSpaceCheck : SetupCheck
    {
        public override string Label => "Color space: Linear";

        public override bool Evaluate(out string details)
        {
            details = $"Color space is {PlayerSettings.colorSpace} (want Linear).";
            return PlayerSettings.colorSpace == ColorSpace.Linear;
        }

        public override void Fix()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.SaveAssets();
        }
    }
}
```

- [ ] **Step 2: Create AndroidPlayerCheck.cs** — VRBase's `AndroidPlayerCheck` adapted for Quest 3: **minSdk 32** (not 29) and **Vulkan only** (not GLES3):

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Immrsiv.QuestSetup.Editor
{
    public class AndroidPlayerCheck : SetupCheck
    {
        public override string Label =>
            "Android player: minSdk 32, ARM64, IL2CPP, Vulkan only, ASTC, Landscape Left";

        public override bool Evaluate(out string details)
        {
            var issues = new List<string>();
            if ((int)PlayerSettings.Android.minSdkVersion < 32)
                issues.Add($"minSdk {(int)PlayerSettings.Android.minSdkVersion} (want 32)");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                issues.Add($"architectures {PlayerSettings.Android.targetArchitectures} (want ARM64)");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                issues.Add("scripting backend not IL2CPP");
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) ||
                apis.Length != 1 || apis[0] != GraphicsDeviceType.Vulkan)
                issues.Add("graphics APIs not exactly [Vulkan]");
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.LandscapeLeft)
                issues.Add($"orientation {PlayerSettings.defaultInterfaceOrientation} (want LandscapeLeft)");
            if (EditorUserBuildSettings.androidBuildSubtarget != MobileTextureSubtarget.ASTC)
                issues.Add($"Build Settings texture compression {EditorUserBuildSettings.androidBuildSubtarget} (want ASTC)");

            bool astcOk = false;
            try
            {
                var formats = PlayerSettings.Android.textureCompressionFormats;
                astcOk = formats.Length == 1 && formats[0] == TextureCompressionFormat.ASTC;
            }
            catch
            {
                var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").First();
                var prop = new SerializedObject(asset)
                    .FindProperty("m_BuildTargetDefaultTextureCompressionFormat"); // value 3 = ASTC
                astcOk = prop != null && prop.intValue == 3;
            }
            if (!astcOk) issues.Add("Player texture compression not exactly [ASTC]");

            details = issues.Count == 0 ? "All Android player settings match."
                                        : "Wrong: " + string.Join("; ", issues);
            return issues.Count == 0;
        }

        public override void Fix()
        {
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            try
            {
                PlayerSettings.Android.textureCompressionFormats =
                    new[] { TextureCompressionFormat.ASTC };
            }
            catch
            {
                var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").First();
                var so = new SerializedObject(asset);
                var prop = so.FindProperty("m_BuildTargetDefaultTextureCompressionFormat");
                if (prop != null) { prop.intValue = 3; so.ApplyModifiedProperties(); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
```

- [ ] **Step 3: Append `new ColorSpaceCheck(),` and `new AndroidPlayerCheck(),` to the registry.**

- [ ] **Step 4: Verify — and align the template if needed.** Run the harness. If either new check FAILs **in this template**, that is the template being off-baseline: invoke the failing check's `Fix()` here (reflection: instantiate the check type, call `Fix()`), re-run the harness until `failures: 0`, and include the resulting `ProjectSettings` changes in the commit. (Likely candidates: minSdk and texture compression, which Meta's tool sets differently.)

- [ ] **Step 5: Commit**

```powershell
git add Packages/com.immrsiv.quest-setup ProjectSettings
git commit -m @'
Add color-space and Android player checks; align template to the baseline

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 7: Sample sync tool + populate Samples~ + declare package samples

**Files:**
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/SampleSyncTool.cs`
- Create (generated): `Packages/com.immrsiv.quest-setup/Samples~/ExampleScenes/**` and `Packages/com.immrsiv.quest-setup/Samples~/ShowcaseSamples/**`
- Modify: `Packages/com.immrsiv.quest-setup/package.json` (add `samples`)

**Interfaces:**
- Consumes: template folders `Assets/Samples/<Meta XR Interaction*SDK>/205.0.0/Example Scenes` (name matched by pattern — zero-width char) and `Assets/ShowcaseSamples`.
- Produces: menu `Immrsiv/Quest Setup Dev/Sync Samples From This Project`; populated `Samples~`; package sample entries `Interaction Example Scenes` and `Showcase Samples` (display names used by Task 8's check).

- [ ] **Step 1: Create SampleSyncTool.cs**

```csharp
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Dev-only (menu appears only while the package is embedded in the
    /// template project): snapshots the template's curated sample folders into
    /// Samples~ for distribution. Copies .meta files so GUIDs survive; never
    /// hand-types the Meta folder name (it contains a zero-width character).</summary>
    public static class SampleSyncTool
    {
        const string MenuPath = "Immrsiv/Quest Setup Dev/Sync Samples From This Project";

        [MenuItem(MenuPath, validate = true)]
        static bool Validate() =>
            PackageInfo.FindForPackageName("com.immrsiv.quest-setup")?.source == PackageSource.Embedded;

        [MenuItem(MenuPath)]
        public static void Sync()
        {
            var pkg = PackageInfo.FindForPackageName("com.immrsiv.quest-setup");
            var samplesRoot = Path.Combine(pkg.resolvedPath, "Samples~");

            var sdkDir = Directory.GetDirectories("Assets/Samples")
                .FirstOrDefault(d => Path.GetFileName(d).StartsWith("Meta XR Interaction") &&
                                     !Path.GetFileName(d).Contains("Essentials"));
            if (sdkDir == null) { Debug.LogError("[SampleSync] Interaction SDK sample folder not found under Assets/Samples."); return; }
            var exampleScenes = Directory.GetDirectories(sdkDir).Select(v => Path.Combine(v, "Example Scenes"))
                .FirstOrDefault(Directory.Exists);
            if (exampleScenes == null) { Debug.LogError("[SampleSync] 'Example Scenes' not found in " + sdkDir); return; }

            CopyTree(exampleScenes, Path.Combine(samplesRoot, "ExampleScenes"));
            CopyTree("Assets/ShowcaseSamples", Path.Combine(samplesRoot, "ShowcaseSamples"));
            Debug.Log("[SampleSync] done.");
        }

        static void CopyTree(string source, string dest)
        {
            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            Directory.CreateDirectory(dest);
            int files = 0;
            long bytes = 0;
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(source, file);
                var target = Path.Combine(dest, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target);
                files++;
                bytes += new FileInfo(file).Length;
            }
            Debug.Log($"[SampleSync] {source} -> {dest}: {files} files, {bytes / (1024 * 1024)} MB");
        }
    }
}
```

- [ ] **Step 2: Run the sync** via `Unity_RunCommand` (reflection):

```csharp
var t = System.Type.GetType("Immrsiv.QuestSetup.Editor.SampleSyncTool, Immrsiv.QuestSetup.Editor");
t.GetMethod("Sync").Invoke(null, null);
```

Expected console: two `[SampleSync] ... files, ... MB` lines (ExampleScenes ≈ 17 files / ~13 MB + metas; ShowcaseSamples ≈ ~88 MB) and `[SampleSync] done.`

- [ ] **Step 3: Add the samples block to package.json** (after `"dependencies"`):

```json
  "samples": [
    {
      "displayName": "Interaction Example Scenes",
      "description": "Meta XR Interaction SDK example scenes with Immrsiv fixes (hand + button interaction). Do NOT also import Meta's own 'Example Scenes' sample - same GUIDs.",
      "path": "Samples~/ExampleScenes"
    },
    {
      "displayName": "Showcase Samples",
      "description": "Immrsiv showcase scenes: Locomotion, Sliders and Handles, Throwing.",
      "path": "Samples~/ShowcaseSamples"
    }
  ]
```

- [ ] **Step 4: Verify Unity sees the samples** via `Unity_RunCommand`:

```csharp
UnityEditor.AssetDatabase.Refresh();
foreach (var s in UnityEditor.PackageManager.UI.Sample.FindByPackage("com.immrsiv.quest-setup", null))
    result.Log("sample: {0} imported={1}", (object)s.displayName, (object)s.isImported);
```

Expected: exactly two lines — `Interaction Example Scenes` and `Showcase Samples`, both `imported=False` (the template holds the originals in `Assets/`, not sample imports — that's correct).

- [ ] **Step 5: Commit** (large — samples payload)

```powershell
git add Packages/com.immrsiv.quest-setup
git commit -m @'
Bundle curated Example Scenes and Showcase Samples as package samples with sync tool

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 8: Samples-imported check + TMP essentials check

**Files:**
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/SamplesImportedCheck.cs`
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/TmpEssentialsCheck.cs`
- Modify: registry (append `new SamplesImportedCheck(), new TmpEssentialsCheck(),`)

**Interfaces:**
- Consumes: `SetupCheck` base; sample display names from Task 7 (`Interaction Example Scenes`, `Showcase Samples`).
- Produces: `class SamplesImportedCheck : SetupCheck`, `class TmpEssentialsCheck : SetupCheck`.

- [ ] **Step 1: Create SamplesImportedCheck.cs**

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Imports this package's two bundled sample sets in consumer projects.
    /// In the template itself (package embedded) the originals live directly in
    /// Assets/, so the check auto-passes - importing here would duplicate GUIDs
    /// (same protection VRBase documents for its settings sample).</summary>
    public class SamplesImportedCheck : SetupCheck
    {
        const string PackageName = "com.immrsiv.quest-setup";
        static readonly string[] SampleNames = { "Interaction Example Scenes", "Showcase Samples" };

        public override string Label => "Immrsiv Quest Setup samples imported";

        static bool IsEmbeddedTemplate() =>
            PackageInfo.FindForPackageName(PackageName)?.source == PackageSource.Embedded;

        static List<Sample> Missing() =>
            Sample.FindByPackage(PackageName, null)
                .Where(s => SampleNames.Contains(s.displayName) && !s.isImported)
                .ToList();

        static bool MetaExampleScenesAlsoImported()
        {
            // Our ExampleScenes bundle keeps Meta's GUIDs; importing Meta's own
            // "Example Scenes" sample alongside creates GUID conflicts.
            if (!Directory.Exists("Assets/Samples")) return false;
            return Directory.GetDirectories("Assets/Samples").Any(d =>
                Path.GetFileName(d).StartsWith("Meta XR Interaction") &&
                !Path.GetFileName(d).Contains("Essentials"));
        }

        public override bool Evaluate(out string details)
        {
            if (IsEmbeddedTemplate())
            {
                details = "Template project: sample originals live in Assets/ (import skipped by design).";
                return true;
            }
            var missing = Missing();
            var warn = MetaExampleScenesAlsoImported()
                ? " WARNING: Meta's own 'Example Scenes' sample is imported - remove it or our copy to avoid GUID conflicts."
                : "";
            details = (missing.Count == 0
                ? "Both Immrsiv sample sets imported."
                : "Missing: " + string.Join(", ", missing.Select(s => s.displayName))) + warn;
            return missing.Count == 0;
        }

        public override void Fix()
        {
            foreach (var sample in Missing())
            {
                UnityEngine.Debug.Log($"[QuestSetup] importing sample '{sample.displayName}'");
                sample.Import();
            }
        }
    }
}
```

- [ ] **Step 2: Create TmpEssentialsCheck.cs**

```csharp
using System.IO;
using UnityEditor;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>The interaction samples use TextMesh Pro UI; consumers need the TMP
    /// essential resources imported once per project.</summary>
    public class TmpEssentialsCheck : SetupCheck
    {
        const string EssentialsFolder = "Assets/TextMesh Pro/Resources";

        public override string Label => "TextMesh Pro essential resources imported";
        public override bool IsAdvisory => true;

        public override bool Evaluate(out string details)
        {
            bool ok = Directory.Exists(EssentialsFolder);
            details = ok ? "TMP essentials present."
                         : "TMP essentials missing (sample UI text will not render).";
            return ok;
        }

        public override void Fix()
        {
            TMPro.TMP_PackageResourceImporter.ImportResources(
                importEssentials: true, importExtras: false, interactive: false);
            AssetDatabase.SaveAssets();
        }
    }
}
```

(If `TMPro.TMP_PackageResourceImporter` fails to resolve, the ugui 2.0 asmdef name differs — check `Library/PackageCache/com.unity.ugui@*/Editor/TMP/` for the asmdef name and importer class, and update the asmdef reference from Task 1 accordingly.)

- [ ] **Step 3: Append `new SamplesImportedCheck(),` and `new TmpEssentialsCheck(),` to the registry.**

- [ ] **Step 4: Verify** — run the harness. Expected: `PASS | Immrsiv Quest Setup samples imported | Template project: sample originals live in Assets/ (import skipped by design).`, `PASS`/`WARN` for TMP (template has `Assets/TextMesh Pro` → PASS), `failures: 0`.

- [ ] **Step 5: Commit**

```powershell
git add Packages/com.immrsiv.quest-setup
git commit -m @'
Add samples-imported and TMP essentials checks with template-embed protection

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 9: Setup window + auto-open

**Files:**
- Create: `Packages/com.immrsiv.quest-setup/Editor/Setup/QuestSetupWindow.cs`

**Interfaces:**
- Consumes: `SetupCheckRegistry.CreateAll()`, `SetupCheck` (Task 1).
- Produces: menu `Immrsiv/Quest Setup`; `QuestSetupWindow.Open()`; once-per-project auto-open.

- [ ] **Step 1: Create QuestSetupWindow.cs** — port of `VRBaseSetupWindow` (window + `SetupKeys` + auto-open) with renames; no interactive-only skip (we have no build-target check):

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    public class QuestSetupWindow : EditorWindow
    {
        List<SetupCheck> _checks;
        readonly Dictionary<SetupCheck, (bool ok, string details)> _results =
            new Dictionary<SetupCheck, (bool, string)>();
        readonly Dictionary<SetupCheck, string> _fixErrors =
            new Dictionary<SetupCheck, string>();
        Vector2 _scroll;

        [MenuItem("Immrsiv/Quest Setup")]
        public static void Open() => GetWindow<QuestSetupWindow>("Quest Setup");

        void OnEnable()
        {
            _checks = SetupCheckRegistry.CreateAll();
            RefreshAll();
        }

        void OnFocus()
        {
            // Checks re-evaluate only here and after fixes, not on every repaint -
            // some of them touch the asset database.
            if (_checks != null) RefreshAll();
            Repaint();
        }

        void RefreshAll()
        {
            _results.Clear();
            int failing = 0;
            foreach (var check in _checks)
            {
                bool ok;
                string details;
                try { ok = check.Evaluate(out details); }
                catch (System.Exception e) { ok = false; details = e.Message; }
                _results[check] = (ok, details);
                if (!ok && !check.IsAdvisory) failing++;
                if (ok) _fixErrors.Remove(check);
            }
            EditorPrefs.SetBool(SetupKeys.CompleteKey, failing == 0);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Immrsiv Quest Setup - project requirements for Meta Quest. Nothing changes without a click.\n" +
                "Sample imports reload scripts: keep clicking Fix All until every row is green (usually twice).",
                MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int failing = 0;
            foreach (var check in _checks)
            {
                var (ok, details) = _results.TryGetValue(check, out var r) ? r : (false, "Not evaluated.");
                if (!ok && !check.IsAdvisory) failing++;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                var icon = ok ? "TestPassed" : check.IsAdvisory ? "console.warnicon" : "TestFailed";
                GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(20));
                EditorGUILayout.BeginVertical();
                GUILayout.Label(check.Label, EditorStyles.boldLabel);
                GUILayout.Label(details, EditorStyles.wordWrappedMiniLabel);
                if (_fixErrors.TryGetValue(check, out var fixError))
                    EditorGUILayout.HelpBox($"Fix failed: {fixError}", MessageType.Error);
                EditorGUILayout.EndVertical();
                using (new EditorGUI.DisabledScope(ok))
                {
                    if (GUILayout.Button("Fix", GUILayout.Width(60)))
                    {
                        RunFix(check);
                        RefreshAll();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(failing == 0))
            {
                if (GUILayout.Button("Fix All", GUILayout.Height(28)))
                {
                    foreach (var check in _checks)
                    {
                        if (_results.TryGetValue(check, out var r) && r.ok) continue;
                        RunFix(check);
                    }
                    RefreshAll();
                }
            }
            if (failing == 0)
                EditorGUILayout.HelpBox("All required checks pass. You are ready to build for Quest.", MessageType.Info);
        }

        void RunFix(SetupCheck check)
        {
            try
            {
                check.Fix();
                _fixErrors.Remove(check);
            }
            catch (System.Exception e)
            {
                _fixErrors[check] = e.Message;
                Debug.LogError($"[Quest Setup] Fix failed for '{check.Label}': {e}");
            }
        }
    }

    /// <summary>Per-project EditorPrefs keys for the setup window.</summary>
    static class SetupKeys
    {
        static string ProjectKey =>
            Application.dataPath.Replace('/', '_').Replace('\\', '_').Replace(':', '_');

        /// <summary>Set once the window has auto-opened for this project.</summary>
        public static string ShownKey => "Immrsiv.QuestSetup.SetupShown." + ProjectKey;

        /// <summary>True while every non-advisory check passes (refreshed on evaluate).</summary>
        public static string CompleteKey => "Immrsiv.QuestSetup.SetupComplete." + ProjectKey;
    }

    /// <summary>Opens the setup window once per project after the package lands.</summary>
    [InitializeOnLoad]
    static class SetupWindowAutoOpen
    {
        static SetupWindowAutoOpen()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(SetupKeys.ShownKey, false)) return;
                EditorPrefs.SetBool(SetupKeys.ShownKey, true);
                QuestSetupWindow.Open();
            };
        }
    }
}
```

- [ ] **Step 2: Verify** via `Unity_RunCommand` (reflection):

```csharp
var t = System.Type.GetType("Immrsiv.QuestSetup.Editor.QuestSetupWindow, Immrsiv.QuestSetup.Editor");
t.GetMethod("Open", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(null, null);
```

Expected: no exception; console clean. Then run the harness once more: `failures: 0`.

- [ ] **Step 3: Commit**

```powershell
git add Packages/com.immrsiv.quest-setup
git commit -m @'
Add Quest Setup window with per-check Fix, Fix All and once-per-project auto-open

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 10: READMEs + CHANGELOG

**Files:**
- Create: `README.md` (repo root)
- Create: `Packages/com.immrsiv.quest-setup/README.md`
- Create: `Packages/com.immrsiv.quest-setup/CHANGELOG.md`

**Interfaces:**
- Consumes: check labels from Tasks 2–8 (the checklist table must match `SetupCheckRegistry` order and labels — read `SetupCheck.cs` registry before writing); install URL style from `C:\Work\Unity\DSTA\VRBaseTemplate\README.md`.

- [ ] **Step 1: Determine the real install URL.** Run `git remote -v` in the repo. If a remote exists, the install URL is `<remote-url>?path=Packages/com.immrsiv.quest-setup`. If none exists, use `https://github.com/LDRSG/QuestBaseTemplate.git?path=Packages/com.immrsiv.quest-setup` (the VRBase org) and note in the commit message that the URL assumes the repo will be pushed there.

- [ ] **Step 2: Write the root README.md** — same structure as `C:\Work\Unity\DSTA\VRBaseTemplate\README.md` (open it side-by-side; keep its section order, badge style, tone, and footer). Full content:

````markdown
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

   <INSTALL-URL-FROM-STEP-1>

2. The Quest Setup window opens automatically
   (or open it any time via  Immrsiv → Quest Setup)

3. Click  Fix All  until every row is green (usually twice — sample
   imports reload scripts in between)
```

The setup window audits your project against this template's reference configuration and shows exactly what differs. **Nothing changes without a click.**

---

## 📦 What you get

### Packages, resolved automatically

| Package | Version | How it's installed |
|---|---|---|
| **Meta XR All-in-One SDK** | **205.0.0** | UPM dependency |
| OpenXR Plugin | 1.17.0 | UPM dependency |
| Universal Render Pipeline | 17.3.0 | UPM dependency |
| Input System | 1.19.0 | UPM dependency |

Plus two curated sample sets, imported for you by the setup window:

| Sample | Contents |
|---|---|
| **Interaction Example Scenes** | Meta XR Interaction SDK example scenes with Immrsiv fixes (hand + button interaction) |
| **Showcase Samples** | Locomotion (climbing), Sliders and Handles, Throwing |

### Project configuration — the checks

| ✅ | Check | Enforced state |
|---|---|---|
| 1 | VIVE layer kill switch | VIVE Hub's implicit OpenXR layers disabled for the editor process (they break Quest Link XR init) |
| 2 | XR Plug-in Management | OpenXR loader + initialize-on-startup for Standalone (Link) and Android (headset) |
| 3 | OpenXR features | Meta XR feature group + Meta XR Feature + Touch Controller profile on both platforms |
| 4 | Render mode | **Multi Pass on Standalone** (fixes Meta sample shaders rendering in the left eye only over Link) · Single Pass Instanced on Android |
| 5 | Meta Project Setup Tool | No outstanding Required issues (Meta's own checklist, run for you) |
| 6 | Color space | Linear |
| 7 | Android player | minSdk 32 · ARM64 · IL2CPP · Vulkan only · ASTC textures · Landscape Left |
| 8 | Samples | Both Immrsiv sample sets imported (with duplicate-GUID guard against Meta's own "Example Scenes") |
| 9 | TextMesh Pro | Essential resources imported *(advisory)* |

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
- **CI / automation:** every check is scriptable in batch mode — `SetupCLI.RunChecks` (exit 1 on failure) and `SetupCLI.FixAll`.

## 📚 More documentation

- Package details: [Packages/com.immrsiv.quest-setup/README.md](Packages/com.immrsiv.quest-setup/README.md)
- Design spec: [docs/superpowers/specs/](docs/superpowers/specs/)
- Implementation plan: [docs/superpowers/plans/](docs/superpowers/plans/)

---

<p align="center"><sub>Maintained by <b>Eugene</b> · Immrsiv · internal use</sub></p>
````

Replace `<INSTALL-URL-FROM-STEP-1>` with the real URL. Before saving, re-read the registry in `SetupCheck.cs` and make the checks table rows match its actual order and coverage.

- [ ] **Step 3: Write the package README.md** (`Packages/com.immrsiv.quest-setup/README.md`):

```markdown
# Immrsiv Quest Setup

Turns any Unity 6 URP project into the Immrsiv Meta Quest baseline.

- **Install:** Package Manager → Install package from git URL → `<INSTALL-URL-FROM-STEP-1>`
- **Use:** the Quest Setup window opens on first install (or `Immrsiv → Quest Setup`). Click **Fix All** until every row is green — sample imports reload scripts in between, so it usually takes two clicks.
- **What it enforces:** pinned Meta XR SDK 205.0.0 + OpenXR 1.17.0 (UPM dependencies), OpenXR loaders and Meta features on both platforms, Multi Pass render mode on Standalone (Quest Link one-eye-rendering fix), Linear color space, Quest-ready Android player settings, the VIVE-Hub OpenXR layer kill switch, and imports the two curated sample sets.
- **Batch mode:** `-executeMethod Immrsiv.QuestSetup.Editor.SetupCLI.RunChecks` (exit 1 on failure) / `...SetupCLI.FixAll`.
- **Dev (template only):** `Immrsiv → Quest Setup Dev → Sync Samples From This Project` refreshes `Samples~` from the template's `Assets` folders.

See the [repository README](../../README.md) for the full checklist and gotchas.
```

- [ ] **Step 4: Write CHANGELOG.md** (`Packages/com.immrsiv.quest-setup/CHANGELOG.md`):

```markdown
# Changelog

## [1.0.0] - 2026-07-31

### Added
- Initial release: Quest Setup window with 9 checks, Fix All, batch CLI.
- Pinned baseline: Meta XR All-in-One SDK 205.0.0, OpenXR 1.17.0, URP 17.3.0, Input System 1.19.0, Unity 6000.3.
- Bundled samples: Interaction Example Scenes (with Immrsiv hand/button interaction fixes), Showcase Samples (Locomotion, Sliders and Handles, Throwing).
- VIVE Hub OpenXR implicit-layer kill switch (editor-process scope).
- Multi Pass render mode enforcement on Standalone (Quest Link one-eye-rendering fix).
```

- [ ] **Step 5: Verify** — render check: view `README.md` raw for broken table pipes/code fences (each `|` row has equal columns; all fences closed). Confirm every link target exists (`Packages/com.immrsiv.quest-setup/README.md`, `docs/superpowers/specs/`, `docs/superpowers/plans/`).

- [ ] **Step 6: Commit**

```powershell
git add README.md Packages/com.immrsiv.quest-setup/README.md Packages/com.immrsiv.quest-setup/CHANGELOG.md
git commit -m @'
Add template and package READMEs plus changelog

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
'@
```

---

### Task 11: Final verification and release readiness

**Files:**
- No new files; possibly small fixes discovered here.

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: Full template verification** — run the harness. Expected: every check `PASS` (TMP may be `WARN` only if `Assets/TextMesh Pro/Resources` is absent — it isn't), `failures: 0`.

- [ ] **Step 2: Console sweep** — `Unity_GetConsoleLogs` with `logTypes: "Error,Warning"`. Expected: no errors originating from `Immrsiv.QuestSetup` code. (Meta SDK noise like `XR_ERROR_ACTIONSET_NOT_ATTACHED` haptics warnings is pre-existing and out of scope.)

- [ ] **Step 3: Window smoke test** — open `Immrsiv → Quest Setup` via reflection (Task 9 Step 2 code). Expected: opens without exceptions; bottom help box shows "All required checks pass."

- [ ] **Step 4: Git hygiene** — `git status`: no unexpected untracked files under `Packages/`; `git log --oneline -12` shows one commit per task.

- [ ] **Step 5: Commit any final fixes** (only if Steps 1–4 surfaced changes), same message style.

- [ ] **Step 6: Report the manual acceptance matrix for the user** (cannot be automated — requires headset + a scratch project). Present as the final summary:
  1. Create a fresh Unity 6000.3 URP project → install the package by git URL → window auto-opens → Fix All (×2) → all green.
  2. Open an imported showcase scene → press Play with Quest Link → renders in **both eyes**, hands + controllers work.
  3. Build an APK → runs on Quest 3.
  4. This template project: open `Immrsiv → Quest Setup` → all green, Fix All disabled (no-op).

---

## Self-Review Notes

- **Spec coverage:** distribution/embedding (Task 1), kill switch + machine rule (Task 2), XR loader (3), feature set + render mode (4), Meta tool delegation (5), color space + Android (6), samples bundling + sync tool + zero-width-char handling (7), sample import + GUID-collision guard + TMP (8), window + auto-open + "click twice" UX (9), README format + package README + CHANGELOG + blessing procedure (10), template self-verification + manual matrix (11). Spec's ~~pipeline/state machine~~ was superseded in the spec itself (section 6) — not planned.
- **Known uncertainty, flagged in-task:** exact `OVRConfigurationTask` member names (Task 5 Step 1 verifies against installed source before coding); TMP importer asmdef name under ugui 2.0 (Task 8 Step 2 note); real git remote URL (Task 10 Step 1).
- **Type consistency:** `SetupCheck`/`SetupCheckRegistry`/`SetupCLI.RunChecksInternal` names match across Tasks 1–9; sample display names in Task 7 package.json match Task 8's `SampleNames` and Task 10's README tables.
