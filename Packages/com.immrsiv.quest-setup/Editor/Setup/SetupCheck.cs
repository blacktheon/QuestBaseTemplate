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
            // Order = Fix All execution order (spec §D, v1.1).
            // BuildTargetCheck runs first so all later per-target checks evaluate
            // against the correct active target.  RenderModeCheck runs AFTER
            // MetaProjectSetupCheck and ProjectValidationCheck so our policy always
            // wins (we suppress Meta's SPI recommendation on Standalone via ignore).
            new BuildTargetCheck(),
            new ViveLayerKillSwitchCheck(),
            new XRLoaderCheck(),
            new OpenXRFeaturesCheck(),
            new MetaProjectSetupCheck(),
            new ProjectValidationCheck(),
            new RenderModeCheck(),
            new ColorSpaceCheck(),
            new AndroidPlayerCheck(),
            new SamplesImportedCheck(),
            new TmpEssentialsCheck(),
            new BaselineVersionsCheck(),
        };
    }
}
