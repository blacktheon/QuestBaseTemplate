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
            EditorUtility.SetDirty(perBuildTarget);
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
