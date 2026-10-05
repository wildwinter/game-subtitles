# @wildwinter/game-subtitles

The JavaScript player from [Game Subtitles](https://github.com/wildwinter/game-subtitles): give it a subtitle and how long it should stay up, and it wraps the text to fit your subtitle box, hyphenates long words in the right places, splits the text into pages, and shows each page for its share of the time.

It measures text in your actual font at runtime, so it copes with any font, box size, or number of lines, including ones the player changes in the settings. Hyphenation comes from soft hyphens (U+00AD) that the Game Subtitles **preprocessor** adds to your localised strings at build time, using the TeX hyphenation rules for each language. The preprocessor, and players for Unity, Unreal, and Godot that behave the same way, are in the [GitHub repository](https://github.com/wildwinter/game-subtitles).

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

// For each line of dialogue:
player.start({
  text,                            // a string from the preprocessor's output
  duration: 4.5,                   // seconds; leave it out to estimate one from the text
  characterName: 'Tam',            // optional: shown as "Tam: " at the start of every page
  characterNameColor: '#f0cc88',   // optional
  lineColor: '#ffffff',            // optional
  onComplete: () => player.stop(), // the last page stays up until you stop the player
});

// Every frame, with the seconds since the last frame:
player.tick(deltaSeconds);
```

`start()` lays out the text and shows the first page straight away, stopping any subtitle already playing. Create the player and renderer once and reuse them for every line.

### Without a duration

If you leave out the duration, or pass 0 or less, the player estimates one with `estimateDuration`, which you can also call yourself:

```javascript
import { estimateDuration } from '@wildwinter/game-subtitles';

const seconds = estimateDuration(text, { charsPerSecond: 14, minSeconds: 1.5, maxSeconds: 18 });
```

It counts characters (Unicode code points, not counting soft hyphens), divides by the reading rate, and clamps the result; the options shown are the defaults. Counting characters rather than words means languages written without spaces, such as Chinese and Japanese, still get an estimate. The Unity, Unreal, and Godot players use the same rule, so the same text gets the same estimate everywhere.

## Renderers

**`DomRenderer(element)`** draws each line as a `<p>` inside `element`, and measures text in that element's own computed font, so it follows your stylesheet. The character name is a `<span>` with the class `gs-character-name` (exported as `CHARACTER_NAME_CLASS`); style that class and the renderer measures with it too. If you change the element's font at runtime, call `renderer.invalidateFont()`.

**`CanvasRenderer(canvas, font, lineHeight?, characterNameFont?)`** draws onto a canvas. `font` and `characterNameFont` are CSS font strings such as `'16px Arial'`; `lineHeight` defaults to 1.2 times the font size.

**Your own renderer** can be any object with these four methods, so the player works with any engine or drawing layer:

```typescript
import type { SubtitleRenderer } from '@wildwinter/game-subtitles';

const renderer: SubtitleRenderer = {
  measureLineWidth(text, useCharacterNameFont) { /* the width of text in your font */ },
  getContainerWidth()                          { /* the width available for a line */ },
  render(lines, characterContext)              { /* show the lines; draw "Name: " first if characterContext has a name */ },
  clear()                                      { /* remove the subtitle */ },
};
```

`measureLineWidth` must count trailing spaces, because the player measures `"Name: "` to reserve room for the character name.

## Layout on its own

The functions the player uses are exported too:

```javascript
import { wrapAndPaginate, allocateTimings } from '@wildwinter/game-subtitles';

const pages = wrapAndPaginate(text, measureWidth, containerWidth, maxLines); // string[][]
const timings = allocateTimings(pages, totalSeconds);                        // seconds per page
```

## Licence

MIT. See the [repository](https://github.com/wildwinter/game-subtitles) for the full documentation and the preprocessor.
