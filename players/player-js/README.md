# @wildwinter/game-subtitles

The JavaScript player from [Game Subtitles](https://github.com/wildwinter/game-subtitles): give it a subtitle and how long it should stay up, and it wraps the text to fit your subtitle box, hyphenates long words in the right places, splits the text into pages, and shows each page for its share of the time.

It measures text in your actual font at runtime, so it copes with any font, box size, or number of lines, including ones changed in your game's settings. Hyphenation comes from soft hyphens (U+00AD) that the Game Subtitles **preprocessor** adds to your localised strings at build time, using the TeX hyphenation rules for each language. The preprocessor, and players for Unity, Unreal, and Godot that behave the same way, are in the [GitHub repository](https://github.com/wildwinter/game-subtitles).

## Install

```bash
npm install @wildwinter/game-subtitles
```

The package is ES modules, with TypeScript types included. For a plain `<script>` tag there is also a build that defines a `GameSubtitles` global, at `@wildwinter/game-subtitles/iife` (`dist/game-subtitles-player.js`).

## Usage

```javascript
import { SubtitlePlayer, DomRenderer } from '@wildwinter/game-subtitles';

// Once: a renderer for your subtitle element, and a player that uses it.
const renderer = new DomRenderer(document.getElementById('subtitle-bar'));
const player = new SubtitlePlayer({ renderer, maxLines: 2 });

// For each line of dialogue. The text comes from the preprocessor's output, so it
// carries soft hyphens (U+00AD) where words may break.
player.start({
  text: 'Im\u00adpos\u00adsi\u00adble. The re\u00adinforce\u00adments should have ar\u00adrived hours ago.',
  duration: 4.5,                   // seconds; leave it out to estimate one from the text
  characterName: 'Kael',           // optional: shown as "Kael: " at the start of every page
  characterNameColor: '#f0cc88',   // optional
  lineColor: '#ffffff',            // optional
  onComplete: () => player.stop(), // the last page stays up until you stop the player
});

// Every frame, with the seconds since the last frame:
let last = performance.now();
function frame(now) {
  player.tick((now - last) / 1000);
  last = now;
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);
```

`start()` lays out the text and shows the first page straight away, stopping any subtitle already playing. Create the player and renderer once and reuse them for every line.

### Without a duration

If you leave out the duration, or pass 0 or less, the player estimates one with `estimateDuration`, which you can also call yourself:

```javascript
import { estimateDuration } from '@wildwinter/game-subtitles';

const seconds = estimateDuration('Get down, now! They have seen us.', { charsPerSecond: 14, minSeconds: 1.5, maxSeconds: 18 });
```

It counts characters (Unicode code points, not counting soft hyphens), divides by the reading rate, and clamps the result; the options shown are the defaults. Counting characters rather than words means languages written without spaces, such as Chinese and Japanese, still get an estimate. The Unity, Unreal, and Godot players use the same rule, so the same text gets the same estimate everywhere.

## Renderers

**`DomRenderer(element)`** draws each line as a `<p>` inside `element`, and measures text in that element's own computed font, so it follows your stylesheet. The character name is a `<span>` with the class `gs-character-name` (exported as `CHARACTER_NAME_CLASS`); style that class and the renderer measures with it too. If you change the element's font at runtime, call `renderer.invalidateFont()`.

**`CanvasRenderer(canvas, font, lineHeight?, characterNameFont?)`** draws onto a canvas. `font` and `characterNameFont` are CSS font strings such as `'16px Arial'`; `lineHeight` defaults to 1.2 times the font size.

**Your own renderer** can be any object with these four methods, so the player works with any engine or drawing layer. This one measures with a canvas and shows the lines as plain text:

```typescript
import { SubtitlePlayer, type SubtitleRenderer } from '@wildwinter/game-subtitles';

const measure = document.createElement('canvas').getContext('2d')!;
const box = document.getElementById('subtitles')!;

const renderer: SubtitleRenderer = {
  // The width of the text in your font. Pass true to measure in the character name's font.
  measureLineWidth(text, useCharacterNameFont) {
    measure.font = useCharacterNameFont ? 'bold 18px Arial' : '16px Arial';
    return measure.measureText(text).width;
  },
  // The width available for a line, in the same units.
  getContainerWidth() {
    return box.clientWidth;
  },
  // Show one page. When characterContext has a name, show "Name: " before the first line.
  render(lines, characterContext) {
    const prefix = characterContext?.name ? `${characterContext.name}: ` : '';
    box.textContent = prefix + lines.join('\n');
  },
  clear() {
    box.textContent = '';
  },
};

const player = new SubtitlePlayer({ renderer });
```

`measureLineWidth` must count trailing spaces, because the player measures `"Name: "` to reserve room for the character name. A canvas's `measureText` does; TextMeshPro, for one, does not.

## Layout on its own

The functions the player uses are exported too:

```javascript
import { wrapAndPaginate, allocateTimings } from '@wildwinter/game-subtitles';

// Any function that returns the width of a string will do; here, 10 pixels a character.
const measureWidth = text => text.length * 10;

const pages = wrapAndPaginate('Get down, now! They have seen us.', measureWidth, 200, 2); // string[][]
const timings = allocateTimings(pages, 4);                                              // seconds per page
```

## Licence

MIT. See the [repository](https://github.com/wildwinter/game-subtitles) for the full documentation and the preprocessor.
