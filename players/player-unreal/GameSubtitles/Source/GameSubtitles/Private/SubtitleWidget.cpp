#include "SubtitleWidget.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/TextBlock.h"
#include "Blueprint/WidgetTree.h"
#include "Framework/Application/SlateApplication.h"
#include "Fonts/FontMeasure.h"
#include "Rendering/SlateRenderer.h"
#include "Fonts/SlateFontInfo.h"

// ── UUserWidget overrides ──────────────────────────────────────────────────────

void USubtitleWidget::NativeOnInitialized()
{
    Super::NativeOnInitialized();
    EnsureTextContainer();
}

void USubtitleWidget::NativeConstruct()
{
    Super::NativeConstruct();
}

// ── ISubtitleRenderer implementation ──────────────────────────────────────────

float USubtitleWidget::MeasureLineWidth_Implementation(const FString& Text)
{
    if (!FSlateApplication::IsInitialized() || !FSlateApplication::Get().GetRenderer())
    {
        return 0.f;
    }

    TSharedRef<FSlateFontMeasure> FontMeasure =
        FSlateApplication::Get().GetRenderer()->GetFontMeasureService();

    // Measure at the actual DPI scale so the result matches what Slate renders.
    // Measure() returns physical pixels at the given scale; dividing back gives Slate units,
    // which is the same space as GetCachedGeometry().GetLocalSize() used by GetContainerWidth().
    float Scale = GetCachedGeometry().Scale;
    if (Scale <= 0.f)
    {
        Scale = FSlateApplication::Get().GetApplicationScale();
    }
    if (Scale <= 0.f)
    {
        Scale = 1.f;
    }

    const FVector2D Size = FontMeasure->Measure(FText::FromString(Text), FontInfo, Scale);
    return Size.X / Scale;
}

float USubtitleWidget::GetContainerWidth_Implementation()
{
    if (ContainerWidthOverride > 0.f)
    {
        return ContainerWidthOverride;
    }

    const FGeometry Geom = GetCachedGeometry();
    const float     W    = Geom.GetLocalSize().X;
    return W > 0.f ? W : 540.f; // fall back to a sensible default before first layout pass
}

void USubtitleWidget::Render_Implementation(const TArray<FString>& Lines)
{
    EnsureTextContainer();
    if (!TextContainer)
    {
        return;
    }

    TextContainer->ClearChildren();

    for (const FString& Line : Lines)
    {
        UTextBlock* TextBlock = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
        if (!TextBlock)
        {
            continue;
        }

        TextBlock->SetText(FText::FromString(Line));
        TextBlock->SetFont(FontInfo);
        TextBlock->SetColorAndOpacity(FSlateColor(TextColor));
        TextBlock->SetJustification(ETextJustify::Center);
        TextBlock->SetAutoWrapText(false); // layout is already done by WrapAndPaginate

        UVerticalBoxSlot* NewSlot = TextContainer->AddChildToVerticalBox(TextBlock);
        if (NewSlot)
        {
            NewSlot->SetHorizontalAlignment(HAlign_Fill);
            NewSlot->SetVerticalAlignment(VAlign_Center);
        }
    }
}

void USubtitleWidget::Clear_Implementation()
{
    if (TextContainer)
    {
        TextContainer->ClearChildren();
    }
}

// ── Private ────────────────────────────────────────────────────────────────────

void USubtitleWidget::EnsureTextContainer()
{
    // TextContainer may already be set by BindWidgetOptional (Blueprint subclass) or a
    // previous call to this function. Only create programmatically when absent.
    if (TextContainer || !WidgetTree)
    {
        return;
    }

    TextContainer = WidgetTree->ConstructWidget<UVerticalBox>(
        UVerticalBox::StaticClass(), TEXT("TextContainer"));

    if (!TextContainer)
    {
        return;
    }

    // If no root widget has been defined (pure-C++ widget with no Blueprint), set this
    // vertical box as the root so the widget has something to display.
    if (!WidgetTree->RootWidget)
    {
        WidgetTree->RootWidget = TextContainer;
    }
}
