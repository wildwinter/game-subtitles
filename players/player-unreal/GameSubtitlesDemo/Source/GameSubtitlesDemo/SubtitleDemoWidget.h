#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "SubtitlePlayer.h"
#include "SubtitleWidget.h"
#include "SubtitleDemoWidget.generated.h"

class UVerticalBox;
class UHorizontalBox;
class UTextBlock;
class UButton;
class UComboBoxString;
class UProgressBar;
class UBorder;
class UOverlay;
class USizeBox;
class UCanvasPanel;
class UImage;

/** One entry loaded from subtitles.json */
USTRUCT(BlueprintType)
struct FSubtitleEntry
{
    GENERATED_BODY()

    UPROPERTY(BlueprintReadOnly) FString Id;
    UPROPERTY(BlueprintReadOnly) FString Speaker;
    UPROPERTY(BlueprintReadOnly) FString Text;
    UPROPERTY(BlueprintReadOnly) float   Duration = 5.f;
};

/**
 * Full-screen demo widget for the GameSubtitles plugin.
 *
 * Features:
 *   - Loads Content/Demo/subtitles.json at construct time
 *   - Start / Stop / Reset buttons
 *   - 2x-speed toggle
 *   - Lines per page +/- control (1-5)
 *   - Font size +/- control (10-32 px)
 *   - Progress bar + elapsed/total time display
 *   - Status line
 *
 * The widget builds its own UI tree programmatically in NativeOnInitialized; no Blueprint
 * layout is required. Subclass and override to customise styling.
 */
UCLASS(BlueprintType, Blueprintable)
class GAMESUBTITLESDEMO_API USubtitleDemoWidget : public UUserWidget
{
    GENERATED_BODY()

public:
    // ── UUserWidget ────────────────────────────────────────────────────────────

    virtual void NativeOnInitialized() override;
    virtual void NativeConstruct() override;
    virtual void NativeDestruct() override;
    virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;

protected:
    // ── State ──────────────────────────────────────────────────────────────────

    UPROPERTY()
    TArray<FSubtitleEntry> Scripts;

    UPROPERTY()
    USubtitlePlayer* Player = nullptr;

    /** The SubtitleWidget that acts as the renderer. */
    UPROPERTY()
    USubtitleWidget* SubWidget = nullptr;

    bool  bIsRunning      = false;
    float ElapsedMs       = 0.f;
    float TotalMs         = 0.f;
    int32 CurrentMaxLines = 2;
    int32 CurrentFontSize = 16;
    bool  bDoubleSpeed    = false;
    bool  bCharNameEnabled = true;
    int32 CharColourIndex  = 0;

    // ── Built UI widgets ───────────────────────────────────────────────────────

    UPROPERTY() UComboBoxString* LangSelector    = nullptr;
    UPROPERTY() UComboBoxString* ScriptSelector  = nullptr;
    UPROPERTY() UButton*        BtnStart         = nullptr;
    UPROPERTY() UButton*        BtnStop          = nullptr;
    UPROPERTY() UButton*        BtnReset         = nullptr;
    UPROPERTY() UButton*        BtnSpeedToggle   = nullptr;
    UPROPERTY() UButton*        BtnLinesDec      = nullptr;
    UPROPERTY() UButton*        BtnLinesInc      = nullptr;
    UPROPERTY() UButton*        BtnFontDec       = nullptr;
    UPROPERTY() UButton*        BtnFontInc       = nullptr;
    UPROPERTY() UProgressBar*   ProgressBar      = nullptr;
    UPROPERTY() UTextBlock*     StatusText       = nullptr;
    UPROPERTY() UTextBlock*     PageInfoText     = nullptr;
    UPROPERTY() UTextBlock*     TimeInfoText     = nullptr;
    UPROPERTY() UTextBlock*     LinesCountText   = nullptr;
    UPROPERTY() UTextBlock*     FontSizeText     = nullptr;
    UPROPERTY() UTextBlock*     SpeakerNameText  = nullptr;
    UPROPERTY() UButton*        BtnCharToggle    = nullptr;
    UPROPERTY() UButton*        BtnCharColour    = nullptr;

    // ── Helpers ────────────────────────────────────────────────────────────────

    void LoadSubtitles(const FString& Filename);
    void PopulateScriptSelector();
    void BuildUI();

    void DoStart();
    void DoStop();
    void DoReset();
    void ApplyFont();
    void UpdateProgress();
    void SetRunning(bool bRunning);
    void UpdateLinesDisplay();
    void UpdateFontDisplay();
    void UpdateCharNameDisplay();

    UFUNCTION()
    void OnStartClicked();
    UFUNCTION()
    void OnStopClicked();
    UFUNCTION()
    void OnResetClicked();
    UFUNCTION()
    void OnSpeedToggleClicked();
    UFUNCTION()
    void OnLinesDecClicked();
    UFUNCTION()
    void OnLinesIncClicked();
    UFUNCTION()
    void OnFontDecClicked();
    UFUNCTION()
    void OnFontIncClicked();
    UFUNCTION()
    void OnLangSelectionChanged(FString SelectedItem, ESelectInfo::Type SelectionType);
    UFUNCTION()
    void OnScriptSelectionChanged(FString SelectedItem, ESelectInfo::Type SelectionType);
    UFUNCTION()
    void OnSubtitleComplete();
    UFUNCTION()
    void OnCharToggleClicked();
    UFUNCTION()
    void OnCharColourClicked();

private:
    // Helper factories
    UTextBlock* MakeLabel(const FString& Text, float Size = 11.f, FLinearColor Color = FLinearColor(0.545f, 0.580f, 0.620f, 1.f));
    UButton*    MakeButton(const FString& Label, FLinearColor BgColor = FLinearColor(0.129f, 0.149f, 0.176f, 1.f));
    FSlateFontInfo SubtitleFont() const;
    FSlateFontInfo SubtitleBoldFont() const;
};
