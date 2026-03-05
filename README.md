# Game Subtitles

A two-component subtitle system for games:

1. **Subtitle Preprocessor** — C# library and cross-platform CLI that inserts soft hyphen (U+00AD) markers into localised strings so they can be word-wrapped safely at runtime.
2. **Render Helper** — Browser JavaScript library that renders paginated subtitles with caller-driven timing.

---

## Quick Start

```bash
# Build everything
npm run build

# Run all tests (C# + JS)
npm test

# Produce distribution zips
npm run dist
```

---

## Subtitle Preprocessor

### Installation

Pre-built binaries are included in the distribution zips:

| Zip | Contents |
| --- | --- |
| `game-subtitles-win-v{version}.zip` | `game-subtitles-preprocess.exe` + JS render helper + README |
| `game-subtitles-osx-v{version}.zip` | `game-subtitles-preprocess` (arm64) + JS render helper + README |
| `game-subtitles-lib-v{version}.zip` | `PreprocessorLib.dll` + JS render helper + README |

### CLI Usage

```text
game-subtitles-preprocess <input> -o <output> [options]

Arguments:
  <input>               File or folder path (required)

Options:
  -o, --output <path>   Output file or folder (required)
  -f, --field <name>    Field to process in CSV / JSON / XLSX files
  -l, --lang <code>     Language code e.g. en_GB, fr_FR (default: en_US)
  --force-overwrite     Always reprocess files, even if the output is already up to date
  -h, --help
  --version

Exit codes: 0 = success, 1 = warnings, 2 = error
```

By default the tool skips any output file that is already newer than the corresponding input file (make-style up-to-date check). Pass `--force-overwrite` to reprocess regardless.

**Examples:**

```bash
# Process a PO file into French
game-subtitles-preprocess strings.po -o out.po -l fr_FR

# Process a JSON array — field name is case-insensitive
game-subtitles-preprocess subtitles.json -o out.json --field subtitle -l de_DE

# Process a folder of CSV files
game-subtitles-preprocess ./loc/ -o ./out/ --field text -l es_ES

# Process XLSX — first sheet, named column
game-subtitles-preprocess sheet.xlsx -o out.xlsx --field Subtitle -l en_GB
```

### What the preprocessor does

The tool reads each subtitle string and inserts **soft hyphen** (U+00AD) characters at every safe hyphenation point according to the TeX rules for the chosen language. No visible characters are added or removed — the soft hyphens are invisible unless the renderer decides to break a line there, in which case it displays a `-` at the break point.

Example (`en_US`):

```text
Input:  "Internationalization is hard."
Output: "In|ter|na|tion|al|i|za|tion is hard."
         ↑  ↑  ↑   ↑  ↑ ↑  ↑   soft hyphens (U+00AD), shown here as | — invisible at runtime
```

Because soft hyphens are non-ASCII Unicode characters, **it is essential that your source files are saved and read as UTF-8**. See the format-specific notes below.

### Supported File Formats

| Format | Rules |
| --- | --- |
| `.po` | Processes all `msgstr` values; `--field` is ignored (warning if supplied) |
| `.csv` | `--field` required; processes named column; all other columns pass through unchanged |
| `.json` | `--field` required; input must be a JSON array of objects `[{…}]`; top-level key match is case-insensitive |
| `.xlsx` | `--field` required; first sheet only; header row on row 1 |

#### CSV and the UTF-8 BOM

The CSV formatter mirrors the byte-order mark (BOM) of the input file in the output file — if the input starts with a UTF-8 BOM (`EF BB BF`) the output will too; if not, neither will the output.

> **Strongly recommended:** always save CSV input files as **UTF-8 with BOM**.
>
> Without a BOM, many tools (notably Microsoft Excel on Windows) assume a legacy code page and will silently misread the soft hyphens and any other non-ASCII characters in the file, corrupting your data before it ever reaches this tool. A UTF-8 BOM is the standard signal that tells these tools the file is Unicode.

Most modern editors (VS Code, Notepad++, JetBrains IDEs) have an explicit "Save with BOM" or "UTF-8 BOM" option in their encoding selector. The BOM has no effect on command-line tools, Python's `csv` module, or any other well-behaved UTF-8 reader.

### Library API (C#)

```csharp
using GameSubtitles.Lib;

var preprocessor = new SubtitlePreprocessor();

// Process a string — inserts U+00AD at safe hyphenation points
string result = preprocessor.Process("Internationalization is hard.", "en_US");

// List bundled language codes
IReadOnlyList<string> langs = SubtitlePreprocessor.SupportedLanguages;
```

### Supported Languages

| Language | Code(s) | Hyphenation License |
| --- | --- | --- |
| English (US) | `en_US`, `en` | MIT |
| English (GB) | `en_GB` | MIT |
| French | `fr_FR`, `fr` | MIT |
| Italian | `it_IT`, `it` | MIT |
| German (Reform 1996) | `de_DE`, `de` | MIT |
| Spanish | `es_ES`, `es` | MIT |
| Russian | `ru_RU`, `ru` | LPPL 1.2 ¹ |
| Polish | `pl_PL`, `pl` | MIT |
| Portuguese | `pt_PT`, `pt_BR`, `pt` | MIT |
| Dutch | `nl_NL`, `nl` | LPPL 1.2 ¹ |
| Swedish | `sv_SE`, `sv` | LPPL 1.2 ¹ |
| Norwegian Bokmål | `nb_NO`, `nb` | MIT |
| Danish | `da_DK`, `da` | MIT |
| Finnish | `fi_FI`, `fi` | LPPL 1.3c ¹ |
| Czech | `cs_CZ`, `cs` | LPPL 1.3 ¹ |
| Slovak | `sk_SK`, `sk` | LPPL 1.3 ¹ |
| Hungarian | `hu_HU`, `hu` | LPPL 1.3c ¹ |
| Turkish | `tr_TR`, `tr` | MIT |
| Ukrainian | `uk_UA`, `uk` | LPPL 1.3 ¹ |
| Croatian | `hr_HR`, `hr` | MIT |
| Romanian | `ro_RO`, `ro` | LPPL 1.2 ¹ |
| Bulgarian | `bg_BG`, `bg` | LPPL 1.2 ¹ |

¹ **LPPL (LaTeX Project Public License)**: Distributing the pattern files _unmodified_ is permitted without restriction — no copyleft effect on your project code. Copyright notices must be preserved. See [https://latex-project.org/lppl/](https://latex-project.org/lppl/) for full terms.

Language patterns are from the [tex-hyphen](https://github.com/hyphenation/tex-hyphen) project.

---

## Render Helper (JavaScript)

### Setup

Copy `game-subtitles-render.esm.js` (ES module) or `game-subtitles-render.js` (IIFE, `window.GameSubtitles`) from the distribution zip into your project.

### API

```javascript
// ES module
import { SubtitlePlayer, DomRenderer, CanvasRenderer } from './game-subtitles-render.esm.js';

// IIFE
const { SubtitlePlayer, DomRenderer, CanvasRenderer } = window.GameSubtitles;
```

#### `SubtitlePlayer`

```javascript
const player = new SubtitlePlayer({
  text,          // string — may contain U+00AD soft hyphens from the preprocessor
  duration,      // number — total seconds to display all pages (> 0)
  maxLines,      // number — lines per page (integer ≥ 1, default 2)
  renderer,      // IRenderer instance (DomRenderer or CanvasRenderer)
  onComplete,    // () => void — called when all pages have been shown (optional)
});

player.start();              // layout + render page 0 immediately
player.tick(deltaSeconds);   // call each frame — advances pages, fires onComplete when done
player.reset();              // clear + return to initial state (call start() again to replay)
player.stop();               // stop without firing onComplete
```

#### `DomRenderer`

```javascript
const renderer = new DomRenderer(element); // HTMLElement
```

Renders each line as a `<p>` inside `element`. Measures text using the element's computed font.

#### `CanvasRenderer`

```javascript
const renderer = new CanvasRenderer(
  canvas,      // HTMLCanvasElement
  '16px Arial' // CSS font string
  // optional: lineHeight (px) — defaults to 1.2 × font-size
);
```

#### Implementing a custom renderer

Any object satisfying this interface works:

```javascript
{
  measureLineWidth(text)  // (string) => number — pixel width
  getContainerWidth()     // () => number — container pixel width
  render(lines)           // (string[]) => void — display lines
  clear()                 // () => void — clear display
}
```

### Text Layout Functions

```javascript
import { wrapText, paginateLines, allocateTimings } from './game-subtitles-render.esm.js';

// Wrap text into lines
const lines = wrapText(text, measureWidth, containerWidth);

// Split lines into pages
const pages = paginateLines(lines, maxLines);

// Allocate display time proportionally
const timings = allocateTimings(pages, totalDuration); // number[] in seconds
```

---

## Building from Source

### Prerequisites

- Node.js 18+
- .NET SDK 8.0+

### Commands

```bash
# Install npm dependencies
npm install
npm install --prefix render-helpers/render-helper-js

# Build
npm run build

# Test
npm test

# Dist — produces dist/*.zip
npm run dist
```

---

## Third-Party Libraries

### C# / NuGet

| Library | Version | License | Purpose |
| --- | --- | --- | --- |
| [NHyphenator](https://github.com/alkozko/NHyphenator) | 2.0.0 | Apache 2.0 | Knuth-Liang TeX hyphenation algorithm |
| [CsvHelper](https://joshclose.github.io/CsvHelper/) | 33.1.0 | MS-PL / Apache 2.0 | CSV read/write |
| [ClosedXML](https://github.com/ClosedXML/ClosedXML) | 0.105.0 | MIT | XLSX read/write |
| [Karambolo.PO](https://github.com/adams85/po) | 1.12.0 | MIT | PO file parsing |
| [System.CommandLine](https://github.com/dotnet/command-line-api) | 2.0.0-beta5 | MIT | CLI argument parsing |
| [xunit](https://xunit.net/) | 2.9.3 | Apache 2.0 | Unit testing (dev only) |

### JavaScript / npm

| Library | Version | License | Purpose |
| --- | --- | --- | --- |
| [esbuild](https://esbuild.github.io/) | ^0.24.0 | MIT | JS bundling (dev only) |
| [vitest](https://vitest.dev/) | ^2.0.0 | MIT | Unit testing (dev only) |
| [jsdom](https://github.com/jsdom/jsdom) | ^24.0.0 | MIT | DOM emulation in tests (dev only) |
| [archiver](https://github.com/archiverjs/node-archiver) | ^7.0.0 | MIT | ZIP creation in dist script (dev only) |

### Hyphenation Patterns

TeX hyphenation pattern files are from the [tex-hyphen](https://github.com/hyphenation/tex-hyphen) project.
Files are distributed unmodified; per-language licenses are listed in the Supported Languages table above.
LPPL 1.2/1.3 permits unmodified redistribution without copyleft effect on surrounding code.

---

## License

MIT — see [LICENSE](LICENSE).
