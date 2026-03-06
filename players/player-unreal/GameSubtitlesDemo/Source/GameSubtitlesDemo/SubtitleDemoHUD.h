#pragma once

#include "CoreMinimal.h"
#include "GameFramework/HUD.h"
#include "SubtitleDemoHUD.generated.h"

class USubtitleDemoWidget;

/**
 * Minimal HUD that creates the SubtitleDemoWidget and adds it to the viewport
 * when the game begins.
 */
UCLASS()
class GAMESUBTITLESDEMO_API ASubtitleDemoHUD : public AHUD
{
    GENERATED_BODY()

public:
    virtual void BeginPlay() override;

private:
    UPROPERTY()
    USubtitleDemoWidget* DemoWidget = nullptr;
};
