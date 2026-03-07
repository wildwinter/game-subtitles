import { wrapAndPaginate, allocateTimings } from './TextLayout.js';

/**
 * Manages paginated subtitle display driven by caller-supplied ticks.
 *
 * The player is created once for a given renderer and line-count, then reused
 * across any number of subtitles by calling `start()` each time.
 *
 * @example
 * const player = new SubtitlePlayer({ renderer: domRenderer, maxLines: 2 });
 *
 * // In your dialogue system:
 * player.start({ text: 'Hello world', duration: 5, onComplete: next });
 *
 * // In your game loop:
 * player.tick(deltaSeconds);
 */
export class SubtitlePlayer {
  /**
   * @param {object} opts
   * @param {object}   opts.renderer     An object implementing the IRenderer interface.
   * @param {number}   [opts.maxLines=2] Lines per page (integer ≥ 1).
   */
  constructor({ renderer, maxLines = 2 }) {
    this._renderer = renderer;
    this._maxLines = maxLines;

    this._pages = [];
    this._timings = [];
    this._pageIndex = 0;
    this._elapsed = 0;
    this._running = false;
    this._done = false;
    this._onComplete = null;
  }

  /**
   * Loads a subtitle, lays out text, and renders page 0 immediately.
   * Calling this while another subtitle is playing stops it first.
   *
   * @param {object}   opts
   * @param {string}   opts.text        Text, may contain U+00AD soft hyphens.
   * @param {number}   opts.duration    Total display seconds (> 0).
   * @param {Function} [opts.onComplete] Called when all pages have been shown.
   */
  start({ text, duration, onComplete = null }) {
    this._running = false;
    this._renderer.clear();

    this._onComplete = onComplete;
    this._elapsed = 0;
    this._pageIndex = 0;
    this._done = false;

    const measure = t => this._renderer.measureLineWidth(t);
    const width   = this._renderer.getContainerWidth();
    this._pages   = wrapAndPaginate(text, measure, width, this._maxLines);
    this._timings = allocateTimings(this._pages, duration);

    this._running = true;
    this._renderCurrent();
  }

  /**
   * Number of pages in the current subtitle layout.
   * Valid after `start()` has been called; 0 before first call.
   * @returns {number}
   */
  get pageCount() { return this._pages.length; }

  /** Update the lines-per-page setting; takes effect on the next `start()`. */
  set maxLines(n) { this._maxLines = n; }

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
   * Call `start()` again to replay from the beginning.
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

  _renderCurrent() {
    this._renderer.render(this._pages[this._pageIndex]);
  }
}
