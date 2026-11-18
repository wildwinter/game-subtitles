#include "GameSubtitleTextLayoutLibrary.h"
#include "GameSubtitleTextLayout.h"
#include "Framework/Application/SlateApplication.h"
#include "Fonts/FontMeasure.h"
#include "Rendering/SlateRenderer.h"

FString UGameSubtitleTextLayoutLibrary::WithoutSoftHyphens(const FString& Text)
{
    return FGameSubtitleTextLayout::WithoutSoftHyphens(Text);
}

TArray<FString> UGameSubtitleTextLayoutLibrary::WrapLines(const FString& Text, const FSlateFontInfo& Font, float Width)
{
    if (!FSlateApplication::IsInitialized() || !FSlateApplication::Get().GetRenderer())
    {
        // Nothing to measure with: everything fits, so this is one line.
        return FGameSubtitleTextLayout::WrapLines(Text, [](const FString&) { return 0.f; }, Width);
    }

    const TSharedRef<FSlateFontMeasure> FontMeasure =
        FSlateApplication::Get().GetRenderer()->GetFontMeasureService();
    return FGameSubtitleTextLayout::WrapLines(Text, [FontMeasure, &Font](const FString& Line)
    {
        const FVector2D Size = FontMeasure->Measure(FStringView(Line), Font, 1.f);
        return static_cast<float>(Size.X);
    }, Width);
}
