using UnrealBuildTool;
using System.Collections.Generic;

public class GameSubtitlesDemoEditorTarget : TargetRules
{
    public GameSubtitlesDemoEditorTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Editor;
        DefaultBuildSettings = BuildSettingsVersion.Latest;
        IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
        ExtraModuleNames.Add("GameSubtitlesDemo");
    }
}

