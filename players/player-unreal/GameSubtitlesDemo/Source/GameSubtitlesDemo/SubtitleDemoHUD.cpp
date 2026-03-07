#include "SubtitleDemoHUD.h"
#include "SubtitleDemoWidget.h"
#include "Blueprint/UserWidget.h"
#include "Engine/LocalPlayer.h"
#include "GameFramework/PlayerController.h"

void ASubtitleDemoHUD::BeginPlay()
{
    Super::BeginPlay();

    APlayerController* PC = GetOwningPlayerController();
    if (!PC)
    {
        return;
    }

    DemoWidget = CreateWidget<USubtitleDemoWidget>(PC, USubtitleDemoWidget::StaticClass());
    if (DemoWidget)
    {
        DemoWidget->AddToViewport(0);
        PC->bShowMouseCursor = true;
        PC->SetInputMode(FInputModeUIOnly());
    }
}
