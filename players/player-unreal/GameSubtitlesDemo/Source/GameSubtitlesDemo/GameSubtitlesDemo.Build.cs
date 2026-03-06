using UnrealBuildTool;

public class GameSubtitlesDemo : ModuleRules
{
    public GameSubtitlesDemo(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = ModuleRules.PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[]
        {
            "Core",
            "CoreUObject",
            "Engine",
            "InputCore",
            "Slate",
            "SlateCore",
            "UMG",
            "GameSubtitles",
        });

        PrivateDependencyModuleNames.AddRange(new string[]
        {
            "Json",
        });
    }
}
