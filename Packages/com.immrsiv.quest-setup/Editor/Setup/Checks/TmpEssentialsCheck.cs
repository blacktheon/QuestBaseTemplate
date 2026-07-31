using System.IO;
using UnityEditor;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>The interaction samples use TextMesh Pro UI; consumers need the TMP
    /// essential resources imported once per project.</summary>
    public class TmpEssentialsCheck : SetupCheck
    {
        const string EssentialsFolder = "Assets/TextMesh Pro/Resources";

        public override string Label => "TextMesh Pro essential resources imported";
        public override bool IsAdvisory => true;

        public override bool Evaluate(out string details)
        {
            bool ok = Directory.Exists(EssentialsFolder);
            details = ok ? "TMP essentials present."
                         : "TMP essentials missing (sample UI text will not render).";
            return ok;
        }

        public override void Fix()
        {
            if (Directory.Exists(EssentialsFolder)) return;
            // com.unity.ugui (this project's TMP package, b95364aab964) does not expose
            // TMPro.TMP_PackageResourceImporter (that was a legacy standalone TMP class).
            // The ugui equivalent is TMPro.TMP_PackageUtilities.ImportEssentialResources()
            // but it is private. Call AssetDatabase.ImportPackage directly with
            // interactive:false using the package path obtained from the public
            // TMPro.EditorUtilities.TMP_EditorUtility.packageFullPath property.
            // Evidence: Library/PackageCache/com.unity.ugui@b95364aab964/Editor/TMP/
            //   TMP_PackageUtilities.cs line 1078 — the only call site for the .unitypackage.
            //   TMP_EditorUtility.cs line 34 — packageFullPath is public static.
            string packageFullPath = TMPro.EditorUtilities.TMP_EditorUtility.packageFullPath;
            AssetDatabase.ImportPackage(
                packageFullPath + "/Package Resources/TMP Essential Resources.unitypackage",
                false);
            AssetDatabase.SaveAssets();
        }
    }
}
