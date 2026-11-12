using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace Immrsiv.QuestSetup.Editor.Tests
{
    public class ProjectValidationCheckTests
    {
        Dictionary<BuildTargetGroup, List<BuildValidationRule>> rules;
        Dictionary<BuildTargetGroup, List<BuildValidationRule>> savedRules;
        BuildTargetGroup savedSelection;
        BuildTarget activeTarget;

        [SetUp]
        public void SetUp()
        {
            savedSelection = EditorUserBuildSettings.selectedBuildTargetGroup;
            activeTarget = EditorUserBuildSettings.activeBuildTarget;
            rules = (Dictionary<BuildTargetGroup, List<BuildValidationRule>>)typeof(BuildValidator)
                .GetProperty("PlatformRules", BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
            savedRules = new Dictionary<BuildTargetGroup, List<BuildValidationRule>>(rules);
            rules.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            rules.Clear();
            foreach (var pair in savedRules)
                rules.Add(pair.Key, pair.Value);
            EditorUserBuildSettings.selectedBuildTargetGroup = savedSelection;
            Assert.That(EditorUserBuildSettings.activeBuildTarget, Is.EqualTo(activeTarget),
                "Validation must not switch the active build target.");
        }

        [TestCase(BuildTargetGroup.Standalone)]
        [TestCase(BuildTargetGroup.Android)]
        public void EvaluateUsesEachRulesPlatformAndRestoresSelection(BuildTargetGroup initialSelection)
        {
            EditorUserBuildSettings.selectedBuildTargetGroup = initialSelection;
            int predicatesRun = 0;
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
                BuildValidator.AddRules(group, new[]
                {
                    new BuildValidationRule
                    {
                        // A wrong context must not silently skip the other platform's rule.
                        IsRuleEnabled = () => EditorUserBuildSettings.selectedBuildTargetGroup == group,
                        CheckPredicate = () =>
                        {
                            Assert.That(EditorUserBuildSettings.selectedBuildTargetGroup, Is.EqualTo(group));
                            predicatesRun++;
                            return true;
                        }
                    }
                });

            Assert.That(new ProjectValidationCheck().Evaluate(out var details), Is.True, details);
            Assert.That(predicatesRun, Is.EqualTo(2), "Both platforms must actually be checked.");
            Assert.That(EditorUserBuildSettings.selectedBuildTargetGroup, Is.EqualTo(initialSelection));
        }

        [TestCase(BuildTargetGroup.Standalone)]
        [TestCase(BuildTargetGroup.Android)]
        public void FixWritesToEachRulesPlatformAndRestoresSelection(BuildTargetGroup initialSelection)
        {
            EditorUserBuildSettings.selectedBuildTargetGroup = initialSelection;
            var fixedPlatforms = new HashSet<BuildTargetGroup>();
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
                BuildValidator.AddRules(group, new[]
                {
                    new BuildValidationRule
                    {
                        Message = "Regression test platform setting",
                        IsRuleEnabled = () => EditorUserBuildSettings.selectedBuildTargetGroup == group,
                        CheckPredicate = () => fixedPlatforms.Contains(EditorUserBuildSettings.selectedBuildTargetGroup),
                        FixIt = () => fixedPlatforms.Add(EditorUserBuildSettings.selectedBuildTargetGroup)
                    }
                });

            new ProjectValidationCheck().Fix();

            Assert.That(fixedPlatforms, Is.EquivalentTo(new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android }));
            Assert.That(EditorUserBuildSettings.selectedBuildTargetGroup, Is.EqualTo(initialSelection));
        }

        [TestCase("enabled")]
        [TestCase("predicate")]
        [TestCase("fix")]
        public void RuleExceptionsRemainVisibleAndRestoreSelection(string failureStage)
        {
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;
            var failure = new InvalidOperationException("Deliberate regression-test failure");
            BuildValidator.AddRules(BuildTargetGroup.Android, new[]
            {
                new BuildValidationRule
                {
                    IsRuleEnabled = () => failureStage == "enabled" ? throw failure : true,
                    CheckPredicate = () => failureStage == "predicate" ? throw failure : false,
                    FixIt = () => throw failure
                }
            });

            var check = new ProjectValidationCheck();
            TestDelegate run = failureStage == "fix"
                ? () => check.Fix()
                : () => check.Evaluate(out _);
            Assert.That(Assert.Throws<InvalidOperationException>(run), Is.SameAs(failure));
            Assert.That(EditorUserBuildSettings.selectedBuildTargetGroup, Is.EqualTo(BuildTargetGroup.Standalone));
        }

        [Test]
        public void MetaQuestMultiviewRuleDoesNotReadStandaloneFeatures()
        {
            // Exercise the actual OpenXR rule that threw on a fresh STBC clone.
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var feature = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android)
                .GetFeature<MetaQuestFeature>();
            Assert.That(feature, Is.Not.Null, "Android Build Support and OpenXR are required for this regression.");
            var featureRules = new List<OpenXRFeature.ValidationRule>();
            typeof(MetaQuestFeature).GetMethod("GetValidationChecks", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(feature, new object[] { featureRules, BuildTargetGroup.Android });
            var multiview = featureRules.Single(rule => rule.message.Contains("Multiview Render Regions Optimization - All Passes"));
            rules.Clear();
            BuildValidator.AddRules(BuildTargetGroup.Android, new[]
            {
                new BuildValidationRule { Message = multiview.message, CheckPredicate = multiview.checkPredicate }
            });
            EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Standalone;

            Assert.DoesNotThrow(() => new ProjectValidationCheck().Evaluate(out _));
            Assert.That(EditorUserBuildSettings.selectedBuildTargetGroup, Is.EqualTo(BuildTargetGroup.Standalone));
        }
    }
}
