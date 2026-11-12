using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Immrsiv.QuestSetup.Editor.Tests
{
    public class QuestSetupWindowTests
    {
        Dictionary<BuildTargetGroup, List<BuildValidationRule>> rules;
        Dictionary<BuildTargetGroup, List<BuildValidationRule>> savedRules;
        QuestSetupWindow window;
        int validationCalls;

        [SetUp]
        public void SetUp()
        {
            rules = (Dictionary<BuildTargetGroup, List<BuildValidationRule>>)typeof(BuildValidator)
                .GetProperty("PlatformRules", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            savedRules = new Dictionary<BuildTargetGroup, List<BuildValidationRule>>(rules);
            rules.Clear();
            validationCalls = 0;
            BuildValidator.AddRules(BuildTargetGroup.Standalone, new[]
            {
                new BuildValidationRule { CheckPredicate = () => { validationCalls++; return true; } }
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (window != null) Object.DestroyImmediate(window);
            rules.Clear();
            foreach (var pair in savedRules) rules.Add(pair.Key, pair.Value);
        }

        [UnityTest]
        public IEnumerator EnableAndFocusDeferAndCoalesceValidation()
        {
            window = ScriptableObject.CreateInstance<QuestSetupWindow>();
            var focus = typeof(QuestSetupWindow).GetMethod("OnFocus", BindingFlags.Instance | BindingFlags.NonPublic);
            focus.Invoke(window, null);
            focus.Invoke(window, null);
            Assert.That(validationCalls, Is.Zero, "XR settings must not be read during window deserialization/focus.");

            // Batch-mode EditMode tests can advance many yields between inspector updates.
            // Wait for the real editor callback, with a wall-clock bound for a missing refresh.
            double deadline = EditorApplication.timeSinceStartup + 5;
            while (validationCalls == 0 && EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That(validationCalls, Is.EqualTo(1), "The queued refresh must run once after initialization.");
            yield return null;
            Assert.That(validationCalls, Is.EqualTo(1), "Focus must not accumulate duplicate refresh callbacks.");
        }

        [UnityTest]
        public IEnumerator ClosingBeforeRefreshCancelsValidation()
        {
            window = ScriptableObject.CreateInstance<QuestSetupWindow>();
            Object.DestroyImmediate(window);
            window = null;
            bool delayedCallbacksRan = false;
            EditorApplication.CallbackFunction sentinel = () => delayedCallbacksRan = true;
            EditorApplication.delayCall += sentinel;
            try
            {
                double deadline = EditorApplication.timeSinceStartup + 5;
                while (!delayedCallbacksRan && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(delayedCallbacksRan, Is.True, "The editor must dispatch delayed callbacks before checking cancellation.");
                Assert.That(validationCalls, Is.Zero, "A closed setup window must not evaluate SDK settings.");
            }
            finally
            {
                EditorApplication.delayCall -= sentinel;
            }
        }
    }
}
