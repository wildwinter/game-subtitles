/**
 * Renders subtitle lines onto an HTMLCanvasElement.
 *
 * @example
 * const r = new CanvasRenderer(canvas, '16px Arial');
 * r.render(['Hello,', 'world!']);
 */
export class CanvasRenderer {
  /**
   * @param {HTMLCanvasElement} canvas
   * @param {string} font  CSS font string, e.g. `"16px Arial"`.
   * @param {number} [lineHeight]  Pixel distance between baselines.
   *   Defaults to 1.2× the numeric font size parsed from `font`.
   */
  constructor(canvas, font, lineHeight) {
    this._canvas = canvas;
    this._ctx = canvas.getContext('2d');
    this._font = font;
    this._lineHeight = lineHeight ?? this._parseLineHeight(font);
  }

  /** @returns {number} Pixel width of `text` in the current font. */
  measureLineWidth(text) {
    this._ctx.font = this._font;
    return this._ctx.measureText(text).width;
  }

  /** @returns {number} Canvas pixel width. */
  getContainerWidth() {
    return this._canvas.width;
  }

  /**
   * Clears the canvas and draws each line of text.
   * @param {string[]} lines
   */
  render(lines) {
    this.clear();
    this._ctx.font = this._font;
    const lh = this._lineHeight;
    lines.forEach((line, i) => {
      this._ctx.fillText(line, 0, (i + 1) * lh);
    });
  }

  /** Clears the entire canvas. */
  clear() {
    this._ctx.clearRect(0, 0, this._canvas.width, this._canvas.height);
  }

  /** @param {string} font */
  _parseLineHeight(font) {
    const match = font.match(/(\d+(?:\.\d+)?)(px|pt)/);
    return match ? parseFloat(match[1]) * 1.2 : 19.2; // 16px × 1.2 fallback
  }
}
