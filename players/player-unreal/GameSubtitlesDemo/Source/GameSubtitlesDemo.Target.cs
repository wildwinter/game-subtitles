using UnrealBuildTool;
using System.Collections.Generic;

public class GameSubtitlesDemoTarget : TargetRules
{
    public GameSubtitlesDemoTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Game;
        DefaultBuildSettings = BuildSettingsVersion.Latest;
        IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
        ExtraModuleNames.Add("GameSubtitlesDemo");
    }
}
