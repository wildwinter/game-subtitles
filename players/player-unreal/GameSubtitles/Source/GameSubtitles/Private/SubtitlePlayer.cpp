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

void USubtitlePlayer::Start(const FString& Text, float Duration)
{
    bRunning = false; // stop any current playback

    if (Renderer.GetObject())
    {
        ISubtitleRenderer::Execute_Clear(Renderer.GetObject());
    }

    Elapsed   = 0.f;
    PageIndex = 0;
    bDone     = false;

    if (!Renderer.GetObject())
    {
        return;
    }

    // Build a MeasureWidth callable that dispatches through the renderer interface
    UObject* RendererObj = Renderer.GetObject();
    TFunction<float(const FString&)> MeasureWidth = [RendererObj](const FString& T) -> float
    {
        return ISubtitleRenderer::Execute_MeasureLineWidth(RendererObj, T);
    };

    const float ContainerWidth = ISubtitleRenderer::Execute_GetContainerWidth(RendererObj);

    Pages   = FSubtitleTextLayout::WrapAndPaginate(Text, MeasureWidth, ContainerWidth, FMath::Max(1, MaxLines));
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

            if (Renderer.GetObject())
            {
                ISubtitleRenderer::Execute_Clear(Renderer.GetObject());
            }

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
        ISubtitleRenderer::Execute_Render(Renderer.GetObject(), Pages[PageIndex]);
    }
}
