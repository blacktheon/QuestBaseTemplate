using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>
    /// Enumerates all rules registered in BuildValidator (Unity XR Project Validation) for both
    /// BuildTargetGroup.Standalone and BuildTargetGroup.Android, and invokes FixIt for every
    /// failing, automatically-fixable, non-excluded rule.
    ///
    /// Exclusion policy:
    ///   (a) Optional input-system migration rules: rules whose message contains "PoseControl" or
    ///       "Vector2Control" are excluded UNLESS the corresponding OptionalRulePrefs toggle is on.
    ///       These are [Optional] OpenXR rules that modify scripting defines and can break existing
    ///       input code if applied without review.
    ///   (b) Standalone Single-Pass-Instanced rules: any rule in the Standalone group whose message
    ///       contains "Single Pass Instanced" or "Multi-view" is excluded with the render-mode policy
    ///       reason. Our RenderModeCheck enforces Multi Pass on Standalone (required for correct
    ///       two-eye rendering over Quest Link — see README). This exclusion prevents BuildValidator
    ///       from fighting our policy.
    ///   (c) Rules without FixIt (fixIt == null) are surfaced as manual counts in the details string
    ///       and never cause the check to fail. They require developer action in Project Settings
    ///       > XR Plug-in Management > Project Validation.
    ///
    /// Implementation note: BuildValidator.PlatformRules and GetCurrentValidationIssues are internal;
    /// they are accessed via reflection (same pattern as MetaProjectSetupCheck on OVRProjectSetup).
    /// The asmdef references Unity.XR.CoreUtils.Editor for the public BuildValidationRule type.
    ///
    /// SDK notes (com.unity.xr.core-utils@a8b900321199):
    ///   BuildValidator.PlatformRules — line 51, internal static property,
    ///     returns Dictionary&lt;BuildTargetGroup, List&lt;BuildValidationRule&gt;&gt;.
    ///   BuildValidationRule.IsRuleEnabled — public Func&lt;bool&gt; property (default () => true).
    ///   BuildValidationRule.CheckPredicate — public Func&lt;bool&gt; property (null = always fails).
    ///   BuildValidationRule.FixIt — public Action (null = no automatic fix).
    ///   BuildValidationRule.FixItAutomatic — public bool (true = automatic, false = requires input).
    ///   BuildValidationRule.Message — public string.
    ///   BuildValidationRule.Error — public bool (true = error, false = warning).
    ///   BuildValidationRule.Category — public string.
    /// </summary>
    public class ProjectValidationCheck : SetupCheck
    {
        static readonly BuildTargetGroup[] Groups =
            { BuildTargetGroup.Standalone, BuildTargetGroup.Android };

        // Reflection handle for BuildValidator.PlatformRules (internal static property).
        static PropertyInfo _platformRulesProp;
        static bool _apiResolved;

        public override string Label => "Unity XR Project Validation: no outstanding fixable issues";

        static bool TryGetPlatformRules(out Dictionary<BuildTargetGroup, List<BuildValidationRule>> rules)
        {
            if (!_apiResolved)
            {
                _apiResolved = true;
                var bvType = typeof(BuildValidator);
                _platformRulesProp = bvType.GetProperty(
                    "PlatformRules",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }

            if (_platformRulesProp == null)
            {
                rules = null;
                return false;
            }

            rules = _platformRulesProp.GetValue(null)
                as Dictionary<BuildTargetGroup, List<BuildValidationRule>>;
            return rules != null;
        }

        public override bool Evaluate(out string details)
        {
            if (!TryGetPlatformRules(out var platformRules))
            {
                details = "BuildValidator.PlatformRules not found (Unity.XR.CoreUtils API may have changed). " +
                          "Check Project Settings > XR Plug-in Management > Project Validation manually.";
                return false;
            }

            bool poseControlOn = OptionalRulePrefs.PoseControlEnabled;
            bool stickControlOn = OptionalRulePrefs.StickControlEnabled;

            // Parity with BuildValidator.GetCurrentValidationIssues (lines 81-86): skip scene-only
            // rules when the editor is in Prefab Stage, as the SDK's own window does.
            bool inPrefabStage = PrefabStageUtility.GetCurrentPrefabStage() != null;

            int totalFixable = 0;
            var groupSummaries = new List<string>();

            foreach (var group in Groups)
            {
                if (!platformRules.TryGetValue(group, out var rules))
                {
                    groupSummaries.Add($"{GroupName(group)}: no rules registered");
                    continue;
                }

                bool isStandalone = group == BuildTargetGroup.Standalone;

                int fixable = 0;
                int spiExcluded = 0;
                int optionalOff = 0;
                int manual = 0;

                foreach (var rule in rules)
                {
                    // Skip rules not applicable right now
                    if (rule.IsRuleEnabled == null || !rule.IsRuleEnabled.Invoke())
                        continue;

                    // Parity with BuildValidator: skip scene-only rules while in Prefab Stage.
                    if (inPrefabStage && rule.SceneOnlyValidation)
                        continue;

                    // Skip passing rules
                    bool fails = rule.CheckPredicate == null || !rule.CheckPredicate.Invoke();
                    if (!fails)
                        continue;

                    string msg = rule.Message ?? string.Empty;

                    // Exclusion (b): Standalone SPI rule
                    if (isStandalone && IsSinglePassInstancingRule(msg))
                    {
                        spiExcluded++;
                        continue;
                    }

                    // No FixIt action => manual (never fail the check)
                    if (rule.FixIt == null)
                    {
                        manual++;
                        continue;
                    }

                    // Exclusion (a): optional input-system rules
                    if (IsPoseControlRule(msg) && !poseControlOn)
                    {
                        optionalOff++;
                        continue;
                    }
                    if (IsStickControlRule(msg) && !stickControlOn)
                    {
                        optionalOff++;
                        continue;
                    }

                    // FixItAutomatic=false rules require user interaction and cannot be auto-fixed;
                    // count as manual so we surface them without blocking.
                    if (!rule.FixItAutomatic)
                    {
                        manual++;
                        continue;
                    }

                    fixable++;
                }

                totalFixable += fixable;

                string gname = GroupName(group);
                string summary;
                if (fixable == 0)
                {
                    var parts = new List<string>();
                    if (spiExcluded > 0) parts.Add($"{spiExcluded} policy-excluded");
                    if (optionalOff > 0) parts.Add($"{optionalOff} optional off");
                    if (manual > 0) parts.Add($"{manual} manual");

                    summary = parts.Count > 0
                        ? $"{gname}: 0 fixable ({string.Join(", ", parts)})"
                        : $"{gname}: 0 fixable";
                }
                else
                {
                    summary = $"{gname}: {fixable} fixable";
                    var extras = new List<string>();
                    if (spiExcluded > 0) extras.Add($"{spiExcluded} policy-excluded");
                    if (optionalOff > 0) extras.Add($"{optionalOff} optional off");
                    if (manual > 0) extras.Add($"{manual} manual");
                    if (extras.Count > 0) summary += $" ({string.Join(", ", extras)})";
                }

                groupSummaries.Add(summary);
            }

            details = string.Join("; ", groupSummaries) + ".";
            if (totalFixable > 0)
                details += " Fix invokes FixIt for each fixable rule.";

            return totalFixable == 0;
        }

        public override void Fix()
        {
            if (!TryGetPlatformRules(out var platformRules))
                throw new InvalidOperationException(
                    "BuildValidator.PlatformRules not found — fix manually via Project Settings > XR Plug-in Management > Project Validation.");

            bool poseControlOn = OptionalRulePrefs.PoseControlEnabled;
            bool stickControlOn = OptionalRulePrefs.StickControlEnabled;

            // Parity with BuildValidator.GetCurrentValidationIssues (lines 81-86): skip scene-only
            // rules when the editor is in Prefab Stage, as the SDK's own window does.
            bool inPrefabStage = PrefabStageUtility.GetCurrentPrefabStage() != null;

            int fixedCount = 0;

            foreach (var group in Groups)
            {
                if (!platformRules.TryGetValue(group, out var rules))
                    continue;

                bool isStandalone = group == BuildTargetGroup.Standalone;

                foreach (var rule in rules)
                {
                    if (rule.IsRuleEnabled == null || !rule.IsRuleEnabled.Invoke())
                        continue;

                    // Parity with BuildValidator: skip scene-only rules while in Prefab Stage.
                    if (inPrefabStage && rule.SceneOnlyValidation)
                        continue;

                    bool fails = rule.CheckPredicate == null || !rule.CheckPredicate.Invoke();
                    if (!fails)
                        continue;

                    if (rule.FixIt == null || !rule.FixItAutomatic)
                        continue;

                    string msg = rule.Message ?? string.Empty;

                    if (isStandalone && IsSinglePassInstancingRule(msg))
                        continue;

                    if (IsPoseControlRule(msg) && !poseControlOn)
                        continue;

                    if (IsStickControlRule(msg) && !stickControlOn)
                        continue;

                    Debug.Log($"[QuestSetup] ProjectValidation Fix: [{GroupName(group)}] {msg}");
                    rule.FixIt.Invoke();
                    fixedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[QuestSetup] ProjectValidation Fix complete: {fixedCount} rules fixed.");
        }

        // ---- Exclusion match helpers ----

        /// <summary>
        /// Returns true for optional input-system PoseControl migration rules.
        /// Match key: "[Optional] Switch to use InputSystem.XR.PoseControl instead of OpenXR.Input.PoseControl"
        /// Substring: "PoseControl"
        /// </summary>
        static bool IsPoseControlRule(string msg) =>
            msg.IndexOf("PoseControl", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Returns true for optional input-system StickControl / Vector2Control migration rules.
        /// Match key: "[Optional] Switch to use StickControl thumbsticks instead of Vector2Control"
        /// Substrings: "Vector2Control" or "StickControl thumbsticks" — brief check on "Vector2Control".
        /// </summary>
        static bool IsStickControlRule(string msg) =>
            msg.IndexOf("Vector2Control", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (msg.IndexOf("StickControl", StringComparison.OrdinalIgnoreCase) >= 0 &&
             msg.IndexOf("thumbstick", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>
        /// Returns true for rules on Standalone that require Single Pass Instanced / Multi-view.
        /// These conflict with our Standalone Multi Pass policy (required for Quest Link two-eye rendering).
        /// Match substrings: "Single Pass Instanced" or "Multi-view" (as part of "Multi-view" in render mode context).
        /// </summary>
        static bool IsSinglePassInstancingRule(string msg) =>
            msg.IndexOf("Single Pass Instanced", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (msg.IndexOf("Multi-view", StringComparison.OrdinalIgnoreCase) >= 0 &&
             msg.IndexOf("Render Mode", StringComparison.OrdinalIgnoreCase) >= 0);

        static string GroupName(BuildTargetGroup group) =>
            group == BuildTargetGroup.Android ? "Android" : "Standalone";
    }

    /// <summary>
    /// EditorPrefs keys for the optional input-system migration toggles in the Quest Setup window.
    /// Both default to false (off) — applying these defines can break existing input code.
    /// Keys are per-project (suffixed with ProjectKey) so toggling one project does not bleed
    /// into other projects on the same machine.
    /// </summary>
    public static class OptionalRulePrefs
    {
        /// <summary>
        /// Stable per-project suffix derived from Application.dataPath.
        /// Shared with SetupKeys so there is one implementation.
        /// </summary>
        internal static string ProjectKey =>
            Application.dataPath.Replace('/', '_').Replace('\\', '_').Replace(':', '_');

        public static string PoseControlKey => "Immrsiv.QuestSetup.Optional.PoseControl." + ProjectKey;
        public static string StickControlKey => "Immrsiv.QuestSetup.Optional.StickControl." + ProjectKey;

        public static bool PoseControlEnabled
        {
            get => EditorPrefs.GetBool(PoseControlKey, false);
            set => EditorPrefs.SetBool(PoseControlKey, value);
        }

        public static bool StickControlEnabled
        {
            get => EditorPrefs.GetBool(StickControlKey, false);
            set => EditorPrefs.SetBool(StickControlKey, value);
        }
    }
}
