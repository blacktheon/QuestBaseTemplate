using System.Collections.Generic;

namespace Immrsiv.QuestSetup.Editor
{
    /// <summary>A single project-configuration requirement with a one-click fix.</summary>
    public abstract class SetupCheck
    {
        public abstract string Label { get; }
        /// <summary>Advisory checks report but never fail Fix All / CI.</summary>
        public virtual bool IsAdvisory => false;
        /// <summary>True when the project already satisfies the requirement.
        /// details always describes current vs expected state.</summary>
        public abstract bool Evaluate(out string details);
        public abstract void Fix();
    }

    public static class SetupCheckRegistry
    {
        public static List<SetupCheck> CreateAll() => new List<SetupCheck>
        {
            // Order = Fix All execution order. Tasks 2-8 append here.
            new ViveLayerKillSwitchCheck(),
            new XRLoaderCheck(),
            new OpenXRFeaturesCheck(),
            new RenderModeCheck(),
            new MetaProjectSetupCheck(),
            new ColorSpaceCheck(),
            new AndroidPlayerCheck(),
            new SamplesImportedCheck(),
            new TmpEssentialsCheck(),
            new BaselineVersionsCheck(),
        };
    }
}
