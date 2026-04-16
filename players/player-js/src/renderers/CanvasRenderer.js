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

  /**
   * @param {string}  text
   * @param {boolean} [bold=false] Measure in bold weight.
   * @returns {number} Pixel width of `text` in the current font.
   */
  measureLineWidth(text, bold = false) {
    this._ctx.font = bold ? this._makeBoldFont(this._font) : this._font;
    const width = this._ctx.measureText(text).width;
    if (bold) this._ctx.font = this._font; // restore
    return width;
  }

  /** @returns {number} Canvas pixel width. */
  getContainerWidth() {
    return this._canvas.width;
  }

  /**
   * Clears the canvas and draws each line of text.
   * When `characterContext` is provided, the first line is prefixed with
   * "Name: " drawn in the specified color and optionally bold weight.
   *
   * @param {string[]} lines
   * @param {{ name: string|null, color: string|null, bold: boolean, lineColor: string|null }|null} [characterContext]
   */
  render(lines, characterContext = null) {
    this.clear();
    this._ctx.font = this._font;
    const lh = this._lineHeight;
    const defaultFill = this._ctx.fillStyle;
    const lineFill = characterContext?.lineColor ?? defaultFill;
    lines.forEach((line, i) => {
      const y = (i + 1) * lh;
      if (i === 0 && characterContext?.name) {
        const prefix = `${characterContext.name}: `;
        // Draw the character name prefix (optionally bold, optionally colored)
        if (characterContext.bold) this._ctx.font = this._makeBoldFont(this._font);
        this._ctx.fillStyle = characterContext.color ?? defaultFill;
        this._ctx.fillText(prefix, 0, y);
        const prefixWidth = this._ctx.measureText(prefix).width;
        // Draw the subtitle body text in the line color
        this._ctx.font      = this._font;
        this._ctx.fillStyle = lineFill;
        this._ctx.fillText(line, prefixWidth, y);
      } else {
        this._ctx.fillStyle = lineFill;
        this._ctx.fillText(line, 0, y);
      }
    });
    this._ctx.fillStyle = defaultFill; // restore
  }

  /** @param {string} font  @returns {string} */
  _makeBoldFont(font) {
    return /^\s*bold\b/i.test(font) ? font : `bold ${font}`;
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
