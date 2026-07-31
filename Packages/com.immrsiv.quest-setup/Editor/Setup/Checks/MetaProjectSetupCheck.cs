using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Delegates all Meta-owned project checks (manifest entries, quality,
    /// permissions...) to Meta's own Project Setup Tool instead of duplicating them.
    /// Evaluate reads outstanding Required tasks via reflection (GetTasks is internal);
    /// Fix calls the public OVRProjectSetup.FixAllAsync per platform.
    ///
    /// SDK notes (com.meta.xr.sdk.core@c0efcbf2ba70, Oculus.VR.Editor assembly):
    ///   OVRProjectSetup is in the GLOBAL namespace (no Meta.XR.Editor namespace).
    ///   GetTasks(BuildTargetGroup) - line 310, internal static.
    ///   FixAllAsync(BuildTargetGroup) - line 558, public static, returns Task.
    ///   OVRConfigurationTask.Level - property returning OptionalLambdaType, call GetValue(group) on it.
    ///   OVRConfigurationTask.IsDone - property (NOT a method) returning Func&lt;BuildTargetGroup, bool&gt;;
    ///     get the property value, cast to Delegate, and call DynamicInvoke(group).
    ///     If the delegate cannot be obtained, Evaluate returns false immediately (hard degradation).
    /// </summary>
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

                    // Level: property returning OptionalLambdaType<BuildTargetGroup, TaskLevel>.
                    // We must call .GetValue(group) on the OptionalLambdaType via reflection.
                    string levelStr = null;
                    var levelProp = t.GetProperty("Level",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (levelProp != null)
                    {
                        var levelObj = levelProp.GetValue(task);
                        if (levelObj != null)
                        {
                            // Try GetValue(BuildTargetGroup) method on the OptionalLambdaType
                            var getValueMethod = levelObj.GetType().GetMethod("GetValue",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (getValueMethod != null)
                                levelStr = getValueMethod.Invoke(levelObj, new object[] { group })?.ToString();
                            else
                                // Fallback: the ToString of the OptionalLambdaType may reveal the default
                                levelStr = levelObj.ToString();
                        }
                    }
                    // Fallback: try GetLevel method (not present in this SDK version but kept for safety)
                    if (levelStr == null)
                    {
                        levelStr = t.GetMethod("GetLevel",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?.Invoke(task, new object[] { group })?.ToString();
                    }

                    // IsDone: property (NOT a method) returning Func<BuildTargetGroup, bool>.
                    // Get the property value, cast to Delegate, and call DynamicInvoke(group).
                    // If the delegate cannot be obtained (SDK API moved), stop and report FAIL loudly.
                    bool? isDoneResult = null;
                    var isDoneProp = t.GetProperty("IsDone",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (isDoneProp != null)
                    {
                        var isDoneDelegate = isDoneProp.GetValue(task) as Delegate;
                        if (isDoneDelegate != null)
                            isDoneResult = isDoneDelegate.DynamicInvoke(group) as bool?;
                    }

                    if (isDoneResult == null)
                    {
                        details = "OVRConfigurationTask.IsDone could not be read (Meta SDK API moved) - fix manually via Meta > Tools > Project Setup Tool.";
                        return false;
                    }

                    if (levelStr == "Required" && isDoneResult is bool done && !done)
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
