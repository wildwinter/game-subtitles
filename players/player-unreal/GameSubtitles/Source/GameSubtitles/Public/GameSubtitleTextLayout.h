#pragma once

#include "CoreMinimal.h"

/**
 * Static functions for subtitle text layout: word-wrap, pagination, and timing allocation.
 *
 * Implements soft-hyphen syllable breaking, ellipsis-reservation, and proportional
 * timing allocation.
 *
 * Soft hyphens (U+00AD) mark valid syllable break-points inside words, as inserted by
 * the C# preprocessor. They are never exposed in the rendered output.
 */
class GAMESUBTITLES_API FGameSubtitleTextLayout
{
public:
    /**
     * Wraps Text and paginates it in a single pass.
     *
     * Rules:
     *   - Soft-hyphen syllable breaks are used on all non-last lines of each page.
     *   - The last line of each page only receives complete words; no word is split
     *     across a page boundary.
     *   - U+2026 (ellipsis) is appended to the last line of every non-final page.
     *   - Ellipsis space is reserved while building last-line slots so appending it
     *     never causes overflow.
     *
     * @param Text            Input text; may contain U+00AD soft hyphens.
     * @param MeasureWidth    Returns the pixel/unit width of a string in the target font.
     * @param ContainerWidth  Maximum width per line (same units as MeasureWidth output).
     * @param MaxLines        Lines per page (integer >= 1).
     * @return Pages, each an array of line strings ready for display.
     */
    static TArray<TArray<FString>> WrapAndPaginate(
        const FString& Text,
        TFunction<float(const FString&)> MeasureWidth,
        float ContainerWidth,
        int32 MaxLines,
        float FirstLineIndent = 0.f
    );

    /**
     * Wraps Text to Width with no pagination, using the same rules as WrapAndPaginate, so its
     * breaks match the subtitles'. It breaks between words, or at a soft hyphen (U+00AD) with a
     * visible '-' added, and force-breaks a word wider than a whole line as a last resort. Soft
     * hyphens not used for a break are removed. There are no pages, so there is no last-line
     * rule: a word may break at a soft hyphen on any line, and no ellipsis is added.
     *
     * Blueprint: UGameSubtitleTextLayoutLibrary::WrapLines, which measures with a font.
     *
     * @param Text          Input text; may contain U+00AD soft hyphens.
     * @param MeasureWidth  Returns the width of a string in the target font.
     * @param Width         Maximum width per line (same units as MeasureWidth output).
     * @return The lines, ready to join with newlines. Empty for blank text.
     */
    static TArray<FString> WrapLines(
        const FString& Text,
        TFunction<float(const FString&)> MeasureWidth,
        float Width
    );

    /**
     * Removes every soft hyphen (U+00AD) from Text, for a display that should only break
     * between words.
     */
    static FString WithoutSoftHyphens(const FString& Text);

    /**
     * Allocates display durations to pages proportionally by non-whitespace character
     * count, excluding U+2026 (ellipsis) which is never vocalized.
     *
     * @param Pages         Pages produced by WrapAndPaginate.
     * @param TotalDuration Total display time in seconds.
     * @return Duration in seconds per page; same length as Pages.
     */
    static TArray<float> AllocateTimings(
        const TArray<TArray<FString>>& Pages,
        float TotalDuration
    );

private:
    /**
     * Force-breaks a single word (no soft hyphens) into fragments that each fit within
     * MaxWidth, returning at least one element.
     */
    static TArray<FString> ForceBreak(
        const FString& Word,
        TFunction<float(const FString&)> MeasureWidth,
        float MaxWidth
    );

    /**
     * Returns the highest syllable index K such that
     * LineText + Sep + syllables[0..K].join('') + '-' fits within MaxWidth.
     * Returns -1 if even the first syllable prefix does not fit.
     */
    static int32 FindSyllableBreak(
        const TArray<FString>& Syllables,
        const FString& LineText,
        const FString& Sep,
        TFunction<float(const FString&)> MeasureWidth,
        float MaxWidth
    );
};
