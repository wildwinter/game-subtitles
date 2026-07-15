#include "GameSubtitlePlayer.h"
#include "GameSubtitleTextLayout.h"

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

	float DurationUsed = Duration;
	if (DurationUsed <= 0.f)	// Let's guess from the text length
	{
		FString CleanText = Text.Replace(TEXT("\u00AD"), TEXT(""));
		const int32 CharCount = CleanText.Len();
		DurationUsed = FMath::Clamp(FMath::RoundToFloat(CharCount / 14.f), 3.f, 18.f);
	}

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
