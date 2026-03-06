#include "SubtitleWidget.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/TextBlock.h"
#include "Blueprint/WidgetTree.h"
#include "Framework/Application/SlateApplication.h"
#include "Rendering/SlateRenderer.h"
#include "Fonts/SlateFontInfo.h"

// ── UUserWidget overrides ──────────────────────────────────────────────────────

void USubtitleWidget::NativeConstruct()
{
    Super::NativeConstruct();
    EnsureTextContainer();
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

    // Measure at scale 1.0; the layout algorithms work in logical (unscaled) pixels.
    // If your project uses a non-1.0 DPI override you may need to adjust here.
    const FVector2D Size = FontMeasure->Measure(FText::FromString(Text), FontInfo, 1.0f);
    return Size.X;
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

        UVerticalBoxSlot* Slot = TextContainer->AddChildToVerticalBox(TextBlock);
        if (Slot)
        {
            Slot->SetHorizontalAlignment(HAlign_Center);
            Slot->SetVerticalAlignment(VAlign_Center);
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
