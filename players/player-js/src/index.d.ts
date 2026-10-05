// Type declarations for @wildwinter/game-subtitles. The player is plain JavaScript; keep these
// in step with src/ when its public API changes. test/types.ts checks them under strict mode.

/** One page of subtitle text: the lines to show together. */
export type Page = string[];

/**
 * Styling for the first line of each page, passed to `SubtitleRenderer.render`.
 * The player passes one when a character name or a line colour is set, and `null` otherwise.
 */
export interface CharacterContext {
  /** The character name to show as "Name: " before the first line, or `null` for none. */
  name: string | null;
  /** CSS colour for the name, or `null` for the renderer's default. */
  color: string | null;
  /** CSS colour for the subtitle text on every line, or `null` for the renderer's default. */
  lineColor: string | null;
}

/**
 * What the player needs from a renderer. `DomRenderer` and `CanvasRenderer` implement it,
 * and any object with these four methods will do.
 */
export interface SubtitleRenderer {
  /**
   * The rendered width of `text`, in the same units as `getContainerWidth()`.
   * Trailing spaces must count: the player measures "Name: " to reserve room for the name.
   */
  measureLineWidth(text: string, useCharacterNameFont?: boolean): number;
  /** The width available for each line. */
  getContainerWidth(): number;
  /** Shows one page. When `characterContext` has a name, draw "Name: " before the first line. */
  render(lines: Page, characterContext?: CharacterContext | null): void;
  /** Removes the current subtitle. */
  clear(): void;
}

export interface EstimateDurationOptions {
  /** Reading rate. Values of 0 or less fall back to 14. Default 14. */
  charsPerSecond?: number;
  /** Shortest estimate returned, in seconds. Default 1.5. */
  minSeconds?: number;
  /** Longest estimate returned, in seconds. Default 18. */
  maxSeconds?: number;
}

/**
 * Estimates how long a subtitle should stay on screen when no duration is known: characters
 * (Unicode code points, not counting U+00AD soft hyphens) divided by the reading rate, clamped.
 * The Unity, Unreal, and Godot players use the same rule and defaults.
 */
export function estimateDuration(text: string | null | undefined, options?: EstimateDurationOptions): number;

export interface SubtitlePlayerOptions {
  renderer: SubtitleRenderer;
  /** Lines per page. Default 2. */
  maxLines?: number;
}

export interface StartOptions {
  /** The subtitle text; may contain U+00AD soft hyphens from the preprocessor. */
  text: string;
  /** Total display time in seconds. When missing or 0 or less, `estimateDuration(text)` is used. */
  duration?: number;
  /** Called once the last page has been shown. The last page stays up until `stop()`. */
  onComplete?: (() => void) | null;
  /** Shown as "Name: " at the start of every page. */
  characterName?: string | null;
  /** CSS colour for the character name. */
  characterNameColor?: string | null;
  /** CSS colour for the subtitle text on every line. */
  lineColor?: string | null;
}

/** Lays out subtitles into pages and shows them in turn, driven by `tick()`. */
export class SubtitlePlayer {
  constructor(options: SubtitlePlayerOptions);
  /** Lays out the text and shows its first page, stopping any subtitle already playing. */
  start(options: StartOptions): void;
  /** Number of pages in the current layout; 0 before `start()`. */
  readonly pageCount: number;
  /** Lines per page; a change takes effect on the next `start()`. */
  set maxLines(n: number);
  /** Advances the clock. Call once per frame with the seconds since the last call. */
  tick(deltaSeconds: number): void;
  /** Clears the display and returns to the state before `start()`. */
  reset(): void;
  /** Stops playback and clears the display, without calling `onComplete`. */
  stop(): void;
}

/**
 * Wraps `text` into pages of at most `maxLines` lines, breaking long words at soft hyphens
 * (U+00AD) and appending "…" to every page but the last.
 */
export function wrapAndPaginate(
  text: string,
  measureWidth: (text: string) => number,
  containerWidth: number,
  maxLines: number,
  firstLineIndent?: number,
): Page[];

/** Splits `totalDuration` seconds across pages in proportion to their non-whitespace characters. */
export function allocateTimings(pages: Page[], totalDuration: number): number[];

/** The CSS class `DomRenderer` gives the character name's `<span>`, `"gs-character-name"`. */
export const CHARACTER_NAME_CLASS: 'gs-character-name';

/**
 * Renders each line as a `<p>` inside an element, measuring with the element's own computed
 * font. Style the character name with the `CHARACTER_NAME_CLASS` class.
 */
export class DomRenderer implements SubtitleRenderer {
  constructor(element: HTMLElement);
  measureLineWidth(text: string, useCharacterNameFont?: boolean): number;
  getContainerWidth(): number;
  render(lines: Page, characterContext?: CharacterContext | null): void;
  clear(): void;
  /** Call after changing the element's font, so measurements re-read it. */
  invalidateFont(): void;
}

/** Draws subtitle lines onto a canvas. */
export class CanvasRenderer implements SubtitleRenderer {
  /**
   * @param font              CSS font for the subtitle text, such as `"16px Arial"`.
   * @param lineHeight        Pixels between baselines. Defaults to 1.2 times the font size.
   * @param characterNameFont CSS font for the character name. Defaults to `font`.
   */
  constructor(canvas: HTMLCanvasElement, font: string, lineHeight?: number, characterNameFont?: string);
  measureLineWidth(text: string, useCharacterNameFont?: boolean): number;
  getContainerWidth(): number;
  render(lines: Page, characterContext?: CharacterContext | null): void;
  clear(): void;
}
