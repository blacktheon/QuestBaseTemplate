using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Delegates all Meta-owned project checks (manifest entries, quality,
    /// permissions...) to Meta's own Project Setup Tool instead of duplicating them.
    /// Evaluate reads ALL outstanding auto-fixable tasks (Required + Recommended) for
    /// both Android and Standalone via reflection (GetTasks is internal); the policy
    /// ignore for Meta's Standalone "Use Stereo Rendering Instancing" recommendation is
    /// applied programmatically so Meta's tab shows 0 outstanding (Multi Pass is required
    /// on Standalone for correct two-eye rendering over Quest Link — see README).
    /// Manual-only tasks (no FixAction/AsyncFixAction) are counted separately and never
    /// fail the check. Fix calls the internal OVRProjectSetup.FixTasks with blocking:true
    /// via reflection, which processes all fixes synchronously before returning — safe in
    /// both interactive and batch (CI) contexts.
    ///
    /// SDK notes (com.meta.xr.sdk.core@c0efcbf2ba70, Oculus.VR.Editor assembly):
    ///   OVRProjectSetup is in the GLOBAL namespace (no Meta.XR.Editor namespace).
    ///   GetTasks(BuildTargetGroup) - line 310, internal static.
    ///   FixTasks(BuildTargetGroup, Func filter, LogMessages, bool blocking, Action onCompleted)
    ///     - line 563, internal static; blocking:true forces synchronous execution via
    ///       OVRConfigurationTaskProcessorQueue (lines 83-94: blocking flag calls Update()
    ///       immediately rather than waiting for EditorApplication.update).
    ///   OVRConfigurationTask.Level - property returning OptionalLambdaType, call GetValue(group) on it.
    ///   OVRConfigurationTask.IsDone - property getter returns the GetDoneState method group as a
    ///     Func&lt;BuildTargetGroup,bool&gt; delegate (shares the task's internal done-cache); the
    ///     reflection approach is: get the property value, cast to Delegate, call DynamicInvoke(group).
    ///     If the delegate cannot be obtained, Evaluate returns false immediately (hard degradation).
    ///   OVRConfigurationTask.IsIgnored(BuildTargetGroup) - public instance method (accessible via reflection).
    ///   OVRConfigurationTask.SetIgnored(BuildTargetGroup, bool) - public instance method (accessible via reflection).
    ///   OVRConfigurationTask.FixAction - public property, null means no automatic sync fix.
    ///   OVRConfigurationTask.AsyncFixAction - public property, null means no automatic async fix.
    ///   OVRConfigurationTaskFixer.OpenTasksFilter skips IsIgnored and done tasks — FixTasks is safe to call as-is.
    ///   Stereo-instancing task match key (OVRProjectSetupRenderingTasks.cs line 254):
    ///     message = "Use Stereo Rendering Instancing", no platform filter (applies to both groups).
    /// </summary>
    public class MetaProjectSetupCheck : SetupCheck
    {
        static readonly BuildTargetGroup[] Groups =
            { BuildTargetGroup.Android, BuildTargetGroup.Standalone };

        /// <summary>
        /// The message text of Meta's "Use Stereo Rendering Instancing" recommendation task.
        /// This is the stable match key derived from OVRProjectSetupRenderingTasks.cs line 254.
        /// </summary>
        private const string StereoInstancingTaskMessage = "Use Stereo Rendering Instancing";

        public override string Label => "Meta Project Setup Tool: no outstanding Required/Recommended issues";

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

            // Per-group counters
            int totalOutstanding = 0;
            var groupSummaries = new List<string>();

            foreach (var group in Groups)
            {
                var tasks = ((IEnumerable)getTasks.Invoke(null, new object[] { group })).Cast<object>().ToList();

                // Apply policy ignore BEFORE counting so the count reflects the ignored state.
                // This is idempotent: we short-circuit if already ignored.
                ApplyPolicyIgnore(tasks, group);

                int requiredOutstanding = 0;
                int recommendedOutstanding = 0;
                int policyIgnoredCount = 0;
                int manualCount = 0;
                var manualLabels = new List<string>();

                foreach (var task in tasks)
                {
                    var t = task.GetType();

                    // --- Level ---
                    string levelStr = GetTaskLevel(t, task, group);

                    // We count Required and Recommended; skip Hidden, Optional, and null.
                    bool isRequired = levelStr == "Required";
                    bool isRecommended = levelStr == "Recommended";
                    if (!isRequired && !isRecommended)
                        continue;

                    // --- Is ignored? (checked FIRST so done+ignored tasks still increment the counter) ---
                    bool? isIgnoredResult = GetIsIgnored(t, task, group);

                    if (isIgnoredResult == true)
                    {
                        // Count for display regardless of done state
                        policyIgnoredCount++;
                        continue;
                    }

                    // --- IsDone ---
                    bool? isDoneResult = GetIsDone(t, task, group);
                    if (isDoneResult == null)
                    {
                        details = "OVRConfigurationTask.IsDone could not be read (Meta SDK API moved) - fix manually via Meta > Tools > Project Setup Tool.";
                        return false;
                    }
                    if (isDoneResult == true)
                        continue; // already satisfied

                    // --- Has automatic fix? ---
                    bool hasAutoFix = HasAutoFix(t, task);

                    if (!hasAutoFix)
                    {
                        // Manual task: count separately, never fail
                        manualCount++;
                        string label = GetMessage(t, task, group);
                        if (!string.IsNullOrEmpty(label))
                            manualLabels.Add(label);
                        continue;
                    }

                    // Outstanding auto-fixable, non-ignored task
                    if (isRequired) requiredOutstanding++;
                    else recommendedOutstanding++;
                }

                int groupOutstanding = requiredOutstanding + recommendedOutstanding;
                totalOutstanding += groupOutstanding;

                string groupName = group == BuildTargetGroup.Android ? "Android" : "Standalone";
                string summary;
                if (groupOutstanding == 0)
                {
                    summary = $"{groupName}: 0 outstanding";
                    if (policyIgnoredCount > 0)
                        summary += $" ({policyIgnoredCount} policy-ignored";
                    if (manualCount > 0)
                        summary += policyIgnoredCount > 0 ? $", {manualCount} manual)" : $" ({manualCount} manual)";
                    else if (policyIgnoredCount > 0)
                        summary += ")";
                }
                else
                {
                    summary = $"{groupName}: {groupOutstanding} outstanding";
                    if (requiredOutstanding > 0 && recommendedOutstanding > 0)
                        summary += $" ({requiredOutstanding} Required, {recommendedOutstanding} Recommended)";
                    else if (requiredOutstanding > 0)
                        summary += $" ({requiredOutstanding} Required)";
                    else
                        summary += $" ({recommendedOutstanding} Recommended)";
                    if (policyIgnoredCount > 0)
                        summary += $", {policyIgnoredCount} policy-ignored";
                    if (manualCount > 0)
                    {
                        summary += $", {manualCount} manual";
                        if (manualLabels.Count > 0)
                            summary += $" ({string.Join(", ", manualLabels)})";
                    }
                }

                groupSummaries.Add(summary);
            }

            details = string.Join("; ", groupSummaries) + ".";
            if (totalOutstanding > 0)
                details += " Fix runs Meta's Fix All.";

            return totalOutstanding == 0;
        }

        public override void Fix()
        {
            // Use the internal blocking FixTasks (line 563, Oculus.VR.Editor assembly) rather than
            // the public FixAllAsync. FixAllAsync enqueues a non-blocking fixer that only runs on
            // EditorApplication.update — in batch mode EditorApplication.Exit fires before any pump,
            // so fixes are silently dropped. FixTasks with blocking:true calls
            // OVRConfigurationTaskProcessorQueue.Update() immediately (lines 83-94) and completes
            // before returning, making it safe in both interactive and batch contexts.
            //
            // Signature (line 563):
            //   internal static void FixTasks(
            //       BuildTargetGroup buildTargetGroup,
            //       Func<IEnumerable<OVRConfigurationTask>, List<OVRConfigurationTask>> filter = null,
            //       LogMessages logMessages = LogMessages.Disabled,   // int 0
            //       bool blocking = true,
            //       Action<OVRConfigurationTaskProcessor> onCompleted = null)
            var setupType = SetupType;
            if (setupType == null)
                throw new InvalidOperationException(
                    "OVRProjectSetup type not found - fix manually via Meta > Tools > Project Setup Tool.");

            var fixTasks = setupType.GetMethod("FixTasks",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (fixTasks == null)
                throw new InvalidOperationException(
                    "OVRProjectSetup.FixTasks not found - fix manually via Meta > Tools > Project Setup Tool.");

            // LogMessages enum lives on OVRProjectSetup (internal enum, value Disabled=0).
            var logMessagesType = setupType.GetNestedType("LogMessages",
                BindingFlags.NonPublic | BindingFlags.Public)
                ?? Type.GetType("OVRProjectSetup+LogMessages, Oculus.VR.Editor");
            // Fall back to raw int 0 (Disabled) if the nested-type lookup fails.
            object logDisabled = logMessagesType != null
                ? Enum.ToObject(logMessagesType, 0)
                : (object)0;

            foreach (var group in Groups)
            {
                // Pass: group, filter=null, logMessages=Disabled, blocking=true, onCompleted=null
                fixTasks.Invoke(null, new object[] { group, null, logDisabled, true, null });
            }
            // FixTasks already skips ignored and done tasks (OVRConfigurationTaskFixer.OpenTasksFilter).
        }

        // ---- Reflection helpers ----

        /// <summary>Guard so the IsIgnored-API-not-found warning logs at most once per domain load.</summary>
        private static bool _isIgnoredApiWarningLogged;

        /// <summary>
        /// For any task in the given group whose message matches StereoInstancingTaskMessage,
        /// call SetIgnored(group, true) if not already ignored. Idempotent.
        /// Logs the policy reason the first time the ignore is set.
        /// If the IsIgnored API cannot be found (SDK changed), logs a one-time warning and skips.
        /// </summary>
        private static void ApplyPolicyIgnore(List<object> tasks, BuildTargetGroup group)
        {
            if (group != BuildTargetGroup.Standalone)
                return;

            foreach (var task in tasks)
            {
                var t = task.GetType();
                string msg = GetMessage(t, task, group);
                if (msg != StereoInstancingTaskMessage)
                    continue;

                // Check current ignore state
                bool? isAlreadyIgnored = GetIsIgnored(t, task, group);
                if (isAlreadyIgnored == null)
                {
                    // IsIgnored API not found — cannot determine state; skip SetIgnored
                    if (!_isIgnoredApiWarningLogged)
                    {
                        _isIgnoredApiWarningLogged = true;
                        Debug.LogWarning("[QuestSetup] IsIgnored API not found on OVRConfigurationTask (Meta SDK API may have changed). " +
                                         "The Single Pass Instanced policy-ignore could not be applied; the SPI recommendation may reappear in Meta's Project Setup Tool.");
                    }
                    return;
                }

                if (isAlreadyIgnored == true)
                    return; // already ignored — idempotent, do not log again

                // Apply ignore via SetIgnored(BuildTargetGroup, bool)
                var setIgnored = t.GetMethod("SetIgnored",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (setIgnored != null)
                {
                    setIgnored.Invoke(task, new object[] { group, true });
                    Debug.Log("[QuestSetup] Ignoring Meta's Single Pass Instanced recommendation on Standalone: " +
                              "Multi Pass is required for correct two-eye rendering over Quest Link (see README).");
                }
                return;
            }
        }

        private static string GetTaskLevel(Type t, object task, BuildTargetGroup group)
        {
            var levelProp = t.GetProperty("Level",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (levelProp == null) return null;

            var levelObj = levelProp.GetValue(task);
            if (levelObj == null) return null;

            var getValueMethod = levelObj.GetType().GetMethod("GetValue",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getValueMethod != null)
                return getValueMethod.Invoke(levelObj, new object[] { group })?.ToString();

            // GetValue not found — cannot resolve level string; return null rather than a
            // type-name string that would silently break Required/Recommended match keys.
            return null;
        }

        private static bool? GetIsDone(Type t, object task, BuildTargetGroup group)
        {
            var isDoneProp = t.GetProperty("IsDone",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (isDoneProp == null) return null;

            var isDoneDelegate = isDoneProp.GetValue(task) as Delegate;
            if (isDoneDelegate == null) return null;

            return isDoneDelegate.DynamicInvoke(group) as bool?;
        }

        private static bool HasAutoFix(Type t, object task)
        {
            var fixActionProp = t.GetProperty("FixAction",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fixActionProp?.GetValue(task) != null) return true;

            var asyncFixActionProp = t.GetProperty("AsyncFixAction",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (asyncFixActionProp?.GetValue(task) != null) return true;

            return false;
        }

        /// <summary>
        /// Returns true/false from IsIgnored(group), or null if the method cannot be found
        /// (API changed — caller must not treat indeterminate as false).
        /// </summary>
        private static bool? GetIsIgnored(Type t, object task, BuildTargetGroup group)
        {
            var isIgnoredMethod = t.GetMethod("IsIgnored",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (isIgnoredMethod == null) return null;
            return (bool)(isIgnoredMethod.Invoke(task, new object[] { group }) ?? false);
        }

        private static string GetMessage(Type t, object task, BuildTargetGroup group)
        {
            var messageProp = t.GetProperty("Message",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (messageProp == null) return null;

            var messageObj = messageProp.GetValue(task);
            if (messageObj == null) return null;

            var getValueMethod = messageObj.GetType().GetMethod("GetValue",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getValueMethod != null)
                return getValueMethod.Invoke(messageObj, new object[] { group })?.ToString();

            // GetValue not found — cannot resolve message string; return null rather than a
            // type-name string that would silently break the StereoInstancingTaskMessage match key.
            return null;
        }
    }
}
