#include "SubtitleDemoGameMode.h"
#include "SubtitleDemoHUD.h"

ASubtitleDemoGameMode::ASubtitleDemoGameMode()
{
    HUDClass = ASubtitleDemoHUD::StaticClass();
}
