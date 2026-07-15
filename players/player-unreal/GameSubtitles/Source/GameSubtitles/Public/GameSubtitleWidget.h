#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "IGameSubtitleRenderer.h"
#include "Fonts/SlateFontInfo.h"
#include "GameSubtitleWidget.generated.h"

class UVerticalBox;
class UTextBlock;

/**
 * A UUserWidget that implements IGameSubtitleRenderer.
 *
 * Measures text with the Slate font measure service (same font used for rendering),
 * renders each line as a UTextBlock in a centred UVerticalBox, and reports the
 * widget's local width as the container width.
 *
 * -- Blueprint usage --
 * Create a Widget Blueprint subclass of GameSubtitleWidget. In the designer, add a
 * UVerticalBox named "TextContainer" anywhere in the hierarchy; the widget will
 * populate it with line text blocks. If no TextContainer is present in the designer,
 * one is created automatically filling the widget's root.
 *
 * -- C++ / programmatic usage --
 * UGameSubtitleWidget* Widget = CreateWidget<UGameSubtitleWidget>(PlayerController, UGameSubtitleWidget::StaticClass());
 * // The widget builds its own tree on NativeOnInitialized.
 * Player->Initialize(Widget, 2);
 *
 * -- Font --
 * Set SubtitleFontInfo before the first Start(). Optionally set CharacterNameFontInfo
 * to style the character-name prefix differently from the body text.
 * If ContainerWidthOverride > 0 it is used instead of the widget geometry (useful
 * before the widget is laid out on screen for the first time).
 */
UCLASS(BlueprintType, Blueprintable)
class GAMESUBTITLES_API UGameSubtitleWidget : public UUserWidget, public IGameSubtitleRenderer
{
    GENERATED_BODY()

public:
    /** Font used for measuring and rendering subtitle body lines. */
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Subtitles")
    FSlateFontInfo SubtitleFontInfo;

    /**
     * Font used for the character-name prefix when a subtitle has a character name.
     * Carries its own typeface and size. If not set (HasValidFont() == false),
     * SubtitleFontInfo is used as a fallback.
     */
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Subtitles")
    FSlateFontInfo CharacterNameFontInfo;

    /** Text colour for rendered subtitle lines. */
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Subtitles")
    FLinearColor TextColor = FLinearColor::White;

    /**
     * When > 0, returned by GetContainerWidth() instead of the widget's cached
     * geometry width. Set this if you call Start() before the widget is on screen.
     */
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Subtitles")
    float ContainerWidthOverride = 0.f;

    // ── IGameSubtitleRenderer ──────────────────────────────────────────────────

    virtual float MeasureLineWidth_Implementation(const FString& Text, bool bUseCharacterNameFont) override;
    virtual float GetContainerWidth_Implementation() override;
    virtual void  Render_Implementation(const TArray<FString>& Lines, const FGameSubtitleCharacterContext& CharacterContext) override;
    virtual void  Clear_Implementation() override;

    // ── UUserWidget ────────────────────────────────────────────────────────────

    virtual void NativeOnInitialized() override;
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
