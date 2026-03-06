#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "SubtitleDemoGameMode.generated.h"

/**
 * Game mode for the subtitle demo. Sets the default HUD to SubtitleDemoHUD,
 * which in turn creates and adds the USubtitleDemoWidget to the viewport.
 */
UCLASS()
class GAMESUBTITLESDEMO_API ASubtitleDemoGameMode : public AGameModeBase
{
    GENERATED_BODY()

public:
    ASubtitleDemoGameMode();
};
