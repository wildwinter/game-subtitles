#include "GameSubtitlePlayer.h"
#include "GameSubtitleTextLayout.h"

float UGameSubtitlePlayer::EstimateDuration(const FString& Text, float CharsPerSecond,
                                           float MinSeconds, float MaxSeconds)
{
    int32 Count = 0;
    const int32 Len = Text.Len();
    for (int32 i = 0; i < Len; ++i)
    {
        const TCHAR Ch = Text[i];
        if (Ch == (TCHAR)0x00AD)
        {
            continue;
        }
        // Count a UTF-16 surrogate pair once, as a single code point. Never true where TCHAR is UTF-32.
        if (Ch >= 0xD800 && Ch <= 0xDBFF && i + 1 < Len && Text[i + 1] >= 0xDC00 && Text[i + 1] <= 0xDFFF)
        {
            ++i;
        }
        ++Count;
    }

    const float Rate = CharsPerSecond > 0.f ? CharsPerSecond : 14.f;
    return FMath::Min(FMath::Max(Count / Rate, MinSeconds), MaxSeconds);
}

UGameSubtitlePlayer::UGameSubtitlePlayer()
    : MaxLines(2)
    , PageIndex(0)
    , Elapsed(0.f)
    , bRunning(false)
    , bDone(false)
{
}

void UGameSubtitlePlayer::Initialize(TScriptInterface<IGameSubtitleRenderer> InRenderer, int32 InMaxLines)
{
    Renderer = InRenderer;
    MaxLines  = FMath::Max(1, InMaxLines);
}

void UGameSubtitlePlayer::Start(const FString& Text, float Duration,
                            const FString& CharacterName,
                            bool bHasCharacterNameColor,
                            FLinearColor CharacterNameColor,
                            bool bHasLineColor,
                            FLinearColor LineColor)
{
    bRunning = false; // stop any current playback

    if (Renderer.GetObject())
    {
        IGameSubtitleRenderer::Execute_Clear(Renderer.GetObject());
    }

    Elapsed   = 0.f;
    PageIndex = 0;
    bDone     = false;

    // Build CurrentCharacterContext
    CurrentCharacterContext = FGameSubtitleCharacterContext();
    if (!CharacterName.IsEmpty())
    {
        CurrentCharacterContext.bValid     = true;
        CurrentCharacterContext.Name       = CharacterName;
        CurrentCharacterContext.bHasColor  = bHasCharacterNameColor;
        CurrentCharacterContext.Color      = CharacterNameColor;
    }
    CurrentCharacterContext.bHasLineColor = bHasLineColor;
    CurrentCharacterContext.LineColor     = LineColor;

    if (!Renderer.GetObject())
    {
        return;
    }

    UObject* RendererObj = Renderer.GetObject();

    // Build a MeasureWidth callable that dispatches through the renderer interface
    TFunction<float(const FString&)> MeasureWidth = [RendererObj](const FString& T) -> float
    {
        return IGameSubtitleRenderer::Execute_MeasureLineWidth(RendererObj, T, /*bUseCharacterNameFont=*/false);
    };

    const float ContainerWidth = IGameSubtitleRenderer::Execute_GetContainerWidth(RendererObj);

    // Reserve space on line 0 of each page for the character-name prefix, measured
    // in the character-name font so the body text always fits alongside it.
    float FirstLineIndent = 0.f;
    if (CurrentCharacterContext.bValid)
    {
        const float RawIndent = IGameSubtitleRenderer::Execute_MeasureLineWidth(
            RendererObj, CharacterName + TEXT(": "), /*bUseCharacterNameFont=*/true);
        FirstLineIndent = FMath::CeilToFloat(RawIndent);
    }

    const float DurationUsed = Duration > 0.f ? Duration : EstimateDuration(Text);

    Pages   = FGameSubtitleTextLayout::WrapAndPaginate(Text, MeasureWidth, ContainerWidth,
                                                    FMath::Max(1, MaxLines), FirstLineIndent);
    Timings = FGameSubtitleTextLayout::AllocateTimings(Pages, DurationUsed);

    bRunning = true;
    RenderCurrent();
}

void UGameSubtitlePlayer::Tick(float DeltaSeconds)
{
    if (!bRunning || bDone)
    {
        return;
    }

    Elapsed += DeltaSeconds;

    while (Elapsed >= Timings[PageIndex])
    {
        Elapsed -= Timings[PageIndex];
        ++PageIndex;

        if (PageIndex >= Pages.Num())
        {
            bDone    = true;
            bRunning = false;
            // Hold the last page visible - caller is responsible for clearing via Stop().
            OnComplete.Broadcast();
            return;
        }

        RenderCurrent();
    }
}

void UGameSubtitlePlayer::Stop()
{
    bRunning = false;

    if (Renderer.GetObject())
    {
        IGameSubtitleRenderer::Execute_Clear(Renderer.GetObject());
    }
}

void UGameSubtitlePlayer::Reset()
{
    bRunning  = false;
    bDone     = false;
    PageIndex = 0;
    Elapsed   = 0.f;
    Pages.Empty();
    Timings.Empty();

    if (Renderer.GetObject())
    {
        IGameSubtitleRenderer::Execute_Clear(Renderer.GetObject());
    }
}

void UGameSubtitlePlayer::RenderCurrent()
{
    if (Renderer.GetObject() && Pages.IsValidIndex(PageIndex))
    {
        // Pad to MaxLines so the renderer always receives a full-height page.
        // A single space per padding line is invisible when centered but guarantees
        // the text block reports a non-zero line height from the font metrics.
        TArray<FString> PaddedLines = Pages[PageIndex];
        while (PaddedLines.Num() < MaxLines)
        {
            PaddedLines.Add(TEXT(" "));
        }
        IGameSubtitleRenderer::Execute_Render(Renderer.GetObject(), PaddedLines, CurrentCharacterContext);
    }
}
