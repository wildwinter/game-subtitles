#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "GameSubtitlesVersion.generated.h"

/** This plugin's version, as in its .uplugin's VersionName. Written by the build; never edit it by hand. */
inline constexpr const char* GameSubtitlesVersion = "0.4.0";

/**
 * Which Game Subtitles this is. A plugin that ships its own copy of Game Subtitles can read it at
 * start-up to tell when the game is running a different copy.
 *
 * C++ code can read GameSubtitlesVersion directly; Blueprint calls GetGameSubtitlesVersion.
 */
UCLASS()
class GAMESUBTITLES_API UGameSubtitlesVersionLibrary : public UBlueprintFunctionLibrary
{
    GENERATED_BODY()

public:
    /** This plugin's version, as in its .uplugin's VersionName ("0.4.0"). */
    UFUNCTION(BlueprintPure, Category = "Game Subtitles|Version")
    static FString GetGameSubtitlesVersion() { return UTF8_TO_TCHAR(GameSubtitlesVersion); }
};
