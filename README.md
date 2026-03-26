# Game Subtitles

**Game Subtitles** is a subtitle system for games built around a shared two-step approach. The first step is a **preprocessor** — a command-line tool and C# library — that annotates your localised subtitle strings with soft hyphen markers before they ship. The second step is a **runtime player** that uses those markers to wrap and paginate subtitle text accurately, no matter what font or container size you are using. Runtime players are provided for **JavaScript** (browser, Node, any JS game engine), **Unreal Engine 5** (C++ plugin with Blueprint support), and **Unity** (UPM package using TextMeshPro).

Together they solve the situation where you have a long line of audio, a long subtitle that won't fit on the screen, and you want to simply say "display this text for this amount of time please".

**Game Subtitles** will take care of that for you, wrapping and breaking the text properly into multiple pages of text, and displaying them at the right time in the spoken line. And it will do this for lots of different languages.

## Table of Contents

- [Why do I need this?](#why-do-i-need-this)
- [Source Code](#source-code)
- [Releases](#releases)
- [Usage](#usage)
  - [Overview](#overview)
  - [Preprocessor](#preprocessor)
  - [JavaScript Player (`players/player-js`)](#javascript-player-playersplayer-js)
  - [Unreal Engine Plugin (`players/player-unreal`)](#unreal-engine-plugin-playersplayer-unreal)
  - [Unity Package (`players/player-unity`)](#unity-package-playersplayer-unity)
- [Security Issues](#security-issues)
- [Contributors](#contributors)
- [Acknowledgements](#acknowledgements)
  - [Hyphenation patterns](#hyphenation-patterns)
- [License](#license)

## Why do I need this?

When displaying subtitles in a game you probably want to:

* Break long lines at sensible word-wrap points without cutting in the middle of a word.
* Hyphenate long words when there is no other option, in the correct place for the language.
* Split text across multiple pages if it is too long to fit in the subtitle box.
* Spend proportional time on each page, rather than a flat duration for everything.

The catch is that all of these things depend on the *actual rendered width* of each character, which you probably only know at runtime as it'll depend on user display settings. Pre-calculating it offline is hard, and different languages have wildly different hyphenation rules.

**Game Subtitles** splits the problem across two steps:

1. **Offline (at build or localization time):** the preprocessor reads your subtitle strings and inserts invisible [soft hyphen](https://en.wikipedia.org/wiki/Soft_hyphen) characters (U+00AD) at every point where a word could safely be broken, using the correct TeX hyphenation rules for the target language. These are zero-width when not used; they only produce a visible `-` if the renderer decides to break there.

2. **At runtime:** the JavaScript player uses those markers — together with the actual measured pixel widths of your font — to wrap and paginate text correctly, whatever the container size. If you (or the user!) changes font size, or the container size, or the number of lines of subtitle, then the render helper will cope with all that when it shows the next line. Flexible subtitles! Cool, right?

## Source Code

Source code is on [GitHub](https://github.com/wildwinter/game-subtitles), available under the MIT license.

## Releases

Pre-built releases are available in the [Releases](https://github.com/wildwinter/game-subtitles/releases) area on GitHub. Each release includes the following zips:

| Zip | Contents | For |
| --- | --- | --- |
| `game-subtitles-unreal-v{version}.zip` | Unreal plugin + Windows & macOS preprocessor binaries | **Unreal Engine developers** — everything you need in one download |
| `game-subtitles-win-v{version}.zip` | Windows preprocessor binary + all players | Windows developers using multiple players |
| `game-subtitles-osx-v{version}.zip` | macOS preprocessor binary + all players | macOS developers using multiple players |
| `game-subtitles-lib-v{version}.zip` | C# `PreprocessorLib.dll` + all players | Custom tooling / calling the preprocessor as a library |

If you are only using the Unreal plugin, download `game-subtitles-unreal-v{version}.zip`.

---

## Usage

### Overview

Here is a typical end-to-end flow:

1. **Run the preprocessor** on your subtitle strings — either via the CLI as part of your build pipeline, or by calling the C# library directly if you have a custom tool. This annotates every string with U+00AD markers and writes the result to a new file in the same format.
2. **Ship the annotated strings** with your game, in whichever format you use (CSV, JSON, XLSX, PO).
3. **At runtime,** load a subtitle string and pass it to the player. It measures each word in your actual font, wraps to lines, paginates, and drives the display for you. Players are available for JavaScript, Unreal Engine 5, and Unity; all three expose the same concepts under equivalent APIs.
4. **In your game loop,** call `player.tick(delta)` (JS), `Player->Tick(DeltaSeconds)` (Unreal), or `player.Tick(deltaTime)` (Unity) each frame. The player advances pages automatically and fires your completion callback when the last page has been shown.

---

### Preprocessor

#### CLI

```text
game-subtitles-preprocess <input> -o <output> [options]

Arguments:
  <input>               File or folder path (required)

Options:
  -o, --output <path>   Output file or folder (required)
  -f, --field <name>    Field to process in CSV / JSON / XLSX files
  -l, --lang <code>     Language code e.g. en_GB, fr_FR (default: en_US)
  --force-overwrite     Always reprocess, even if the output is already up to date
  -h, --help
  --version

Exit codes: 0 = success, 1 = warnings, 2 = error
```

The tool skips any output file that is already newer than its input (make-style up-to-date check). Pass `--force-overwrite` to reprocess regardless.

*Is the utility failing to run on Windows? Check the [security issues](#security-issues) note here.*

**Examples:**

```bash
# Process a PO file for French
game-subtitles-preprocess strings.po -o out.po -l fr_FR

# Process a JSON array — field name is case-insensitive
game-subtitles-preprocess subtitles.json -o out.json --field subtitle -l de_DE

# Process a folder of CSV files
game-subtitles-preprocess ./loc/ -o ./out/ --field text -l es_ES

# Process an XLSX spreadsheet
game-subtitles-preprocess sheet.xlsx -o out.xlsx --field Subtitle -l en_GB
```

#### What the preprocessor does

The tool inserts **soft hyphen** (U+00AD) characters at every valid break point according to the TeX hyphenation rules for the chosen language.

```text
Input:  "Internationalization is hard."
Output: "In|ter|na|tion|al|i|za|tion is hard."
         ↑  ↑  ↑   ↑  ↑ ↑  ↑   soft hyphens (U+00AD), shown here as | — invisible at runtime
```

No visible characters are added or removed. The strings are otherwise identical to the originals and can be used directly everywhere you already use them.

#### Supported file formats

| Format | Notes |
| --- | --- |
| `.po` | Processes all `msgstr` values; `--field` is ignored |
| `.csv` | `--field` required; processes the named column; all other columns pass through unchanged |
| `.json` | `--field` required; input must be a JSON array of objects `[{…}]` |
| `.xlsx` | `--field` required; first sheet only; header row on row 1 |

> **CSV and UTF-8:** Always save CSV files as **UTF-8 with BOM**. Without a BOM, tools like Microsoft Excel on Windows assume a legacy code page and will silently corrupt any non-ASCII characters — including soft hyphens — before they reach this tool. Most editors (VS Code, Notepad++, JetBrains) have an explicit UTF-8 BOM option in their encoding selector.

#### Supported languages

| Language | Code(s) |
| --- | --- |
| English (US) | `en_US`, `en` |
| English (GB) | `en_GB` |
| French | `fr_FR`, `fr` |
| Italian | `it_IT`, `it` |
| German (Reform 1996) | `de_DE`, `de` |
| Spanish | `es_ES`, `es` |
| Russian | `ru_RU`, `ru` |
| Polish | `pl_PL`, `pl` |
| Portuguese | `pt_PT`, `pt_BR`, `pt` |
| Dutch | `nl_NL`, `nl` |
| Swedish | `sv_SE`, `sv` |
| Norwegian Bokmål | `nb_NO`, `nb` |
| Danish | `da_DK`, `da` |
| Finnish | `fi_FI`, `fi` |
| Czech | `cs_CZ`, `cs` |
| Slovak | `sk_SK`, `sk` |
| Hungarian | `hu_HU`, `hu` |
| Turkish | `tr_TR`, `tr` |
| Ukrainian | `uk_UA`, `uk` |
| Croatian | `hr_HR`, `hr` |
| Romanian | `ro_RO`, `ro` |
| Bulgarian | `bg_BG`, `bg` |

#### C# library API

If you want to call the preprocessor from your own tooling rather than the CLI:

```csharp
using GameSubtitles.Lib;

var preprocessor = new SubtitlePreprocessor();

// Annotate a string with soft hyphens at valid break points
string result = preprocessor.Process("Internationalization is hard.", "en_US");

// List all supported language codes
IReadOnlyList<string> langs = SubtitlePreprocessor.SupportedLanguages;
```

---

### JavaScript Player (`players/player-js`)

Copy `player-js/game-subtitles-player.js` from the distribution zip into your project:

| File | Format |
| --- | --- |
| `player-js/game-subtitles-player.js` | IIFE — `window.GameSubtitles` |

#### Real-world usage

Here is how you would wire this up in a typical game with a DOM-based subtitle overlay.

**1. Set up the player once, pointing it at your subtitle element.**

```javascript
import { SubtitlePlayer, DomRenderer } from './game-subtitles-player.esm.js';

const subtitleEl = document.getElementById('subtitle-bar');
const renderer   = new DomRenderer(subtitleEl);

const player = new SubtitlePlayer({ renderer, maxLines: 2 });
```

The player and renderer are long-lived objects. Create them once and reuse them for every subtitle in the game.

**2. When your dialogue system triggers a line, call `start()`.**

```javascript
function showSubtitle(text, durationSeconds, onDone) {
  player.start({
    text,                  // the annotated string from your subtitle data
    duration: durationSeconds,
    onComplete: onDone,    // called automatically when the last page expires
  });
}
```

`start()` lays out the text, renders the first page immediately, and begins timing. If a subtitle is already playing it is stopped first.

**3. Drive it from your game loop.**

```javascript
function gameLoop(timestamp) {
  const delta = (timestamp - lastTimestamp) / 1000; // seconds
  lastTimestamp = timestamp;

  player.tick(delta);  // advances pages, clears display and fires onComplete when done

  requestAnimationFrame(gameLoop);
}
requestAnimationFrame(gameLoop);
```

That is all. The player handles multi-page display, proportional timing across pages, and the completion callback entirely by itself. Your dialogue system just calls `start()` and waits for `onComplete`.

**4. Using a Canvas renderer instead.**

If your subtitle overlay is drawn on a canvas rather than DOM elements, swap in the `CanvasRenderer`:

```javascript
import { SubtitlePlayer, CanvasRenderer } from './game-subtitles-player.esm.js';

const canvas   = document.getElementById('game-canvas');
const renderer = new CanvasRenderer(canvas, '16px Arial');

const player = new SubtitlePlayer({ renderer, maxLines: 2 });
```

Everything else is the same.

#### `SubtitlePlayer` API

```javascript
const player = new SubtitlePlayer({ renderer, maxLines });

// Start playing a subtitle (stops any currently playing subtitle first)
player.start({ text, duration, onComplete });

// Call once per frame from your game loop
player.tick(deltaSeconds);

// Stop without firing onComplete
player.stop();

// Clear display and return to initial state
player.reset();

// Number of pages in the current layout (valid after start())
player.pageCount;

// Change lines-per-page (takes effect on the next start() call)
player.maxLines = 3;
```

#### `DomRenderer`

```javascript
const renderer = new DomRenderer(element);
```

Renders each line as a `<p>` inside `element`. Measures text width using the element's own computed CSS font, so it automatically respects whatever font you apply via your stylesheet.

If you change the element's font at runtime (e.g. to adjust font size), call `renderer.invalidateFont()` afterwards so the measurement cache is refreshed.

#### `CanvasRenderer`

```javascript
const renderer = new CanvasRenderer(
  canvas,       // HTMLCanvasElement
  '16px Arial', // CSS font string
  22            // optional: line height in px (defaults to 1.2 × font size)
);
```

#### Custom renderer

Any object with these four methods will work as a renderer, so you can integrate with any rendering system — Phaser, Pixi, a game engine WebGL layer, anything:

```javascript
const myRenderer = {
  measureLineWidth(text) { /* return pixel width of text as a number */ },
  getContainerWidth()    { /* return available width in pixels */ },
  render(lines)          { /* display the string[] of lines */ },
  clear()                { /* remove the current subtitle display */ },
};
```

---

### Unreal Engine Plugin (`players/player-unreal`)

The Unreal plugin provides the same player logic as a native C++ UE 5.7 plugin with full Blueprint support.

#### Setup

**From the Unreal release zip (`game-subtitles-unreal-v{version}.zip`):** copy `GameSubtitles/` into your project's `Plugins/` folder (create it if it does not exist). The zip also contains the preprocessor binaries (`game-subtitles-preprocess` / `game-subtitles-preprocess.exe`) — place these wherever is convenient on your machine (e.g. somewhere on your `PATH`).

**From another release zip:** the zip contains a `player-unreal/GameSubtitles/` folder. Copy that `GameSubtitles/` folder into your project's `Plugins/` folder.

**From source:** copy `players/player-unreal/GameSubtitles/` into your project's `Plugins/` folder instead.

Then:

1. Add `"GameSubtitles"` to your `.uproject` plugins list.
2. Add `"GameSubtitles"` to your module's `PublicDependencyModuleNames` in `Build.cs`.
3. Right-click the `.uproject` → *Generate Visual Studio project files*, then rebuild.

#### Real-world usage

**1. Add a `USubtitleWidget` to your HUD layout.**

In your Widget Blueprint, subclass `USubtitleWidget` and place a `UVerticalBox` named `TextContainer` wherever you want the subtitle lines to appear. Or use it programmatically:

```cpp
USubtitleWidget* SubWidget = CreateWidget<USubtitleWidget>(PlayerController, USubtitleWidget::StaticClass());
SubWidget->FontInfo = FSlateFontInfo(MyFontAsset, 16);
SubWidget->ContainerWidthOverride = 540.f; // set if calling Start() before the widget is on screen
SubWidget->AddToViewport();
```

**2. Create a player and point it at the widget.**

```cpp
USubtitlePlayer* Player = NewObject<USubtitlePlayer>(this);
Player->Initialize(SubWidget, /*MaxLines=*/2);

Player->OnComplete.AddDynamic(this, &AMyHUD::HandleSubtitleDone);
```

The player and widget are long-lived. Create them once and reuse them for every subtitle.

**3. When your dialogue system triggers a line, call `Start()`.**

```cpp
void AMyHUD::ShowSubtitle(const FString& Text, float DurationSeconds)
{
    Player->Start(Text, DurationSeconds);
}
```

`Start()` lays out the text, renders the first page immediately, and begins timing. If a subtitle is already playing it is stopped first.

**4. Drive it from your tick.**

From an `AActor`, `UActorComponent`, or `UUserWidget::NativeTick`:

```cpp
void AMyHUD::Tick(float DeltaSeconds)
{
    Super::Tick(DeltaSeconds);
    Player->Tick(DeltaSeconds);
}
```

#### `USubtitlePlayer` API

```cpp
// Create and configure
USubtitlePlayer* Player = NewObject<USubtitlePlayer>(this);
Player->Initialize(RendererObject, MaxLines);  // RendererObject implements ISubtitleRenderer

// Start playing (stops any currently playing subtitle first)
Player->Start(Text, DurationSeconds);

// Call once per frame
Player->Tick(DeltaSeconds);

// Stop without firing OnComplete
Player->Stop();

// Clear display and return to initial state
Player->Reset();

// Number of pages in the current layout (valid after Start())
Player->GetPageCount();

// Change lines-per-page (takes effect on the next Start())
Player->MaxLines = 3;

// Completion event (BlueprintAssignable dynamic multicast delegate)
Player->OnComplete.AddDynamic(this, &AMyActor::HandleDone);
```

All methods are also Blueprint-callable. The player is a plain `UObject` — not a component — so you own its lifetime and call `Tick` yourself. This matches the JS player's design exactly.

#### `USubtitleWidget`

```cpp
SubWidget->FontInfo             = FSlateFontInfo(FontAsset, 16);
SubWidget->TextColor            = FLinearColor::White;
SubWidget->ContainerWidthOverride = 540.f; // bypass geometry lookup before first layout pass
```

Text is measured using Slate's font measure service with the same `FontInfo`, so measurements always match what is rendered. To use a Blueprint-designed layout, subclass `USubtitleWidget` in a Widget Blueprint and add a `UVerticalBox` named **`TextContainer`**.

#### Custom renderer

Implement `ISubtitleRenderer` on any `UObject` to plug in a fully custom renderer:

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

Blueprint implementations are equally supported — bind the interface events in any Blueprint class.

#### Low-level layout API

The same layout functions used internally are exposed as static C++ helpers:

```cpp
#include "TextLayout.h"

// Wrap + paginate (MeasureWidth is any callable returning float)
TArray<TArray<FString>> Pages = FSubtitleTextLayout::WrapAndPaginate(
    Text, MeasureWidth, ContainerWidth, MaxLines);

// Allocate display time proportionally to character count
TArray<float> Timings = FSubtitleTextLayout::AllocateTimings(Pages, TotalDurationSeconds);
```

#### Demo project

`players/player-unreal/GameSubtitlesDemo/` is an Unreal project that mirrors the player-js `demo/index.html`:

- Same `subtitles.json` data (loaded from `Content/Demo/subtitles.json` at runtime)
- Script selector, **▶ Start** / **■ Stop** / **↺ Reset** buttons, 1×/2× speed toggle
- Lines-per-page ±, font size ±, progress bar, elapsed/total time, status line

Setup: copy the `GameSubtitles` plugin (from the Unreal release zip at `GameSubtitles/`, from another release zip at `player-unreal/GameSubtitles/`, or from source at `players/player-unreal/GameSubtitles/`) into `GameSubtitlesDemo/Plugins/GameSubtitles/`, right-click the `.uproject` → *Generate Visual Studio project files*, open in UE 5.7, and Play in Editor.

---

### Unity Package (`players/player-unity`)

The Unity package provides the same player logic as a native C# UPM package using TextMeshPro for font measurement and rendering. Requires **Unity 6.0** or later.

#### Setup

**From a release zip:** the zip contains a `player-unity/GameSubtitles/` folder. Copy `GameSubtitles/` into your project's `Packages/` folder (create it if it does not exist).

**From source:** copy `players/player-unity/GameSubtitles/` into your project's `Packages/` folder instead.

Then open `Packages/manifest.json` and add the package reference:

```json
{
  "dependencies": {
    "net.wildwinter.game-subtitles": "file:GameSubtitles"
  }
}
```

Unity will resolve the package from the local folder automatically. If you place it outside `Packages/`, adjust the relative path accordingly (Unity 6 resolves `file:` paths relative to the `Packages/` folder).

> **TextMeshPro:** In Unity 6, TMP is bundled inside `com.unity.ugui`. If the TMP Essential Resources have not been imported yet, go to **Window → TextMeshPro → Import TMP Essential Resources** after adding the package.

#### Real-world usage

**1. Add a `SubtitleWidget` to your uGUI layout.**

Attach `SubtitleWidget` to any `RectTransform` in your Canvas. It renders each subtitle line as a `TextMeshProUGUI` child and reports its own preferred height to the layout system.

```csharp
// Programmatic setup
var widgetGo = new GameObject("SubtitleWidget");
widgetGo.transform.SetParent(myCanvasTransform, false);
widgetGo.AddComponent<RectTransform>();
var widget = widgetGo.AddComponent<SubtitleWidget>();
widget.FontSize               = 16f;
widget.TextColor              = Color.white;
widget.ContainerWidthOverride = 540f; // set if calling Start() before the widget is laid out
```

**2. Create a player and point it at the widget.**

`SubtitlePlayer` is a plain C# class — not a MonoBehaviour. Create it from any MonoBehaviour and hold a reference to it.

```csharp
var player = new SubtitlePlayer();
player.Initialize(widget, maxLines: 2);

player.OnComplete += HandleSubtitleDone;
```

The player and widget are long-lived. Create them once and reuse them for every subtitle.

**3. When your dialogue system triggers a line, call `Start()`.**

```csharp
void ShowSubtitle(string text, float durationSeconds)
{
    player.Start(text, durationSeconds);
}
```

`Start()` lays out the text, renders the first page immediately, and begins timing. If a subtitle is already playing it is stopped first.

**4. Drive it from your Update loop.**

```csharp
void Update()
{
    player.Tick(Time.deltaTime);
}
```

#### `SubtitlePlayer` API

```csharp
// Create and configure
var player = new SubtitlePlayer();
player.Initialize(renderer, maxLines);  // renderer implements ISubtitleRenderer

// Start playing (stops any currently playing subtitle first)
player.Start(text, durationSeconds);

// Call once per frame
player.Tick(deltaTime);

// Stop without firing OnComplete
player.Stop();

// Clear display and return to initial state
player.Reset();

// Number of pages in the current layout (valid after Start())
player.PageCount;

// Change lines-per-page (takes effect on the next Start())
player.MaxLines = 3;

// Completion event
player.OnComplete += HandleDone;
```

#### `SubtitleWidget`

```csharp
widget.FontAsset              = myTMPFontAsset;  // optional; defaults to TMP default font
widget.FontSize               = 16f;
widget.TextColor              = Color.white;
widget.ContainerWidthOverride = 540f;            // bypass geometry lookup before first layout pass
```

Text is measured using TMP's `GetPreferredValues()` with the same font settings, so measurements always match what is rendered. The widget is a MonoBehaviour implementing `ISubtitleRenderer` and can be used anywhere in a standard uGUI hierarchy.

#### Custom renderer

Implement `ISubtitleRenderer` on any MonoBehaviour or plain C# class to plug in a fully custom renderer:

```csharp
public class MyRenderer : MonoBehaviour, ISubtitleRenderer
{
    public float MeasureLineWidth(string text) { /* return pixel width of text */ }
    public float GetContainerWidth()           { /* return available width in pixels */ }
    public void  Render(string[] lines)        { /* display the lines */ }
    public void  Clear()                       { /* remove the current subtitle display */ }
}
```

#### Low-level layout API

The same layout functions used internally are available as static C# helpers:

```csharp
using GameSubtitles;

// Wrap + paginate (measureFn is any Func<string, float> returning pixel width)
List<List<string>> pages = TextLayout.WrapAndPaginate(
    text, measureFn, containerWidth, maxLines);

// Allocate display time proportionally to character count
List<float> timings = TextLayout.AllocateTimings(pages, totalDurationSeconds);
```

#### Demo project

`players/player-unity/GameSubtitlesDemo/` is a Unity project that mirrors the player-js `demo/index.html` and the Unreal demo:

- Same `subtitles.json` data (loaded from `Assets/Demo/Resources/` at runtime)
- Script selector, **Start** / **Stop** / **Reset** buttons, 1×/2× speed toggle
- Lines-per-page ±, font size ±, progress bar, elapsed/total time, status line
- Entire UI constructed programmatically — no prefab or scene setup required

Setup:

1. Copy the `GameSubtitles` package (from the release zip at `player-unity/GameSubtitles/`, or from source at `players/player-unity/GameSubtitles/`) into `GameSubtitlesDemo/Packages/GameSubtitles/`.
2. Copy the four JSON files from `players/player-unreal/GameSubtitlesDemo/Content/Demo/` into `GameSubtitlesDemo/Assets/Demo/Resources/`.
3. Open the project in Unity 6.0 or later.
4. Go to **Window → TextMeshPro → Import TMP Essential Resources** if the prompt does not appear automatically.
5. Open the `Demo` scene and enter Play mode, or attach `GameSubtitlesDemo` to an empty GameObject in any scene.

## Security Issues

### Note on Windows Security

Because this is a hobbyist project, this app is **currently not digitally signed*** for Windows. If you receive an "Access Denied" or "Unauthorized" error when running this tool in PowerShell/CMD:

1. Right-click the EXE in File Explorer.

2. Select Properties.

3. At the bottom, check the Unblock box and click OK.

Try running the command again.

\* *Because it costs a lot and seems to be impossible outside North America right now for individual developers. Thanks Microsoft!*

### Note on Mac Security

The app is signed. Because it's easier on Mac.

## Contributors

* [Ian Thomas](https://github.com/wildwinter) — original author

## Acknowledgements

TeX hyphenation algorithm implemented by [NHyphenator](https://github.com/alkozko/NHyphenator).

### Hyphenation patterns

TeX hyphenation pattern files are from the [tex-hyphen](https://github.com/hyphenation/tex-hyphen) project and are embedded unmodified. Most are MIT-licensed; some (Russian, Dutch, Swedish, Finnish, Czech, Slovak, Hungarian, Ukrainian, Romanian, Bulgarian) are under LPPL 1.2 or 1.3. The LaTeX Project Public License permits unmodified redistribution of these files without any copyleft effect on surrounding code. Copyright notices in the pattern files are preserved as required.

## License

```text
MIT License

Copyright (c) 2026 Ian Thomas

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
