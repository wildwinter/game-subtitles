using UnrealBuildTool;

public class GameSubtitles : ModuleRules
{
    public GameSubtitles(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = ModuleRules.PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[]
        {
            "Core",
            "CoreUObject",
            "Engine",
            "Slate",
            "SlateCore",
            "UMG",
        });

        PrivateDependencyModuleNames.AddRange(new string[]
        {
            "Json",
        });
    }
}
