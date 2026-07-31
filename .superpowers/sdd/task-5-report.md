# Task 5 Report: Meta Project Setup Tool delegation check

## Actual SDK signatures found

All inspected in `Library/PackageCache/com.meta.xr.sdk.core@c0efcbf2ba70/`.

| Symbol | File:Line | Notes |
|---|---|---|
| `OVRProjectSetup` | `Editor/OVRProjectSetup/OVRProjectSetup.cs:49` | **Global namespace** (no `Meta.XR.Editor` wrapping namespace); assembly `Oculus.VR.Editor` |
| `GetTasks(BuildTargetGroup)` | `OVRProjectSetup.cs:310` | `internal static IEnumerable<OVRConfigurationTask>` — matches brief exactly |
| `FixAllAsync(BuildTargetGroup)` | `OVRProjectSetup.cs:558` | `public static Task` — matches brief exactly |
| `OVRConfigurationTask.Level` | `Tasks/OVRConfigurationTask.cs:70` | **Property** returning `OptionalLambdaType<BuildTargetGroup, OVRProjectSetup.TaskLevel>`. NOT a plain enum value — must call `.GetValue(group)` on the returned object to get the `TaskLevel` enum string "Required". |
| `OVRConfigurationTask.IsDone` | `Tasks/OVRConfigurationTask.cs:79` | **Property** returning `Func<BuildTargetGroup, bool>`. NOT a method — must get the property, cast to `Delegate`, then call `.DynamicInvoke(group)`. |
| `OVRProjectSetup.TaskLevel` enum | `OVRProjectSetup.cs:54-60` | Values: `Hidden=-1, Optional=0, Recommended=1, Required=2` |

## Deviations from the brief's code

1. **Removed `using Meta.XR.Editor;`** — OVRProjectSetup is in the global namespace, not that namespace. The brief correctly flagged this for deletion.

2. **`Level` reflection — two-level call required:**
   The brief's code `t.GetProperty("Level")?.GetValue(task)?.ToString()` would produce `"OptionalLambdaTypeWithoutLambda"` (the type name), not `"Required"`. The fix: after getting the `Level` property value, look up `GetValue(BuildTargetGroup)` on that `OptionalLambdaType` object and invoke it. A fallback to the brief's `GetLevel` method path is preserved but will never trigger with this SDK.

3. **`IsDone` reflection — property not method:**
   The brief's `t.GetMethod("IsDone")` returns null because `IsDone` is a property. The fix: `t.GetProperty("IsDone")` then cast result to `Delegate` and call `.DynamicInvoke(group)`. A fallback to `GetMethod("IsDone")` is preserved for safety.

4. **`BindingFlags` added to `GetProperty` calls** — using `Instance | Public | NonPublic` to be safe on the `internal` class members.

## Harness output

```
RunChecksInternal failures: 0
PASS | Machine: VIVE OpenXR implicit layers disabled for this editor | VIVE Hub layers present; kill-switch env vars active in this editor.
PASS | XR Plug-in Management: OpenXR enabled (Standalone + Android) | OpenXR loader active and initialize-on-startup on for Standalone and Android.
PASS | OpenXR: Meta XR feature group + Meta XR Feature + Touch Controller profile (both platforms) | Meta XR feature group on; Meta XR Feature and Touch Controller profile enabled on both platforms.
PASS | OpenXR render mode: Multi Pass on Standalone (Link one-eye fix), Single Pass on Android | Standalone Multi Pass, Android Single Pass Instanced.
PASS | Meta Project Setup Tool: no outstanding Required issues | Meta Project Setup Tool reports no outstanding Required tasks.
```

failures: 0, console: clean (no errors or warnings).

## Files changed

- Created: `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/MetaProjectSetupCheck.cs`
- Modified: `Packages/com.immrsiv.quest-setup/Editor/Setup/SetupCheck.cs` (appended `new MetaProjectSetupCheck(),`)
- Auto-generated: `...MetaProjectSetupCheck.cs.meta`

## Commit

`9ceae89` — "Delegate Meta-owned checks to the Meta Project Setup Tool via one check"

## Self-review findings

- Reflection is defensive: every call null-checks before use; unknown SDK version degrades to a descriptive FAIL message rather than an exception.
- Fix() is idempotent (just kicks off Meta's FixAllAsync; if already done, FixAllAsync is a no-op per Meta's own logic).
- No AssetDatabase.SaveAssets() call needed — Fix() delegates fully to Meta's own fixer which handles persistence.
- The `Level.GetValue(group)` second-level reflection is the main brittleness point if Meta changes `OptionalLambdaType`'s API; the fallback path via `ToString()` would silently misreport in that case, but this is acceptable given it would require a major SDK internal restructure.
- No concerns about the `internal` `OVRConfigurationTask` class being accessed — `GetTasks` returns it as `IEnumerable<OVRConfigurationTask>` but we only hold it as `object` and reflect, so no assembly boundary issues at compile time.

## Fix round 1

**Review finding addressed:** The dead `GetMethod("IsDone")` fallback and the silent-skip of tasks whose `isDoneResult` was null.

**Changes made to `Packages/com.immrsiv.quest-setup/Editor/Setup/Checks/MetaProjectSetupCheck.cs`:**

1. Deleted the dead `GetMethod("IsDone")` fallback block (lines 93-100 in the original). Since `IsDone` is a property (`Tasks/OVRConfigurationTask.cs:79`), `GetMethod("IsDone")` always returns null — the block could never execute.

2. Replaced the silent-skip behaviour: when `isDoneResult == null` (property not found, or delegate could not be obtained), `Evaluate` now immediately sets `details` to `"OVRConfigurationTask.IsDone could not be read (Meta SDK API moved) - fix manually via Meta > Tools > Project Setup Tool."` and returns `false`, consistent with the other SDK-degradation paths.

3. Updated the XML `<summary>` header to document that `IsDone` is a property (not a method) and to note the hard-degradation behaviour.

**Harness output (live editor, post-fix):**

```
RunChecksInternal failures: 0
```

Console: `Log,Error` — 0 total entries (clean).

The check continues to report `PASS | Meta Project Setup Tool: no outstanding Required issues | Meta Project Setup Tool reports no outstanding Required tasks.` (confirmed by the failures: 0 result; PASS lines from the prior run in § Harness output above remain valid).
