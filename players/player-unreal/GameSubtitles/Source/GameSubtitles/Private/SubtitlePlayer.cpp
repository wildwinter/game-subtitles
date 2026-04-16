#include "SubtitlePlayer.h"
#include "TextLayout.h"

USubtitlePlayer::USubtitlePlayer()
    : MaxLines(2)
    , PageIndex(0)
    , Elapsed(0.f)
    , bRunning(false)
    , bDone(false)
{
}

void USubtitlePlayer::Initialize(TScriptInterface<ISubtitleRenderer> InRenderer, int32 InMaxLines)
{
    Renderer = InRenderer;
    MaxLines  = FMath::Max(1, InMaxLines);
}

void USubtitlePlayer::Start(const FString& Text, float Duration,
                            const FString& CharacterName,
                            bool bHasCharacterNameColor,
                            FLinearColor CharacterNameColor,
                            bool bHasLineColor,
                            FLinearColor LineColor)
{
    bRunning = false; // stop any current playback

    if (Renderer.GetObject())
    {
        ISubtitleRenderer::Execute_Clear(Renderer.GetObject());
    }

    Elapsed   = 0.f;
    PageIndex = 0;
    bDone     = false;

    // Build CurrentCharacterContext
    CurrentCharacterContext = FSubtitleCharacterContext();
    if (!CharacterName.IsEmpty())
    {
        CurrentCharacterContext.bValid     = true;
        CurrentCharacterContext.Name       = CharacterName;
        CurrentCharacterContext.bBold      = bBoldCharacterName;
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
        return ISubtitleRenderer::Execute_MeasureLineWidth(RendererObj, T, /*bBold=*/false);
    };

    const float ContainerWidth = ISubtitleRenderer::Execute_GetContainerWidth(RendererObj);

    // Reserve space on line 0 of each page for the bold character-name prefix
    float FirstLineIndent = 0.f;
    if (CurrentCharacterContext.bValid)
    {
        const float RawIndent = ISubtitleRenderer::Execute_MeasureLineWidth(
            RendererObj, CharacterName + TEXT(": "), bBoldCharacterName);
        FirstLineIndent = FMath::CeilToFloat(RawIndent);
    }

    Pages   = FSubtitleTextLayout::WrapAndPaginate(Text, MeasureWidth, ContainerWidth,
                                                    FMath::Max(1, MaxLines), FirstLineIndent);
    Timings = FSubtitleTextLayout::AllocateTimings(Pages, Duration);

    bRunning = true;
    RenderCurrent();
}

void USubtitlePlayer::Tick(float DeltaSeconds)
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

void USubtitlePlayer::Stop()
{
    bRunning = false;

    if (Renderer.GetObject())
    {
        ISubtitleRenderer::Execute_Clear(Renderer.GetObject());
    }
}

void USubtitlePlayer::Reset()
{
    bRunning  = false;
    bDone     = false;
    PageIndex = 0;
    Elapsed   = 0.f;
    Pages.Empty();
    Timings.Empty();

    if (Renderer.GetObject())
    {
        ISubtitleRenderer::Execute_Clear(Renderer.GetObject());
    }
}

void USubtitlePlayer::RenderCurrent()
{
    if (Renderer.GetObject() && Pages.IsValidIndex(PageIndex))
    {
        ISubtitleRenderer::Execute_Render(Renderer.GetObject(), Pages[PageIndex], CurrentCharacterContext);
    }
}
