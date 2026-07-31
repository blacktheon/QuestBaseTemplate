using UnityEditor;
using UnityEngine;

namespace Immrsiv.QuestSetup.Editor
{
    public class ColorSpaceCheck : SetupCheck
    {
        public override string Label => "Color space: Linear";

        public override bool Evaluate(out string details)
        {
            details = $"Color space is {PlayerSettings.colorSpace} (want Linear).";
            return PlayerSettings.colorSpace == ColorSpace.Linear;
        }

        public override void Fix()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.SaveAssets();
        }
    }
}
