import { wrapText, paginateLines, allocateTimings } from './TextLayout.js';

/**
 * Manages paginated subtitle display driven by caller-supplied ticks.
 *
 * @example
 * const player = new SubtitlePlayer({
 *   text: 'Hello\u00ADworld and more',
 *   duration: 5,
 *   maxLines: 2,
 *   renderer: domRenderer,
 *   onComplete: () => console.log('done'),
 * });
 * player.start();
 * // In your game loop:
 * player.tick(deltaSeconds);
 */
export class SubtitlePlayer {
  /**
   * @param {object} opts
   * @param {string}   opts.text        Text, may contain U+00AD soft hyphens.
   * @param {number}   opts.duration    Total display seconds (> 0).
   * @param {number}   [opts.maxLines=2] Lines per page (integer ≥ 1).
   * @param {object}   opts.renderer    An object implementing the IRenderer interface.
   * @param {Function} [opts.onComplete] Called when all pages have been shown.
   */
  constructor({ text, duration, maxLines = 2, renderer, onComplete }) {
    this._text = text;
    this._duration = duration;
    this._maxLines = maxLines;
    this._renderer = renderer;
    this._onComplete = onComplete ?? null;

    this._pages = [];
    this._timings = [];
    this._pageIndex = 0;
    this._elapsed = 0;
    this._running = false;
    this._done = false;
  }

  /**
   * Lays out the text, resets state, renders page 0 immediately.
   * Safe to call multiple times (restarts from the beginning each time).
   */
  start() {
    this._layout();
    this._pageIndex = 0;
    this._elapsed = 0;
    this._running = true;
    this._done = false;
    this._renderCurrent();
  }

  /**
   * Advances the internal clock. Call this once per frame from your game loop.
   * When the current page's time expires, advances to the next page.
   * Fires `onComplete` and stops when the last page expires.
   *
   * @param {number} deltaSeconds  Time elapsed since the last tick.
   */
  tick(deltaSeconds) {
    if (!this._running || this._done) return;

    this._elapsed += deltaSeconds;

    while (this._elapsed >= this._timings[this._pageIndex]) {
      this._elapsed -= this._timings[this._pageIndex];
      this._pageIndex++;

      if (this._pageIndex >= this._pages.length) {
        this._done = true;
        this._running = false;
        this._renderer.clear();
        if (this._onComplete) this._onComplete();
        return;
      }

      this._renderCurrent();
    }
  }

  /**
   * Clears the renderer and resets to the initial (pre-start) state.
   * Call `start()` again to play from the beginning.
   */
  reset() {
    this._running = false;
    this._done = false;
    this._pageIndex = 0;
    this._elapsed = 0;
    this._pages = [];
    this._timings = [];
    this._renderer.clear();
  }

  /**
   * Stops playback and clears the renderer without firing `onComplete`.
   */
  stop() {
    this._running = false;
    this._renderer.clear();
  }

  _layout() {
    const lines = wrapText(
      this._text,
      t => this._renderer.measureLineWidth(t),
      this._renderer.getContainerWidth(),
    );
    this._pages = paginateLines(lines, this._maxLines);
    this._timings = allocateTimings(this._pages, this._duration);
  }

  _renderCurrent() {
    this._renderer.render(this._pages[this._pageIndex]);
  }
}
