using UnrealBuildTool;

// Automation tests for the GameSubtitles module. An Editor module, so it is never
// packaged into a game.
public class GameSubtitlesTests : ModuleRules
{
    public GameSubtitlesTests(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = ModuleRules.PCHUsageMode.UseExplicitOrSharedPCHs;

        PrivateDependencyModuleNames.AddRange(new string[]
        {
            "Core",
            "CoreUObject",
            "GameSubtitles",
            "Slate",
            "SlateCore",
        });
    }
}
