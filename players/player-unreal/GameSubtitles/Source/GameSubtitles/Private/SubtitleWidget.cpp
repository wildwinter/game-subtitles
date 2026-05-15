#include "SubtitleWidget.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Components/TextBlock.h"
#include "Components/Spacer.h"
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

float USubtitleWidget::MeasureLineWidth_Implementation(const FString& Text, bool bBold)
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

    const FSlateFontInfo& MeasureFont = (bBold && BoldFontInfo.HasValidFont()) ? BoldFontInfo : FontInfo;
    const FVector2D Size = FontMeasure->Measure(FText::FromString(Text), MeasureFont, Scale);
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

void USubtitleWidget::Render_Implementation(const TArray<FString>& Lines, const FSubtitleCharacterContext& CharacterContext)
{
    EnsureTextContainer();
    if (!TextContainer)
    {
        return;
    }

    TextContainer->ClearChildren();

    for (int32 i = 0; i < Lines.Num(); ++i)
    {
        if (i == 0 && CharacterContext.bValid)
        {
            // Line 0 with a character prefix: build a horizontal box so the name and
            // body text sit side-by-side and the whole line can be centered as a unit.
            UHorizontalBox* HBox = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
            if (!HBox)
            {
                continue;
            }

            const FSlateFontInfo& NameFont   = (CharacterContext.bBold && BoldFontInfo.HasValidFont())
                                                 ? BoldFontInfo : FontInfo;
            const FSlateColor     NameColor  = CharacterContext.bHasColor
                                                 ? FSlateColor(CharacterContext.Color)
                                                 : FSlateColor(TextColor);

            // Lead fill-spacer: expands to push name+body to center
            if (USpacer* LeadSpacer = WidgetTree->ConstructWidget<USpacer>(USpacer::StaticClass()))
            {
                UHorizontalBoxSlot* SpacerSlot = HBox->AddChildToHorizontalBox(LeadSpacer);
                if (SpacerSlot)
                    SpacerSlot->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
            }

            // Name prefix text block
            UTextBlock* NameBlock = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
            if (NameBlock)
            {
                NameBlock->SetText(FText::FromString(CharacterContext.Name + TEXT(": ")));
                NameBlock->SetFont(NameFont);
                NameBlock->SetColorAndOpacity(NameColor);
                NameBlock->SetAutoWrapText(false);

                UHorizontalBoxSlot* NameSlot = HBox->AddChildToHorizontalBox(NameBlock);
                if (NameSlot)
                {
                    NameSlot->SetSize(FSlateChildSize(ESlateSizeRule::Automatic));
                    NameSlot->SetHorizontalAlignment(HAlign_Left);
                    NameSlot->SetVerticalAlignment(VAlign_Center);
                }
            }

            // Line body text block
            UTextBlock* LineBlock = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
            if (LineBlock)
            {
                LineBlock->SetText(FText::FromString(Lines[i]));
                LineBlock->SetFont(FontInfo);
                const FSlateColor BodyColor = CharacterContext.bHasLineColor
                    ? FSlateColor(CharacterContext.LineColor) : FSlateColor(TextColor);
                LineBlock->SetColorAndOpacity(BodyColor);
                LineBlock->SetAutoWrapText(false);

                UHorizontalBoxSlot* LineSlot = HBox->AddChildToHorizontalBox(LineBlock);
                if (LineSlot)
                {
                    LineSlot->SetSize(FSlateChildSize(ESlateSizeRule::Automatic));
                    LineSlot->SetHorizontalAlignment(HAlign_Left);
                    LineSlot->SetVerticalAlignment(VAlign_Center);
                }
            }

            // Trail fill-spacer: mirrors the lead spacer, completing the centering
            if (USpacer* TrailSpacer = WidgetTree->ConstructWidget<USpacer>(USpacer::StaticClass()))
            {
                UHorizontalBoxSlot* SpacerSlot = HBox->AddChildToHorizontalBox(TrailSpacer);
                if (SpacerSlot)
                    SpacerSlot->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
            }

            // HBox fills the full TextContainer width; spacers handle centering without
            // depending on the HBox's own measured width (which avoids a one-frame position
            // jump when Slate hasn't yet measured the new STextBlock children).
            UVerticalBoxSlot* VSlot = TextContainer->AddChildToVerticalBox(HBox);
            if (VSlot)
            {
                VSlot->SetHorizontalAlignment(HAlign_Fill);
                VSlot->SetVerticalAlignment(VAlign_Center);
            }
        }
        else
        {
            UTextBlock* TextBlock = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
            if (!TextBlock)
            {
                continue;
            }

            TextBlock->SetText(FText::FromString(Lines[i]));
            TextBlock->SetFont(FontInfo);
            const FSlateColor BodyColor = CharacterContext.bHasLineColor
                ? FSlateColor(CharacterContext.LineColor) : FSlateColor(TextColor);
            TextBlock->SetColorAndOpacity(BodyColor);
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
