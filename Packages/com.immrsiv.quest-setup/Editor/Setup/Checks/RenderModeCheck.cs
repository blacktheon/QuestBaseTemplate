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
