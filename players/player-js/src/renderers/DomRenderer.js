/**
 * CSS class applied to the character-name `<span>`. Style it from your own
 * stylesheet (e.g. `.gs-character-name { font-weight: bold; color: gold; }`) to
 * control how the character name looks; the renderer measures with the same class
 * so the layout reserves the correct space.
 */
export const CHARACTER_NAME_CLASS = 'gs-character-name';

/**
 * Renders subtitle lines into an HTMLElement, one <p> per line.
 * Measures text width using a hidden span inside the container, so it inherits the
 * container's computed font (and, for the character name, the same CSS class used
 * when rendering).
 */
export class DomRenderer {
  /** @param {HTMLElement} element */
  constructor(element) {
    this._element = element;
    this._measureEl = null;
    this._measureElName = null;
  }

  /**
   * @param {string}  text
   * @param {boolean} [useCharacterNameFont=false] Measure with the character-name styling.
   * @returns {number} Pixel width of `text` in the relevant font.
   */
  measureLineWidth(text, useCharacterNameFont = false) {
    const el = this._ensureMeasureEl(useCharacterNameFont);
    el.textContent = text;
    return el.getBoundingClientRect().width;
  }

  /** @returns {number} Inner pixel width of the container element. */
  getContainerWidth() {
    return this._element.getBoundingClientRect().width ||
      this._element.clientWidth;
  }

  /**
   * Clears the element and renders each line as a <p>.
   * When `characterContext` is provided, the first line is prefixed with
   * "Name: " rendered in a styled <span>.
   *
   * @param {string[]} lines
   * @param {{ name: string|null, color: string|null, lineColor: string|null }|null} [characterContext]
   */
  render(lines, characterContext = null) {
    this._clearLines();
    const doc = this._element.ownerDocument;
    for (let i = 0; i < lines.length; i++) {
      const p = doc.createElement('p');
      if (characterContext?.lineColor) p.style.color = characterContext.lineColor;
      if (i === 0 && characterContext?.name) {
        // Prevent the browser word-wrapping this line if the styled prefix plus
        // the text land fractionally over the container width.  Any residual
        // overflow is clipped invisibly; text-align:center still applies.
        p.style.whiteSpace = 'nowrap';
        p.style.overflow   = 'hidden';
        const span = doc.createElement('span');
        span.className = CHARACTER_NAME_CLASS;
        span.textContent = `${characterContext.name}: `;
        // Name color is set explicitly on the span; it overrides both the
        // p-level lineColor and any color from the CSS class.
        if (characterContext.color) span.style.color = characterContext.color;
        p.appendChild(span);
        p.appendChild(doc.createTextNode(lines[i]));
      } else {
        p.textContent = lines[i];
      }
      this._element.appendChild(p);
    }
  }

  /** Removes all rendered content from the container. */
  clear() {
    this._clearLines();
  }

  /**
   * Removes rendered line elements while preserving the hidden measurement spans,
   * which live inside the container so they inherit its font and CSS.
   */
  _clearLines() {
    for (const child of [...this._element.childNodes]) {
      if (child !== this._measureEl && child !== this._measureElName) {
        this._element.removeChild(child);
      }
    }
  }

  /**
   * Invalidates the cached measure elements so the next measurement re-reads
   * the container's computed font.  Call this after changing the element's
   * font via CSS or inline style.
   */
  invalidateFont() {
    if (this._measureEl)     { this._measureEl.remove();     this._measureEl = null; }
    if (this._measureElName) { this._measureElName.remove(); this._measureElName = null; }
  }

  /**
   * Returns (creating if needed) a hidden measurement span.
   *
   * The span is appended inside the container so it inherits the container's
   * computed font. The character-name span additionally carries
   * `CHARACTER_NAME_CLASS`, so any CSS targeting that class (e.g. a heavier
   * weight or different family) is reflected in the measurement — keeping the
   * reserved layout space in step with what is actually rendered.
   *
   * @param {boolean} useCharacterNameFont
   */
  _ensureMeasureEl(useCharacterNameFont) {
    const field = useCharacterNameFont ? '_measureElName' : '_measureEl';
    if (this[field]) return this[field];
    const doc = this._element.ownerDocument;
    const span = doc.createElement('span');
    // Absolutely positioned so it stays out of the container's flow/layout.
    span.style.cssText =
      'position:absolute;visibility:hidden;white-space:nowrap;left:-9999px;top:-9999px';
    if (useCharacterNameFont) span.className = CHARACTER_NAME_CLASS;
    this._element.appendChild(span);
    this[field] = span;
    return span;
  }
}
