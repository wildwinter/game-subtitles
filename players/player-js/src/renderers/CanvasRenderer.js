/**
 * Renders subtitle lines onto an HTMLCanvasElement.
 *
 * @example
 * const r = new CanvasRenderer(canvas, '16px Arial');
 * r.render(['Hello,', 'world!']);
 *
 * @example
 * // Give the character name its own font:
 * const r = new CanvasRenderer(canvas, '16px Arial', 22, 'bold 18px Arial');
 */
export class CanvasRenderer {
  /**
   * @param {HTMLCanvasElement} canvas
   * @param {string} font  CSS font string for the subtitle body, e.g. `"16px Arial"`.
   * @param {number} [lineHeight]  Pixel distance between baselines.
   *   Defaults to 1.2× the numeric font size parsed from `font`.
   * @param {string} [characterNameFont]  CSS font string for the character-name prefix.
   *   Defaults to `font` (canvas cannot read CSS classes, so this must be explicit).
   */
  constructor(canvas, font, lineHeight, characterNameFont) {
    this._canvas = canvas;
    this._ctx = canvas.getContext('2d');
    this._font = font;
    this._characterNameFont = characterNameFont ?? font;
    this._lineHeight = lineHeight ?? this._parseLineHeight(font);
  }

  /**
   * @param {string}  text
   * @param {boolean} [useCharacterNameFont=false] Measure with the character-name font.
   * @returns {number} Pixel width of `text` in the relevant font.
   */
  measureLineWidth(text, useCharacterNameFont = false) {
    this._ctx.font = useCharacterNameFont ? this._characterNameFont : this._font;
    const width = this._ctx.measureText(text).width;
    if (useCharacterNameFont) this._ctx.font = this._font; // restore
    return width;
  }

  /** @returns {number} Canvas pixel width. */
  getContainerWidth() {
    return this._canvas.width;
  }

  /**
   * Clears the canvas and draws each line of text.
   * When `characterContext` is provided, the first line is prefixed with
   * "Name: " drawn in the character-name font and the specified color.
   *
   * @param {string[]} lines
   * @param {{ name: string|null, color: string|null, lineColor: string|null }|null} [characterContext]
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
        // Draw the character name prefix in the character-name font (optionally colored)
        this._ctx.font     = this._characterNameFont;
        this._ctx.fillStyle = characterContext.color ?? defaultFill;
        this._ctx.fillText(prefix, 0, y);
        const prefixWidth = this._ctx.measureText(prefix).width;
        // Draw the subtitle body text in the subtitle font and line color
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
