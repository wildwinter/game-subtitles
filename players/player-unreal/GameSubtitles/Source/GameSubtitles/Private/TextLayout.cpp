#include "TextLayout.h"

// ── Constants ──────────────────────────────────────────────────────────────────

static const FString SoftHyphen(TEXT("\u00AD")); // U+00AD SOFT HYPHEN
static const FString Ellipsis(TEXT("\u2026"));   // U+2026 HORIZONTAL ELLIPSIS

// ── Private helpers ────────────────────────────────────────────────────────────

TArray<FString> FSubtitleTextLayout::ForceBreak(
    const FString& Word,
    TFunction<float(const FString&)> MeasureWidth,
    float MaxWidth)
{
    TArray<FString> Lines;
    FString Current;

    for (int32 i = 0; i < Word.Len(); ++i)
    {
        FString Next = Current + Word[i];
        if (MeasureWidth(Next) <= MaxWidth)
        {
            Current = Next;
        }
        else
        {
            if (!Current.IsEmpty())
            {
                Lines.Add(Current);
            }
            Current = FString(1, &Word[i]);
        }
    }

    if (!Current.IsEmpty())
    {
        Lines.Add(Current);
    }

    return Lines.Num() > 0 ? Lines : TArray<FString>{ Word };
}

int32 FSubtitleTextLayout::FindSyllableBreak(
    const TArray<FString>& Syllables,
    const FString& LineText,
    const FString& Sep,
    TFunction<float(const FString&)> MeasureWidth,
    float MaxWidth)
{
    FString Acc;
    int32 Last = -1;

    // Test all syllable prefixes except the final one (last syllable cannot be broken off alone)
    for (int32 k = 0; k < Syllables.Num() - 1; ++k)
    {
        Acc += Syllables[k];
        if (MeasureWidth(LineText + Sep + Acc + TEXT("-")) <= MaxWidth)
        {
            Last = k;
        }
        else
        {
            // Prefixes only grow, so no later prefix can fit
            break;
        }
    }

    return Last;
}

// ── Public API ─────────────────────────────────────────────────────────────────

TArray<TArray<FString>> FSubtitleTextLayout::WrapAndPaginate(
    const FString& Text,
    TFunction<float(const FString&)> MeasureWidth,
    float ContainerWidth,
    int32 MaxLines)
{
    const float EllipsisWidth = MeasureWidth(Ellipsis);

    // Split on whitespace, culling empty tokens
    TArray<FString> RawWords;
    Text.ParseIntoArrayWS(RawWords, nullptr, true);

    if (RawWords.Num() == 0)
    {
        return { TArray<FString>() };
    }

    // Mutable copy — the algorithm replaces words with soft-hyphen remainders in-place
    TArray<FString> Words = RawWords;

    TArray<TArray<FString>> Pages;
    TArray<FString> PageLines;
    FString LineText;
    int32 LineSlot = 0; // 0-indexed line position within the current page

    // Advance the current line, starting a new page when the last slot is filled
    auto AdvanceLine = [&]()
    {
        PageLines.Add(LineText);
        LineText.Empty();
        if (LineSlot == MaxLines - 1)
        {
            Pages.Add(PageLines);
            PageLines.Empty();
            LineSlot = 0;
        }
        else
        {
            ++LineSlot;
        }
    };

    int32 wi = 0;
    while (wi < Words.Num())
    {
        const bool  bIsLastSlot    = (LineSlot == MaxLines - 1);
        const float EffectiveWidth = bIsLastSlot ? (ContainerWidth - EllipsisWidth) : ContainerWidth;

        // Split current word on soft hyphens to get syllables
        TArray<FString> Syllables;
        Words[wi].ParseIntoArray(Syllables, *SoftHyphen, false);
        const FString Clean        = FString::Join(Syllables, TEXT(""));
        const bool    bHasSyllables = Syllables.Num() > 1;
        const FString Sep          = LineText.IsEmpty() ? TEXT("") : TEXT(" ");

        // 1. Full word fits within the effective width
        if (MeasureWidth(LineText + Sep + Clean) <= EffectiveWidth)
        {
            LineText += Sep + Clean;
            ++wi;
            continue;
        }

        // 2. Syllable-prefix hyphenation — only on non-last slots with content
        if (!bIsLastSlot && bHasSyllables && !LineText.IsEmpty())
        {
            const int32 BreakAt = FindSyllableBreak(Syllables, LineText, Sep, MeasureWidth, EffectiveWidth);
            if (BreakAt >= 0)
            {
                // Build prefix fragment
                TArray<FString> Prefix(Syllables.GetData(), BreakAt + 1);
                TArray<FString> Remainder;
                for (int32 i = BreakAt + 1; i < Syllables.Num(); ++i)
                {
                    Remainder.Add(Syllables[i]);
                }

                LineText += Sep + FString::Join(Prefix, TEXT("")) + TEXT("-");
                Words[wi] = FString::Join(Remainder, *SoftHyphen);
                AdvanceLine();
                continue;
            }
        }

        // 3. Flush the current line (if non-empty) and retry the word
        if (!LineText.IsEmpty())
        {
            AdvanceLine();
            continue;
        }

        // 4. Line is empty on a last slot with prior lines: close the page so the word
        //    retries at slot 0 of a fresh page where syllable-breaking is allowed
        if (bIsLastSlot && PageLines.Num() > 0)
        {
            Pages.Add(PageLines);
            PageLines.Empty();
            LineSlot = 0;
            continue;
        }

        // 5. Line is empty, non-last slot: try syllable breaking from the start of the line
        if (!bIsLastSlot && bHasSyllables)
        {
            const int32 BreakAt = FindSyllableBreak(Syllables, TEXT(""), TEXT(""), MeasureWidth, EffectiveWidth);
            if (BreakAt >= 0)
            {
                TArray<FString> Prefix(Syllables.GetData(), BreakAt + 1);
                TArray<FString> Remainder;
                for (int32 i = BreakAt + 1; i < Syllables.Num(); ++i)
                {
                    Remainder.Add(Syllables[i]);
                }

                LineText  = FString::Join(Prefix, TEXT("")) + TEXT("-");
                Words[wi] = FString::Join(Remainder, *SoftHyphen);
                AdvanceLine();
                continue;
            }
        }

        // 6. Character-level break as a last resort
        //    Use EffectiveWidth on last slots so the subsequently appended ellipsis always fits
        const TArray<FString> Broken = ForceBreak(
            Clean,
            MeasureWidth,
            bIsLastSlot ? EffectiveWidth : ContainerWidth);

        for (int32 bi = 0; bi < Broken.Num() - 1; ++bi)
        {
            LineText = Broken[bi];
            AdvanceLine();
        }
        LineText = Broken.Last();
        ++wi;
    }

    // Flush any remaining content
    if (!LineText.IsEmpty())
    {
        PageLines.Add(LineText);
    }
    if (PageLines.Num() > 0)
    {
        Pages.Add(PageLines);
    }
    if (Pages.Num() == 0)
    {
        return { TArray<FString>() };
    }

    // Append ellipsis to the last line of every non-final page.
    // Those lines were built with EffectiveWidth, so the ellipsis always fits.
    for (int32 pi = 0; pi < Pages.Num() - 1; ++pi)
    {
        Pages[pi].Last() += Ellipsis;
    }

    // Last-line word reconstitution: if the very last line of the last page is a single
    // token (the tail of a soft-hyphen break) and the preceding line ends with the matching
    // hyphenated stem, rejoin the whole word when it fits within ContainerWidth.
    TArray<FString>& LastPage = Pages.Last();
    if (LastPage.Num() >= 2)
    {
        const FString& LastLine = LastPage.Last();
        if (!LastLine.Contains(TEXT(" ")))
        {
            TArray<FString> PrevTokens;
            LastPage[LastPage.Num() - 2].ParseIntoArray(PrevTokens, TEXT(" "), true);

            if (PrevTokens.Num() > 0 && PrevTokens.Last().EndsWith(TEXT("-")))
            {
                const FString Rejoined = PrevTokens.Last().LeftChop(1) + LastLine;
                if (MeasureWidth(Rejoined) <= ContainerWidth)
                {
                    const int32 LastIdx = LastPage.Num() - 1;
                    const int32 PrevIdx = LastPage.Num() - 2;

                    PrevTokens.Pop(); // remove the hyphenated stem
                    LastPage[LastIdx] = Rejoined;

                    if (PrevTokens.Num() > 0)
                    {
                        LastPage[PrevIdx] = FString::Join(PrevTokens, TEXT(" "));
                    }
                    else
                    {
                        LastPage.RemoveAt(PrevIdx);
                    }
                }
            }
        }
    }

    return Pages;
}

TArray<float> FSubtitleTextLayout::AllocateTimings(
    const TArray<TArray<FString>>& Pages,
    float TotalDuration)
{
    TArray<int32> Counts;
    Counts.Reserve(Pages.Num());

    for (const TArray<FString>& Page : Pages)
    {
        // Concatenate all lines for this page, then count non-whitespace, non-ellipsis chars
        FString PageText;
        for (const FString& Line : Page)
        {
            PageText += Line;
        }

        int32 Count = 0;
        for (int32 i = 0; i < PageText.Len(); ++i)
        {
            const TCHAR Ch = PageText[i];
            if (!FChar::IsWhitespace(Ch) && Ch != TEXT('\u2026'))
            {
                ++Count;
            }
        }

        // Guard against empty pages producing a zero denominator
        Counts.Add(FMath::Max(1, Count));
    }

    int32 Total = 0;
    for (int32 C : Counts)
    {
        Total += C;
    }

    TArray<float> Timings;
    Timings.Reserve(Counts.Num());
    for (int32 C : Counts)
    {
        Timings.Add((static_cast<float>(C) / static_cast<float>(Total)) * TotalDuration);
    }

    return Timings;
}
