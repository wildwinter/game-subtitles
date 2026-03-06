#pragma once

#include "Modules/ModuleManager.h"

class FGameSubtitlesDemoModule : public IModuleInterface
{
public:
    virtual void StartupModule() override;
    virtual void ShutdownModule() override;
};
