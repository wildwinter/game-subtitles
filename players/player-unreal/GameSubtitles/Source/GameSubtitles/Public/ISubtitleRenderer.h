#pragma once

#include "CoreMinimal.h"
#include "UObject/Interface.h"
#include "ISubtitleRenderer.generated.h"

/**
 * Optional character-name styling passed to ISubtitleRenderer::Render for the first
 * line of every page.  Set bValid = false (the default) to render without a prefix.
 */
USTRUCT(BlueprintType)
struct GAMESUBTITLES_API FSubtitleCharacterContext
{
    GENERATED_BODY()

    /** When false this context carries no character name; all other fields are ignored. */
    UPROPERTY(BlueprintReadWrite, Category = "Subtitles")
    bool bValid = false;

    /** The character name to display (e.g. "Aria"). */
    UPROPERTY(BlueprintReadWrite, Category = "Subtitles")
    FString Name;

    /** Colour for the name prefix. Only used when bHasColor is true. */
    UPROPERTY(BlueprintReadWrite, Category = "Subtitles")
    FLinearColor Color = FLinearColor::White;

    /** When false the renderer uses its default text colour for the name. */
    UPROPERTY(BlueprintReadWrite, Category = "Subtitles")
    bool bHasColor = false;

    /** Whether the name prefix should be rendered in bold. */
    UPROPERTY(BlueprintReadWrite, Category = "Subtitles")
    bool bBold = true;

    /** Colour for the subtitle body text on all lines. Only used when bHasLineColor is true. */
    UPROPERTY(BlueprintReadWrite, Category = "Subtitles")
    FLinearColor LineColor = FLinearColor::White;

    /** When false the renderer uses its default text colour for body lines. */
    UPROPERTY(BlueprintReadWrite, Category = "Subtitles")
    bool bHasLineColor = false;
};

/**
 * Interface that a subtitle renderer must implement.
 *
 *   MeasureLineWidth(text, bBold) -> float
 *   GetContainerWidth()           -> float
 *   Render(lines, charContext)    -> void
 *   Clear()                       -> void
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
     * Pass bBold = true to measure in bold weight (e.g. for a character-name prefix).
     * Called frequently during layout — keep implementations fast.
     */
    UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category = "Subtitles|Renderer")
    float MeasureLineWidth(const FString& Text, bool bBold);

    /**
     * Returns the maximum line width available to the renderer (pixel / Slate unit width
     * of the text area).
     */
    UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category = "Subtitles|Renderer")
    float GetContainerWidth();

    /**
     * Display the given lines. Lines is a page from WrapAndPaginate; typically 1-3
     * strings, already soft-hyphen-resolved and ellipsis-appended where needed.
     * When CharacterContext.bValid is true the renderer should prepend "Name: " (styled
     * per the context) to the first line.
     */
    UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category = "Subtitles|Renderer")
    void Render(const TArray<FString>& Lines, const FSubtitleCharacterContext& CharacterContext);

    /**
     * Remove all displayed content (called between pages and on stop/reset).
     */
    UFUNCTION(BlueprintNativeEvent, BlueprintCallable, Category = "Subtitles|Renderer")
    void Clear();
};
