using System.Collections.Generic;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>Advisory: UPM dependencies guarantee minimum versions, not exact pins.
    /// Reports any resolved baseline package version or Unity version that drifted
    /// from the blessed baseline. No auto-fix: version changes are a deliberate,
    /// test-first decision (see the README blessing procedure).</summary>
    public class BaselineVersionsCheck : SetupCheck
    {
        static readonly (string package, string version)[] Baseline =
        {
            ("com.meta.xr.sdk.all", "205.0.0"),
            ("com.unity.xr.openxr", "1.17.0"),
            ("com.unity.render-pipelines.universal", "17.3.0"),
            ("com.unity.inputsystem", "1.19.0"),
        };
        const string UnityStream = "6000.3";

        public override string Label => "Baseline versions: blessed Unity stream and package versions";
        public override bool IsAdvisory => true;

        public override bool Evaluate(out string details)
        {
            var drift = new List<string>();
            if (!Application.unityVersion.StartsWith(UnityStream))
                drift.Add($"Unity {Application.unityVersion} (blessed stream {UnityStream})");
            foreach (var (package, version) in Baseline)
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForPackageName(package);
                if (info == null) drift.Add($"{package} not resolved");
                else if (info.version != version) drift.Add($"{package} {info.version} (blessed {version})");
            }
            details = drift.Count == 0
                ? "Unity stream and all baseline package versions match the blessed baseline."
                : "Drift: " + string.Join("; ", drift);
            return drift.Count == 0;
        }

        public override void Fix()
        {
            Debug.LogWarning("[QuestSetup] Version drift is informational: bless a new baseline (see README) or align Packages/manifest.json manually.");
        }
    }
}
