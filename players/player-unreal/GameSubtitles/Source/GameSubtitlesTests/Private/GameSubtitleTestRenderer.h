#pragma once

#include "CoreMinimal.h"
#include "UObject/Object.h"
#include "IGameSubtitleRenderer.h"
#include "GameSubtitleTestRenderer.generated.h"

/**
 * A renderer for tests: a 100-wide container and 10-wide monospace characters, which
 * records every call. It also counts the player's OnComplete broadcasts, which can
 * only be bound to a UFUNCTION.
 */
UCLASS()
class UGameSubtitleTestRenderer : public UObject, public IGameSubtitleRenderer
{
    GENERATED_BODY()

public:
    struct FMeasured
    {
        FString Text;
        bool    bUseCharacterNameFont;
    };

    TArray<TArray<FString>>              Rendered;
    TArray<FGameSubtitleCharacterContext> Contexts;
    TArray<FMeasured>                    Measured;
    int32                                Cleared   = 0;
    int32                                Completed = 0;

    virtual float MeasureLineWidth_Implementation(const FString& Text, bool bUseCharacterNameFont) override
    {
        Measured.Add({ Text, bUseCharacterNameFont });
        return Text.Len() * 10.f;
    }

    virtual float GetContainerWidth_Implementation() override
    {
        return 100.f;
    }

    virtual void Render_Implementation(const TArray<FString>& Lines,
                                       const FGameSubtitleCharacterContext& CharacterContext) override
    {
        Rendered.Add(Lines);
        Contexts.Add(CharacterContext);
    }

    virtual void Clear_Implementation() override
    {
        ++Cleared;
    }

    UFUNCTION()
    void HandleComplete()
    {
        ++Completed;
    }
};
