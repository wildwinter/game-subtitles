#include "SubtitleDemoWidget.h"

#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Components/TextBlock.h"
#include "Components/Button.h"
#include "Components/ComboBoxString.h"
#include "Components/ProgressBar.h"
#include "Components/Border.h"
#include "Components/BorderSlot.h"
#include "Components/Overlay.h"
#include "Components/OverlaySlot.h"
#include "Components/SizeBox.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Blueprint/WidgetTree.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"
#include "Dom/JsonObject.h"
#include "Dom/JsonValue.h"
#include "Fonts/SlateFontInfo.h"
#include "Styling/SlateTypes.h"
#include "Engine/Font.h"

// ── Palette (mirrors index.html dark theme) ────────────────────────────────────

namespace Palette
{
    static const FLinearColor BgDark     = FLinearColor(0.051f, 0.067f, 0.090f, 1.f); // #0d1117
    static const FLinearColor BgPanel    = FLinearColor(0.082f, 0.102f, 0.133f, 1.f); // #161b22
    static const FLinearColor BgScene    = FLinearColor(0.086f, 0.039f, 0.149f, 1.f); // dark purple sky
    static const FLinearColor BgSubBar   = FLinearColor(0.f,    0.f,    0.f,    0.75f);
    static const FLinearColor TextMain   = FLinearColor(0.788f, 0.820f, 0.851f, 1.f); // #c9d1d9
    static const FLinearColor TextMuted  = FLinearColor(0.545f, 0.580f, 0.620f, 1.f); // #8b949e
    static const FLinearColor TextAccent = FLinearColor(0.941f, 0.800f, 0.533f, 1.f); // #f0cc88
    static const FLinearColor BtnGreen   = FLinearColor(0.137f, 0.525f, 0.212f, 1.f); // #238636
    static const FLinearColor BtnGray    = FLinearColor(0.129f, 0.149f, 0.176f, 1.f); // #21262d
    static const FLinearColor White      = FLinearColor::White;
}

// ── Lifecycle ──────────────────────────────────────────────────────────────────

void USubtitleDemoWidget::NativeConstruct()
{
    Super::NativeConstruct();

    LoadSubtitles();
    BuildUI();

    // Create the player
    Player = NewObject<USubtitlePlayer>(this);
    Player->Initialize(SubWidget, CurrentMaxLines);
    Player->OnComplete.AddDynamic(this, &USubtitleDemoWidget::OnSubtitleComplete);

    SetRunning(false);

    if (Scripts.Num() == 0)
    {
        if (StatusText) StatusText->SetText(FText::FromString(TEXT("No subtitle data found.")));
        if (BtnStart)   BtnStart->SetIsEnabled(false);
    }
    else
    {
        if (StatusText) StatusText->SetText(FText::FromString(TEXT("Select a subtitle entry and press Start.")));
    }
}

void USubtitleDemoWidget::NativeDestruct()
{
    if (Player)
    {
        Player->Stop();
    }
    Super::NativeDestruct();
}

void USubtitleDemoWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
    Super::NativeTick(MyGeometry, InDeltaTime);

    if (!bIsRunning || !Player)
    {
        return;
    }

    const float Multiplier = bDoubleSpeed ? 2.f : 1.f;
    const float Delta      = InDeltaTime * Multiplier;

    ElapsedMs = FMath::Min(ElapsedMs + InDeltaTime * 1000.f * Multiplier, TotalMs);

    Player->Tick(Delta);
    UpdateProgress();
}

// ── Data loading ───────────────────────────────────────────────────────────────

void USubtitleDemoWidget::LoadSubtitles()
{
    const FString JsonPath = FPaths::Combine(FPaths::ProjectContentDir(), TEXT("Demo/subtitles.json"));

    FString JsonStr;
    if (!FFileHelper::LoadFileToString(JsonStr, *JsonPath))
    {
        UE_LOG(LogTemp, Warning, TEXT("SubtitleDemo: could not load %s"), *JsonPath);
        return;
    }

    TArray<TSharedPtr<FJsonValue>> JsonArray;
    TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(JsonStr);
    if (!FJsonSerializer::Deserialize(Reader, JsonArray))
    {
        UE_LOG(LogTemp, Warning, TEXT("SubtitleDemo: failed to parse subtitles.json"));
        return;
    }

    Scripts.Empty();
    for (const TSharedPtr<FJsonValue>& Val : JsonArray)
    {
        const TSharedPtr<FJsonObject>& Obj = Val->AsObject();
        if (!Obj.IsValid())
        {
            continue;
        }

        FSubtitleEntry Entry;
        Entry.Id      = Obj->GetStringField(TEXT("id"));
        Entry.Speaker = Obj->GetStringField(TEXT("speaker"));
        Entry.Text    = Obj->GetStringField(TEXT("subtitle"));

        if (Entry.Text.IsEmpty())
        {
            continue;
        }

        // Duration: ~14 chars/s, clamped to [3, 18] seconds (matches JS demo)
        FString CleanText = Entry.Text.Replace(TEXT("\u00AD"), TEXT(""));
        const int32 CharCount = CleanText.Len();
        Entry.Duration = FMath::Clamp(FMath::RoundToFloat(CharCount / 14.f), 3.f, 18.f);

        Scripts.Add(Entry);
    }
}

// ── UI construction ────────────────────────────────────────────────────────────

void USubtitleDemoWidget::BuildUI()
{
    if (!WidgetTree)
    {
        return;
    }

    // ── Root: vertical stack ─────────────────────────────────────────────────
    UVerticalBox* Root = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
    WidgetTree->RootWidget = Root;

    // Background fill
    Root->SetVisibility(ESlateVisibility::Visible);

    // ── Title ────────────────────────────────────────────────────────────────
    {
        UTextBlock* Title = MakeLabel(TEXT("GAME SUBTITLES — PLAYER DEMO"), 11.f, Palette::TextAccent);
        UVerticalBoxSlot* Slot = Root->AddChildToVerticalBox(Title);
        Slot->SetPadding(FMargin(0.f, 16.f, 0.f, 12.f));
        Slot->SetHorizontalAlignment(HAlign_Center);
    }

    // ── Scene area: dark panel with subtitle bar overlay ─────────────────────
    {
        USizeBox* SceneBox = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
        SceneBox->SetWidthOverride(540.f);
        SceneBox->SetHeightOverride(304.f);

        UOverlay* SceneOverlay = WidgetTree->ConstructWidget<UOverlay>(UOverlay::StaticClass());
        SceneBox->AddChild(SceneOverlay);

        // Background (represents the game scene)
        UBorder* SceneBg = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
        SceneBg->SetBrushColor(Palette::BgScene);
        {
            UOverlaySlot* BgSlot = SceneOverlay->AddChildToOverlay(SceneBg);
            BgSlot->SetHorizontalAlignment(HAlign_Fill);
            BgSlot->SetVerticalAlignment(VAlign_Fill);
        }

        // Bottom footer: speaker name + subtitle widget, bottom-aligned
        UVerticalBox* Footer = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
        {
            UOverlaySlot* FooterSlot = SceneOverlay->AddChildToOverlay(Footer);
            FooterSlot->SetHorizontalAlignment(HAlign_Fill);
            FooterSlot->SetVerticalAlignment(VAlign_Bottom);
        }

        // Speaker name
        SpeakerNameText = MakeLabel(TEXT(""), 8.5f, Palette::TextAccent);
        SpeakerNameText->SetJustification(ETextJustify::Center);
        {
            UVerticalBoxSlot* Slot = Footer->AddChildToVerticalBox(SpeakerNameText);
            Slot->SetPadding(FMargin(0.f, 0.f, 0.f, 4.f));
            Slot->SetHorizontalAlignment(HAlign_Fill);
        }

        // Subtitle widget (implements ISubtitleRenderer)
        SubWidget = WidgetTree->ConstructWidget<USubtitleWidget>(USubtitleWidget::StaticClass());
        SubWidget->FontInfo            = SubtitleFont();
        SubWidget->TextColor           = Palette::White;
        SubWidget->ContainerWidthOverride = 540.f;
        {
            UVerticalBoxSlot* Slot = Footer->AddChildToVerticalBox(SubWidget);
            Slot->SetHorizontalAlignment(HAlign_Fill);
        }

        // Wrap the subtitle widget in a dark semi-transparent border
        UBorder* SubBar = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
        SubBar->SetBrushColor(Palette::BgSubBar);
        SubBar->SetPadding(FMargin(0.f, 8.f, 0.f, 12.f));
        SubBar->AddChild(SubWidget);

        // Replace SubWidget child with the bordered version
        Footer->ClearChildren();
        {
            UVerticalBoxSlot* NameSlot = Footer->AddChildToVerticalBox(SpeakerNameText);
            NameSlot->SetPadding(FMargin(0.f, 0.f, 0.f, 4.f));
            NameSlot->SetHorizontalAlignment(HAlign_Fill);
        }
        {
            UVerticalBoxSlot* BarSlot = Footer->AddChildToVerticalBox(SubBar);
            BarSlot->SetHorizontalAlignment(HAlign_Fill);
        }

        // Add scene box to root
        UVerticalBoxSlot* SceneSlot = Root->AddChildToVerticalBox(SceneBox);
        SceneSlot->SetHorizontalAlignment(HAlign_Center);
        SceneSlot->SetPadding(FMargin(0.f, 0.f, 0.f, 12.f));
    }

    // ── Script selector + main buttons ───────────────────────────────────────
    {
        UHorizontalBox* CtrlRow = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());

        // Combo box
        ScriptSelector = WidgetTree->ConstructWidget<UComboBoxString>(UComboBoxString::StaticClass());
        if (Scripts.Num() == 0)
        {
            ScriptSelector->AddOption(TEXT("(no subtitles loaded)"));
        }
        else
        {
            for (int32 i = 0; i < Scripts.Num(); ++i)
            {
                const FSubtitleEntry& S      = Scripts[i];
                FString               Preview = S.Text.Replace(TEXT("\u00AD"), TEXT("")).Left(42);
                if (S.Text.Len() > 42)
                {
                    Preview += TEXT("\u2026");
                }
                ScriptSelector->AddOption(FString::Printf(TEXT("[%s] %s \u2014 %s"), *S.Id, *S.Speaker, *Preview));
            }
            ScriptSelector->SetSelectedIndex(0);
        }
        ScriptSelector->OnSelectionChanged.AddDynamic(this, &USubtitleDemoWidget::OnScriptSelectionChanged);
        {
            UHorizontalBoxSlot* Slot = CtrlRow->AddChildToHorizontalBox(ScriptSelector);
            Slot->SetPadding(FMargin(0.f, 0.f, 6.f, 0.f));
            Slot->SetHorizontalAlignment(HAlign_Fill);
            Slot->SetVerticalAlignment(VAlign_Center);
            FSlateChildSize Size;
            Size.SizeRule = ESlateSizeRule::Fill;
            Slot->SetSize(Size);
        }

        BtnStart = MakeButton(TEXT("\u25B6 Start"), Palette::BtnGreen);
        BtnStart->OnClicked.AddDynamic(this, &USubtitleDemoWidget::OnStartClicked);
        {
            UHorizontalBoxSlot* Slot = CtrlRow->AddChildToHorizontalBox(BtnStart);
            Slot->SetPadding(FMargin(0.f, 0.f, 6.f, 0.f));
            Slot->SetVerticalAlignment(VAlign_Center);
        }

        BtnStop = MakeButton(TEXT("\u25A0 Stop"));
        BtnStop->OnClicked.AddDynamic(this, &USubtitleDemoWidget::OnStopClicked);
        {
            UHorizontalBoxSlot* Slot = CtrlRow->AddChildToHorizontalBox(BtnStop);
            Slot->SetPadding(FMargin(0.f, 0.f, 6.f, 0.f));
            Slot->SetVerticalAlignment(VAlign_Center);
        }

        BtnReset = MakeButton(TEXT("\u21BA Reset"));
        BtnReset->OnClicked.AddDynamic(this, &USubtitleDemoWidget::OnResetClicked);
        {
            UHorizontalBoxSlot* Slot = CtrlRow->AddChildToHorizontalBox(BtnReset);
            Slot->SetVerticalAlignment(VAlign_Center);
        }

        UVerticalBoxSlot* RowSlot = Root->AddChildToVerticalBox(CtrlRow);
        RowSlot->SetPadding(FMargin(0.f, 0.f, 0.f, 8.f));
        RowSlot->SetHorizontalAlignment(HAlign_Center);
    }

    // ── Options row: speed, lines, font ──────────────────────────────────────
    {
        UHorizontalBox* OptRow = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());

        // 2x speed toggle
        BtnSpeedToggle = MakeButton(TEXT("1\xD7 Speed"));
        BtnSpeedToggle->OnClicked.AddDynamic(this, &USubtitleDemoWidget::OnSpeedToggleClicked);
        {
            UHorizontalBoxSlot* Slot = OptRow->AddChildToHorizontalBox(BtnSpeedToggle);
            Slot->SetPadding(FMargin(0.f, 0.f, 16.f, 0.f));
            Slot->SetVerticalAlignment(VAlign_Center);
        }

        // Lines control
        {
            UTextBlock* Lbl = MakeLabel(TEXT("Lines:"), 11.f, Palette::TextMain);
            UHorizontalBoxSlot* Slot = OptRow->AddChildToHorizontalBox(Lbl);
            Slot->SetPadding(FMargin(0.f, 0.f, 4.f, 0.f));
            Slot->SetVerticalAlignment(VAlign_Center);
        }
        BtnLinesDec = MakeButton(TEXT("\u2212"));
        BtnLinesDec->OnClicked.AddDynamic(this, &USubtitleDemoWidget::OnLinesDecClicked);
        {
            UHorizontalBoxSlot* Slot = OptRow->AddChildToHorizontalBox(BtnLinesDec);
            Slot->SetPadding(FMargin(0.f, 0.f, 4.f, 0.f));
            Slot->SetVerticalAlignment(VAlign_Center);
        }
        LinesCountText = MakeLabel(TEXT("2"), 11.f, Palette::TextMain);
        {
            UHorizontalBoxSlot* Slot = OptRow->AddChildToHorizontalBox(LinesCountText);
            Slot->SetPadding(FMargin(0.f, 0.f, 4.f, 0.f));
            Slot->SetVerticalAlignment(VAlign_Center);
        }
        BtnLinesInc = MakeButton(TEXT("+"));
        BtnLinesInc->OnClicked.AddDynamic(this, &USubtitleDemoWidget::OnLinesIncClicked);
        {
            UHorizontalBoxSlot* Slot = OptRow->AddChildToHorizontalBox(BtnLinesInc);
            Slot->SetPadding(FMargin(0.f, 0.f, 16.f, 0.f));
            Slot->SetVerticalAlignment(VAlign_Center);
        }

        // Font size control
        {
            UTextBlock* Lbl = MakeLabel(TEXT("Font:"), 11.f, Palette::TextMain);
            UHorizontalBoxSlot* Slot = OptRow->AddChildToHorizontalBox(Lbl);
            Slot->SetPadding(FMargin(0.f, 0.f, 4.f, 0.f));
            Slot->SetVerticalAlignment(VAlign_Center);
        }
        BtnFontDec = MakeButton(TEXT("\u2212"));
        BtnFontDec->OnClicked.AddDynamic(this, &USubtitleDemoWidget::OnFontDecClicked);
        {
            UHorizontalBoxSlot* Slot = OptRow->AddChildToHorizontalBox(BtnFontDec);
            Slot->SetPadding(FMargin(0.f, 0.f, 4.f, 0.f));
            Slot->SetVerticalAlignment(VAlign_Center);
        }
        FontSizeText = MakeLabel(TEXT("16px"), 11.f, Palette::TextMain);
        {
            UHorizontalBoxSlot* Slot = OptRow->AddChildToHorizontalBox(FontSizeText);
            Slot->SetPadding(FMargin(0.f, 0.f, 4.f, 0.f));
            Slot->SetVerticalAlignment(VAlign_Center);
        }
        BtnFontInc = MakeButton(TEXT("+"));
        BtnFontInc->OnClicked.AddDynamic(this, &USubtitleDemoWidget::OnFontIncClicked);
        {
            UHorizontalBoxSlot* Slot = OptRow->AddChildToHorizontalBox(BtnFontInc);
            Slot->SetVerticalAlignment(VAlign_Center);
        }

        UVerticalBoxSlot* RowSlot = Root->AddChildToVerticalBox(OptRow);
        RowSlot->SetPadding(FMargin(0.f, 0.f, 0.f, 12.f));
        RowSlot->SetHorizontalAlignment(HAlign_Center);
    }

    // ── Progress bar ─────────────────────────────────────────────────────────
    {
        USizeBox* PBBox = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
        PBBox->SetWidthOverride(540.f);

        ProgressBar = WidgetTree->ConstructWidget<UProgressBar>(UProgressBar::StaticClass());
        FProgressBarStyle PBStyle;
        PBStyle.BackgroundImage.TintColor = FSlateColor(Palette::BgPanel);
        PBStyle.FillImage.TintColor       = FSlateColor(Palette::TextAccent);
        ProgressBar->SetWidgetStyle(PBStyle);
        ProgressBar->SetPercent(0.f);
        PBBox->AddChild(ProgressBar);

        UVerticalBoxSlot* Slot = Root->AddChildToVerticalBox(PBBox);
        Slot->SetPadding(FMargin(0.f, 0.f, 0.f, 4.f));
        Slot->SetHorizontalAlignment(HAlign_Center);
    }

    // ── Progress meta: page info + time ──────────────────────────────────────
    {
        UHorizontalBox* MetaRow = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());

        PageInfoText = MakeLabel(TEXT(""), 9.f, Palette::TextMuted);
        {
            UHorizontalBoxSlot* Slot = MetaRow->AddChildToHorizontalBox(PageInfoText);
            FSlateChildSize Fill;
            Fill.SizeRule = ESlateSizeRule::Fill;
            Slot->SetSize(Fill);
        }

        TimeInfoText = MakeLabel(TEXT(""), 9.f, Palette::TextMuted);
        TimeInfoText->SetJustification(ETextJustify::Right);
        {
            UHorizontalBoxSlot* Slot = MetaRow->AddChildToHorizontalBox(TimeInfoText);
            FSlateChildSize Fill;
            Fill.SizeRule = ESlateSizeRule::Fill;
            Slot->SetSize(Fill);
        }

        USizeBox* MetaBox = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
        MetaBox->SetWidthOverride(540.f);
        MetaBox->AddChild(MetaRow);

        UVerticalBoxSlot* Slot = Root->AddChildToVerticalBox(MetaBox);
        Slot->SetPadding(FMargin(0.f, 0.f, 0.f, 8.f));
        Slot->SetHorizontalAlignment(HAlign_Center);
    }

    // ── Status line ───────────────────────────────────────────────────────────
    {
        StatusText = MakeLabel(TEXT("Loading\u2026"), 10.5f, Palette::TextMuted);
        UVerticalBoxSlot* Slot = Root->AddChildToVerticalBox(StatusText);
        Slot->SetHorizontalAlignment(HAlign_Center);
        Slot->SetPadding(FMargin(0.f, 0.f, 0.f, 24.f));
    }

    UpdateLinesDisplay();
    UpdateFontDisplay();
}

// ── Controls ───────────────────────────────────────────────────────────────────

void USubtitleDemoWidget::DoStart()
{
    if (!Player || Scripts.Num() == 0)
    {
        return;
    }

    Player->Stop();

    const int32 Idx = ScriptSelector ? ScriptSelector->GetSelectedIndex() : 0;
    if (!Scripts.IsValidIndex(Idx))
    {
        return;
    }

    const FSubtitleEntry& S = Scripts[Idx];

    TotalMs   = S.Duration * 1000.f;
    ElapsedMs = 0.f;

    if (SpeakerNameText)
    {
        SpeakerNameText->SetText(FText::FromString(S.Speaker.ToUpper()));
    }

    Player->MaxLines = CurrentMaxLines;
    Player->Start(S.Text, S.Duration);

    SetRunning(true);

    if (StatusText)
    {
        StatusText->SetText(FText::FromString(
            FString::Printf(TEXT("Playing: [%s] %s"), *S.Id, *S.Speaker)));
    }
    UpdateProgress();
}

void USubtitleDemoWidget::DoStop()
{
    if (Player)
    {
        Player->Stop();
    }
    SetRunning(false);
    if (StatusText)
    {
        StatusText->SetText(FText::FromString(TEXT("Stopped.")));
    }
}

void USubtitleDemoWidget::DoReset()
{
    if (Player)
    {
        Player->Reset();
    }
    SetRunning(false);

    ElapsedMs = 0.f;
    if (ProgressBar)   ProgressBar->SetPercent(0.f);
    if (PageInfoText)  PageInfoText->SetText(FText::GetEmpty());
    if (TimeInfoText)  TimeInfoText->SetText(FText::GetEmpty());
    if (SpeakerNameText) SpeakerNameText->SetText(FText::GetEmpty());
    if (StatusText)    StatusText->SetText(FText::FromString(TEXT("Select a subtitle entry and press Start.")));
}

void USubtitleDemoWidget::ApplyFont()
{
    if (SubWidget)
    {
        SubWidget->FontInfo = SubtitleFont();
    }
}

void USubtitleDemoWidget::UpdateProgress()
{
    const float Pct = TotalMs > 0.f ? FMath::Min(1.f, ElapsedMs / TotalMs) : 0.f;
    if (ProgressBar)
    {
        ProgressBar->SetPercent(Pct);
    }

    if (Player && PageInfoText)
    {
        const int32 Pages = Player->GetPageCount();
        // Page index is not directly exposed; derive from elapsed proportion
        if (Pages > 1)
        {
            PageInfoText->SetText(FText::FromString(
                FString::Printf(TEXT("Page ? / %d"), Pages)));
        }
        else
        {
            PageInfoText->SetText(FText::GetEmpty());
        }
    }

    if (TimeInfoText)
    {
        TimeInfoText->SetText(FText::FromString(
            FString::Printf(TEXT("%.1f s / %.1f s"), ElapsedMs / 1000.f, TotalMs / 1000.f)));
    }
}

void USubtitleDemoWidget::SetRunning(bool bRunning)
{
    bIsRunning = bRunning;
    if (BtnStart) BtnStart->SetIsEnabled(!bRunning && Scripts.Num() > 0);
    if (BtnStop)  BtnStop->SetIsEnabled(bRunning);
}

void USubtitleDemoWidget::UpdateLinesDisplay()
{
    if (LinesCountText)
    {
        LinesCountText->SetText(FText::FromString(FString::FromInt(CurrentMaxLines)));
    }
    if (BtnLinesDec) BtnLinesDec->SetIsEnabled(CurrentMaxLines > 1);
}

void USubtitleDemoWidget::UpdateFontDisplay()
{
    if (FontSizeText)
    {
        FontSizeText->SetText(FText::FromString(FString::Printf(TEXT("%dpx"), CurrentFontSize)));
    }
    if (BtnFontDec) BtnFontDec->SetIsEnabled(CurrentFontSize > 10);
    if (BtnFontInc) BtnFontInc->SetIsEnabled(CurrentFontSize < 32);
}

// ── Button callbacks ───────────────────────────────────────────────────────────

void USubtitleDemoWidget::OnStartClicked()  { DoStart(); }
void USubtitleDemoWidget::OnStopClicked()   { DoStop();  }
void USubtitleDemoWidget::OnResetClicked()  { DoReset(); }

void USubtitleDemoWidget::OnSpeedToggleClicked()
{
    bDoubleSpeed = !bDoubleSpeed;
    if (BtnSpeedToggle)
    {
        BtnSpeedToggle->SetToolTipText(FText::GetEmpty());
        // Update button label
        if (UTextBlock* Lbl = Cast<UTextBlock>(BtnSpeedToggle->GetContent()))
        {
            Lbl->SetText(FText::FromString(bDoubleSpeed ? TEXT("2\xD7 Speed") : TEXT("1\xD7 Speed")));
        }
    }
}

void USubtitleDemoWidget::OnLinesDecClicked()
{
    if (CurrentMaxLines > 1)
    {
        --CurrentMaxLines;
        if (Player) Player->MaxLines = CurrentMaxLines;
        UpdateLinesDisplay();
        if (bIsRunning) DoStart();
    }
}

void USubtitleDemoWidget::OnLinesIncClicked()
{
    if (CurrentMaxLines < 5)
    {
        ++CurrentMaxLines;
        if (Player) Player->MaxLines = CurrentMaxLines;
        UpdateLinesDisplay();
        if (bIsRunning) DoStart();
    }
}

void USubtitleDemoWidget::OnFontDecClicked()
{
    if (CurrentFontSize > 10)
    {
        CurrentFontSize -= 2;
        ApplyFont();
        UpdateFontDisplay();
        if (bIsRunning) DoStart();
    }
}

void USubtitleDemoWidget::OnFontIncClicked()
{
    if (CurrentFontSize < 32)
    {
        CurrentFontSize += 2;
        ApplyFont();
        UpdateFontDisplay();
        if (bIsRunning) DoStart();
    }
}

void USubtitleDemoWidget::OnScriptSelectionChanged(FString /*SelectedItem*/, ESelectInfo::Type /*SelectionType*/)
{
    DoReset();
}

void USubtitleDemoWidget::OnSubtitleComplete()
{
    SetRunning(false);
    ElapsedMs = TotalMs;
    UpdateProgress();
    if (SpeakerNameText) SpeakerNameText->SetText(FText::GetEmpty());
    if (StatusText)      StatusText->SetText(FText::FromString(TEXT("Finished.")));
}

// ── Private helpers ────────────────────────────────────────────────────────────

UTextBlock* USubtitleDemoWidget::MakeLabel(const FString& Text, float Size, FLinearColor Color)
{
    UTextBlock* Block = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
    if (!Block) return nullptr;

    FSlateFontInfo Font;
    Font.Size = static_cast<int32>(Size);
    Block->SetFont(Font);
    Block->SetText(FText::FromString(Text));
    Block->SetColorAndOpacity(FSlateColor(Color));
    return Block;
}

UButton* USubtitleDemoWidget::MakeButton(const FString& Label, FLinearColor BgColor)
{
    UButton* Btn = WidgetTree->ConstructWidget<UButton>(UButton::StaticClass());
    if (!Btn) return nullptr;

    FButtonStyle Style;
    Style.Normal.TintColor    = FSlateColor(BgColor);
    Style.Hovered.TintColor   = FSlateColor(BgColor * 1.2f);
    Style.Pressed.TintColor   = FSlateColor(BgColor * 0.85f);
    Style.Disabled.TintColor  = FSlateColor(BgColor * FLinearColor(1,1,1,0.38f));
    Btn->SetStyle(Style);

    UTextBlock* LabelBlock = MakeLabel(Label, 11.f, Palette::TextMain);
    if (LabelBlock)
    {
        Btn->AddChild(LabelBlock);
    }

    return Btn;
}

FSlateFontInfo USubtitleDemoWidget::SubtitleFont() const
{
    FSlateFontInfo Info;
    Info.Size = CurrentFontSize;
    // Resolve the engine default font so the widget works out of the box.
    // In a real project, replace with a project-specific font asset.
    Info.FontObject = GEngine ? GEngine->GetMediumFont() : nullptr;
    return Info;
}
