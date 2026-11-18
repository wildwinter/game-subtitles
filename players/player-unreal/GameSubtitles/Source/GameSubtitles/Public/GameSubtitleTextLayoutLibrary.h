#pragma once

#include "CoreMinimal.h"
#include "Fonts/SlateFontInfo.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "GameSubtitleTextLayoutLibrary.generated.h"

/**
 * Blueprint access to the soft-hyphen helpers in FGameSubtitleTextLayout, for text shown
 * outside the subtitles (speech bubbles, menus, journals) once the preprocessor has added
 * soft hyphens (U+00AD) to it.
 */
UCLASS()
class GAMESUBTITLES_API UGameSubtitleTextLayoutLibrary : public UBlueprintFunctionLibrary
{
    GENERATED_BODY()

public:
    /** Removes every soft hyphen (U+00AD) from Text, for a display that should only break between words. */
    UFUNCTION(BlueprintPure, Category = "Game Subtitles|Text Layout")
    static FString WithoutSoftHyphens(const FString& Text);

    /**
     * Wraps Text to Width with the same breaks as the subtitles: between words, or at a soft
     * hyphen with a visible '-' added, and a forced break inside a word wider than a whole line.
     * Unused soft hyphens are removed, and a word may break on any line. Measures with Slate's
     * font measure service in Font, at a scale of 1, so Width is in Slate units. Returns the
     * lines, ready to join with newlines into a text block with its own wrapping off; empty for
     * blank text. Without a Slate renderer (a dedicated server, say) nothing can be measured,
     * so it returns the text as one line, soft hyphens removed.
     */
    UFUNCTION(BlueprintPure, Category = "Game Subtitles|Text Layout")
    static TArray<FString> WrapLines(const FString& Text, const FSlateFontInfo& Font, float Width);
};
