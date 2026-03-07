#pragma once

#include "CoreMinimal.h"
#include "UObject/Interface.h"
#include "ISubtitleRenderer.generated.h"

/**
 * Interface that a subtitle renderer must implement.
 *
 *   MeasureLineWidth(text) -> float
 *   GetContainerWidth()    -> float
 *   Render(lines)          -> void
 *   Clear()                -> void
 *
 * Implement this interface on any UObject (e.g. a UUserWidget subclass) to plug it
 * into USubtitlePlayer. Both C++ and Blueprint implementations are supported.
 */
UINTERFACE(MinimalAPI, BlueprintType)
class USubtitleRenderer : public UInterface
{
    GENERATED_BODY()
};

class GAMESUBTITLES_API ISubtitleRenderer
{
    GENERATED_BODY()

public:
    /**
     * Returns the width of Text rendered in the renderer's current font.
     * Called frequently during layout — keep implementations fast.
     */
    UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category = "Subtitles|Renderer")
    float MeasureLineWidth(const FString& Text);

    /**
     * Returns the maximum line width available to the renderer (pixel / Slate unit width
     * of the text area).
     */
    UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category = "Subtitles|Renderer")
    float GetContainerWidth();

    /**
     * Display the given lines. Lines is a page from WrapAndPaginate; typically 1-3
     * strings, already soft-hyphen-resolved and ellipsis-appended where needed.
     */
    UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category = "Subtitles|Renderer")
    void Render(const TArray<FString>& Lines);

    /**
     * Remove all displayed content (called between pages and on stop/reset).
     */
    UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category = "Subtitles|Renderer")
    void Clear();
};
