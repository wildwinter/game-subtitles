# Game Subtitles

**Game Subtitles** is a two-part subtitle system for games. The first part is a **preprocessor** — a command-line tool and C# library — that annotates your localised subtitle strings with soft hyphen markers before they ship. The second part is a **JavaScript render helper** that uses those markers at runtime to wrap and paginate subtitle text accurately, no matter what font or container size you are using.

Together they solve the situation where you have a long line of audio, a long subtitle that won't fit on the screen, and you want to simply say "display this text for this amount of time please".

**Game Subtitles** will take care of that for you, wrapping and breaking the text properly into multiple pages of text, and displaying them at the right time in the spoken line. And it will do this for lots of different languages.

## Why do I need this?

When displaying subtitles in a game you probably want to:

* Break long lines at sensible word-wrap points without cutting in the middle of a word.
* Hyphenate long words when there is no other option, in the correct place for the language.
* Split text across multiple pages if it is too long to fit in the subtitle box.
* Spend proportional time on each page, rather than a flat duration for everything.

The catch is that all of these things depend on the *actual rendered width* of each character, which you probably only know at runtime as it'll depend on user display settings. Pre-calculating it offline is hard, and different languages have wildly different hyphenation rules.

**Game Subtitles** splits the problem across two steps:

1. **Offline (at build or localization time):** the preprocessor reads your subtitle strings and inserts invisible [soft hyphen](https://en.wikipedia.org/wiki/Soft_hyphen) characters (U+00AD) at every point where a word could safely be broken, using the correct TeX hyphenation rules for the target language. These are zero-width when not used; they only produce a visible `-` if the renderer decides to break there.

2. **At runtime:** the JavaScript render helper uses those markers — together with the actual measured pixel widths of your font — to wrap and paginate text correctly, whatever the container size.

## Source Code

Source code is on [GitHub](https://github.com/wildwinter/game-subtitles), available under the MIT license.

## Releases

Pre-built releases are available in the [Releases](https://github.com/wildwinter/game-subtitles/releases) area on GitHub.

---

## Usage

### Overview

Here is a typical end-to-end flow:

1. **Run the preprocessor** on your subtitle strings — either via the CLI as part of your build pipeline, or by calling the C# library directly if you have a custom tool. This annotates every string with U+00AD markers and writes the result to a new file in the same format.
2. **Ship the annotated strings** with your game, in whichever format you use (CSV, JSON, XLSX, PO).
3. **At runtime,** load a subtitle string and pass it to `SubtitlePlayer`. It measures each word in your actual font, wraps to lines, paginates, and drives the display for you.
4. **In your game loop,** call `player.tick(delta)` each frame. The player advances pages automatically and calls your `onComplete` callback when the last page has been shown.

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

### JavaScript Render Helper

Copy either file from the distribution zip into your project:

| File | Format |
| --- | --- |
| `game-subtitles-render.esm.js` | ES module — `import { … } from '…'` |
| `game-subtitles-render.js` | IIFE — `window.GameSubtitles` |

#### Real-world usage

Here is how you would wire this up in a typical game with a DOM-based subtitle overlay.

**1. Set up the player once, pointing it at your subtitle element.**

```javascript
import { SubtitlePlayer, DomRenderer } from './game-subtitles-render.esm.js';

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
import { SubtitlePlayer, CanvasRenderer } from './game-subtitles-render.esm.js';

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

## Contributors

* [wildwinter](https://github.com/wildwinter) — original author

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
