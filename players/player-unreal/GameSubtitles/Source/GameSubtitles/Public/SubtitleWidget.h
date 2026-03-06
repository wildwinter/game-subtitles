#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "ISubtitleRenderer.h"
#include "Fonts/SlateFontInfo.h"
#include "SubtitleWidget.generated.h"

class UVerticalBox;
class UTextBlock;

/**
 * A UUserWidget that implements ISubtitleRenderer, providing the "DOM renderer"
 * equivalent from player-js.
 *
 * Measures text with the Slate font measure service (same font used for rendering),
 * renders each line as a UTextBlock in a centred UVerticalBox, and reports the
 * widget's local width as the container width.
 *
 * -- Blueprint usage --
 * Create a Widget Blueprint subclass of SubtitleWidget. In the designer, add a
 * UVerticalBox named "TextContainer" anywhere in the hierarchy; the widget will
 * populate it with line text blocks. If no TextContainer is present in the designer,
 * one is created automatically filling the widget's root.
 *
 * -- C++ / programmatic usage --
 * USubtitleWidget* Widget = CreateWidget<USubtitleWidget>(PlayerController, USubtitleWidget::StaticClass());
 * // The widget builds its own tree on NativeConstruct.
 * Player->Initialize(Widget, 2);
 *
 * -- Font --
 * Set FontInfo before the first Start(), or call InvalidateFont() after changing it.
 * If ContainerWidthOverride > 0 it is used instead of the widget geometry (useful
 * before the widget is laid out on screen for the first time).
 */
UCLASS(BlueprintType, Blueprintable)
class GAMESUBTITLES_API USubtitleWidget : public UUserWidget, public ISubtitleRenderer
{
    GENERATED_BODY()

public:
    /** Font used for both measuring and rendering subtitle lines. */
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Subtitles")
    FSlateFontInfo FontInfo;

    /** Text colour for rendered subtitle lines. */
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Subtitles")
    FLinearColor TextColor = FLinearColor::White;

    /**
     * When > 0, returned by GetContainerWidth() instead of the widget's cached
     * geometry width. Set this if you call Start() before the widget is on screen.
     */
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Subtitles")
    float ContainerWidthOverride = 0.f;

    // ── ISubtitleRenderer ──────────────────────────────────────────────────────

    virtual float MeasureLineWidth_Implementation(const FString& Text) override;
    virtual float GetContainerWidth_Implementation() override;
    virtual void  Render_Implementation(const TArray<FString>& Lines) override;
    virtual void  Clear_Implementation() override;

    // ── UUserWidget ────────────────────────────────────────────────────────────

    virtual void NativeConstruct() override;

protected:
    /**
     * Optional: bind a UVerticalBox named "TextContainer" in a Blueprint subclass
     * to control placement and styling of the line container.
     */
    UPROPERTY(meta = (BindWidgetOptional))
    UVerticalBox* TextContainer = nullptr;

private:
    void EnsureTextContainer();
};
