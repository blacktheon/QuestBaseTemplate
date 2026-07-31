using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Immrsiv.QuestSetup.Editor
{
    public class AndroidPlayerCheck : SetupCheck
    {
        public override string Label =>
            "Android player: minSdk 32, ARM64, IL2CPP, Vulkan only, ASTC, Landscape Left";

        public override bool Evaluate(out string details)
        {
            var issues = new List<string>();
            if ((int)PlayerSettings.Android.minSdkVersion < 32)
                issues.Add($"minSdk {(int)PlayerSettings.Android.minSdkVersion} (want 32)");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                issues.Add($"architectures {PlayerSettings.Android.targetArchitectures} (want ARM64)");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                issues.Add("scripting backend not IL2CPP");
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) ||
                apis.Length != 1 || apis[0] != GraphicsDeviceType.Vulkan)
                issues.Add("graphics APIs not exactly [Vulkan]");
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.LandscapeLeft)
                issues.Add($"orientation {PlayerSettings.defaultInterfaceOrientation} (want LandscapeLeft)");
            if (EditorUserBuildSettings.androidBuildSubtarget != MobileTextureSubtarget.ASTC)
                issues.Add($"Build Settings texture compression {EditorUserBuildSettings.androidBuildSubtarget} (want ASTC)");

            bool astcOk = false;
            try
            {
                var formats = PlayerSettings.Android.textureCompressionFormats;
                astcOk = formats.Length == 1 && formats[0] == TextureCompressionFormat.ASTC;
            }
            catch
            {
                var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").First();
                var prop = new SerializedObject(asset)
                    .FindProperty("m_BuildTargetDefaultTextureCompressionFormat"); // value 3 = ASTC
                astcOk = prop != null && prop.intValue == 3;
            }
            if (!astcOk) issues.Add("Player texture compression not exactly [ASTC]");

            details = issues.Count == 0 ? "All Android player settings match."
                                        : "Wrong: " + string.Join("; ", issues);
            return issues.Count == 0;
        }

        public override void Fix()
        {
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            try
            {
                PlayerSettings.Android.textureCompressionFormats =
                    new[] { TextureCompressionFormat.ASTC };
            }
            catch
            {
                var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").First();
                var so = new SerializedObject(asset);
                var prop = so.FindProperty("m_BuildTargetDefaultTextureCompressionFormat");
                if (prop != null) { prop.intValue = 3; so.ApplyModifiedProperties(); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
