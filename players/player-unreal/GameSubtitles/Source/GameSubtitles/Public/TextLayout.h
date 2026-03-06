#pragma once

#include "CoreMinimal.h"

/**
 * Static functions for subtitle text layout: word-wrap, pagination, and timing allocation.
 *
 * Direct port of the player-js TextLayout.js algorithms. The same soft-hyphen syllable
 * breaking rules, ellipsis-reservation, and proportional timing allocation apply.
 *
 * Soft hyphens (U+00AD) mark valid syllable break-points inside words, as inserted by
 * the C# preprocessor. They are never exposed in the rendered output.
 */
class GAMESUBTITLES_API FSubtitleTextLayout
{
public:
    /**
     * Wraps Text and paginates it in a single pass.
     *
     * Rules (identical to player-js):
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
        int32 MaxLines
    );

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
