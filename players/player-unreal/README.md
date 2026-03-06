# player-unreal

Unreal Engine 5.7 port of [player-js](../player-js). Same layout algorithms, same API shape, packaged as a UE plugin with a matching demo project.

---

## Directory layout

```
player-unreal/
├── GameSubtitles/          # The plugin
│   ├── GameSubtitles.uplugin
│   └── Source/GameSubtitles/
│       ├── GameSubtitles.Build.cs
│       ├── Public/
│       │   ├── TextLayout.h          # WrapAndPaginate + AllocateTimings (static)
│       │   ├── ISubtitleRenderer.h   # UInterface — MeasureLineWidth / GetContainerWidth / Render / Clear
│       │   ├── SubtitlePlayer.h      # UObject — Initialize / Start / Tick / Stop / Reset
│       │   └── SubtitleWidget.h      # UUserWidget that implements ISubtitleRenderer
│       └── Private/
│           ├── TextLayout.cpp
│           ├── SubtitlePlayer.cpp
│           └── SubtitleWidget.cpp
└── GameSubtitlesDemo/      # Demo Unreal project
    ├── GameSubtitlesDemo.uproject
    ├── Config/DefaultGame.ini
    ├── Content/Demo/subtitles.json   # same data as player-js demo
    └── Source/GameSubtitlesDemo/
        ├── SubtitleDemoGameMode.*    # sets HUD class
        ├── SubtitleDemoHUD.*         # creates & adds the widget
        └── SubtitleDemoWidget.*      # full demo UI (mirrors demo/index.html)
```

---

## API — plugin (C++)

### `FSubtitleTextLayout` (static, `TextLayout.h`)

```cpp
// Wrap + paginate text. MeasureWidth returns pixel/Slate-unit width of a string.
TArray<TArray<FString>> Pages =
    FSubtitleTextLayout::WrapAndPaginate(Text, MeasureWidth, ContainerWidth, MaxLines);

// Allocate display time proportionally to character count per page.
TArray<float> Timings =
    FSubtitleTextLayout::AllocateTimings(Pages, TotalDurationSeconds);
```

### `USubtitlePlayer` (UObject, `SubtitlePlayer.h`)

```cpp
USubtitlePlayer* Player = NewObject<USubtitlePlayer>(this);
Player->Initialize(MyRenderer, /*MaxLines=*/2);

Player->OnComplete.AddDynamic(this, &AMyActor::HandleSubtitleDone);

// Start playback (lays out, renders page 0 immediately)
Player->Start(TEXT("Hello world"), 5.0f);

// Call from game loop / NativeTick
Player->Tick(DeltaSeconds);

// Control
Player->Stop();
Player->Reset();

// Introspection
int32 Pages = Player->GetPageCount();
Player->MaxLines = 3; // takes effect on next Start()
```

All methods are also Blueprint-callable. `OnComplete` is a `BlueprintAssignable` dynamic multicast delegate.

### `ISubtitleRenderer` (UInterface, `ISubtitleRenderer.h`)

Implement on any UObject to create a custom renderer:

```cpp
UCLASS()
class UMyRenderer : public UObject, public ISubtitleRenderer
{
    GENERATED_BODY()
public:
    virtual float MeasureLineWidth_Implementation(const FString& Text) override;
    virtual float GetContainerWidth_Implementation() override;
    virtual void  Render_Implementation(const TArray<FString>& Lines) override;
    virtual void  Clear_Implementation() override;
};
```

### `USubtitleWidget` (UUserWidget, `SubtitleWidget.h`)

Ready-made UMG renderer. Measures text via Slate's font measure service and renders each line as a `UTextBlock` inside a `UVerticalBox`.

```cpp
USubtitleWidget* Widget = CreateWidget<USubtitleWidget>(PC, USubtitleWidget::StaticClass());
Widget->FontInfo = FSlateFontInfo(MyFontAsset, 16);
Widget->TextColor = FLinearColor::White;
Widget->ContainerWidthOverride = 540.f; // set before Start() if widget isn't on screen yet
Widget->AddToViewport();

Player->Initialize(Widget, 2);
```

To customise layout in the Blueprint designer, subclass `USubtitleWidget` and add a `UVerticalBox` named **`TextContainer`** anywhere in the hierarchy; the widget will populate it with line blocks.

---

## API — plugin (Blueprint)

1. **Construct Object of Class** → `SubtitlePlayer`
2. **Initialize** (Renderer = your SubtitleWidget or custom renderer, MaxLines = 2)
3. **Bind** the `On Complete` event
4. **Start** (Text, Duration)
5. From `Event Tick` → **Tick** (DeltaSeconds)
6. **Stop** / **Reset** as needed

---

## Demo project

### Setup

1. Copy `GameSubtitles/` into `GameSubtitlesDemo/Plugins/GameSubtitles/` (or your engine plugins folder).
2. Right-click `GameSubtitlesDemo.uproject` → *Generate Visual Studio project files*.
3. Open the `.uproject` in Unreal Engine 5.7.
4. Create a new level (or use an empty default level).
5. **Play in Editor** — the HUD creates `USubtitleDemoWidget` automatically.

### What you get

- A 540×304 dark scene panel with a subtitle bar at the bottom (speaker name + text lines)
- Script selector (all entries from `Content/Demo/subtitles.json`)
- **▶ Start** / **■ Stop** / **↺ Reset** buttons
- **1× / 2× speed** toggle
- **Lines −/+** control (1–5 lines per page)
- **Font −/+** control (10–32 px)
- Progress bar with elapsed / total time

### Substituting a custom font

`SubtitleDemoWidget.cpp` uses `GEngine->GetMediumFont()` as a fallback. To use a project font:

```cpp
FSlateFontInfo USubtitleDemoWidget::SubtitleFont() const
{
    FSlateFontInfo Info;
    Info.Size       = CurrentFontSize;
    Info.FontObject = LoadObject<UFont>(nullptr, TEXT("/Game/Fonts/MyFont.MyFont"));
    return Info;
}
```

---

## Correspondence with player-js

| player-js | player-unreal |
|---|---|
| `TextLayout.js` → `wrapAndPaginate` | `FSubtitleTextLayout::WrapAndPaginate` |
| `TextLayout.js` → `allocateTimings` | `FSubtitleTextLayout::AllocateTimings` |
| `SubtitlePlayer` class | `USubtitlePlayer` |
| `player.start({ text, duration, onComplete })` | `Player->Start(Text, Duration)` + `OnComplete` delegate |
| `player.tick(dt)` | `Player->Tick(DeltaSeconds)` |
| `player.stop()` / `player.reset()` | `Player->Stop()` / `Player->Reset()` |
| `player.pageCount` | `Player->GetPageCount()` |
| `player.maxLines = n` | `Player->MaxLines = n` |
| `DomRenderer` | `USubtitleWidget` |
| custom renderer object | any `UObject` implementing `ISubtitleRenderer` |
| `demo/index.html` | `USubtitleDemoWidget` |
| `demo/subtitles.json` | `Content/Demo/subtitles.json` |
