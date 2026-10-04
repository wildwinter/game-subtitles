#include "Misc/AutomationTest.h"
#include "GameSubtitleWidget.h"
#include "Framework/Application/SlateApplication.h"
#include "Fonts/FontMeasure.h"
#include "Rendering/SlateRenderer.h"
#include "Styling/CoreStyle.h"
#include "Widgets/Text/STextBlock.h"
#include "UObject/StrongObjectPtr.h"

#if WITH_DEV_AUTOMATION_TESTS

// The widget draws "Name: " and the body as separate text blocks, so the name block must be
// as wide as the "Name: " the player measures to reserve its space, trailing space included.
// (TextMeshPro drops trailing spaces from its width, which lost the gap in the Unity player.)
IMPLEMENT_SIMPLE_AUTOMATION_TEST(FGameSubtitleWidgetNameGapTest,
    "GameSubtitles.Widget.NameGap",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)

bool FGameSubtitleWidgetNameGapTest::RunTest(const FString& Parameters)
{
    if (!TestTrue(TEXT("Slate is running"), FSlateApplication::IsInitialized() && FSlateApplication::Get().GetRenderer() != nullptr))
    {
        return false;
    }

    const FSlateFontInfo Font = FCoreStyle::GetDefaultFontStyle("Bold", 24);
    TStrongObjectPtr<UGameSubtitleWidget> Widget(NewObject<UGameSubtitleWidget>());
    Widget->SubtitleFontInfo      = FCoreStyle::GetDefaultFontStyle("Regular", 20);
    Widget->CharacterNameFontInfo = Font;

    const float WithSpace    = IGameSubtitleRenderer::Execute_MeasureLineWidth(Widget.Get(), TEXT("Tam: "), true);
    const float WithoutSpace = IGameSubtitleRenderer::Execute_MeasureLineWidth(Widget.Get(), TEXT("Tam:"), true);
    TestTrue(*FString::Printf(TEXT("measuring counts the trailing space (%f with, %f without)"), WithSpace, WithoutSpace),
             WithSpace > WithoutSpace);

    // What the widget's name text block will be: an STextBlock holding "Tam: " in the name font.
    TSharedRef<STextBlock> NameBlock = SNew(STextBlock).Text(FText::FromString(TEXT("Tam: "))).Font(Font);
    NameBlock->SlatePrepass(1.f);
    TestEqual(TEXT("the name block is as wide as the measured prefix"),
              static_cast<float>(NameBlock->GetDesiredSize().X), WithSpace, 0.01f);

    return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
