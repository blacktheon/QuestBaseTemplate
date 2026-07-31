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
