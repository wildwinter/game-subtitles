/**
 * Renders subtitle lines into an HTMLElement, one <p> per line.
 * Measures text width using a hidden off-screen span that inherits the
 * element's computed font styles.
 */
export class DomRenderer {
  /** @param {HTMLElement} element */
  constructor(element) {
    this._element = element;
    this._measureEl = null;
    this._measureElBold = null;
  }

  /**
   * @param {string}  text
   * @param {boolean} [bold=false] Measure in bold weight.
   * @returns {number} Pixel width of `text` in the element's font.
   */
  measureLineWidth(text, bold = false) {
    const el = this._ensureMeasureEl(bold);
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
   * @param {{ name: string|null, color: string|null, bold: boolean, lineColor: string|null }|null} [characterContext]
   */
  render(lines, characterContext = null) {
    this._element.innerHTML = '';
    const doc = this._element.ownerDocument;
    for (let i = 0; i < lines.length; i++) {
      const p = doc.createElement('p');
      if (characterContext?.lineColor) p.style.color = characterContext.lineColor;
      if (i === 0 && characterContext?.name) {
        // Prevent the browser word-wrapping this line if the bold prefix plus
        // the text land fractionally over the container width.  Any residual
        // overflow is clipped invisibly; text-align:center still applies.
        p.style.whiteSpace = 'nowrap';
        p.style.overflow   = 'hidden';
        const span = doc.createElement('span');
        span.textContent = `${characterContext.name}: `;
        // Name color is set explicitly on the span; it overrides the p-level lineColor.
        if (characterContext.color) span.style.color = characterContext.color;
        if (characterContext.bold) span.style.fontWeight = 'bold';
        p.appendChild(span);
        p.appendChild(doc.createTextNode(lines[i]));
      } else {
        p.textContent = lines[i];
      }
      this._element.appendChild(p);
    }
  }

  /** Removes all content from the container. */
  clear() {
    this._element.innerHTML = '';
  }

  /**
   * Invalidates the cached measure elements so the next measurement re-reads
   * the container's computed font.  Call this after changing the element's
   * font via CSS or inline style.
   */
  invalidateFont() {
    if (this._measureEl)     { this._measureEl.remove();     this._measureEl = null; }
    if (this._measureElBold) { this._measureElBold.remove(); this._measureElBold = null; }
  }

  /**
   * Returns (creating if needed) a hidden measurement span.
   * A separate persistent span is kept for bold to avoid CSS shorthand /
   * longhand interaction issues when toggling fontWeight on a shared element.
   * @param {boolean} bold
   */
  _ensureMeasureEl(bold) {
    const field = bold ? '_measureElBold' : '_measureEl';
    if (this[field]) return this[field];
    const doc = this._element.ownerDocument;
    const span = doc.createElement('span');
    span.style.cssText =
      'position:absolute;visibility:hidden;white-space:nowrap;left:-9999px;top:-9999px';
    // Mirror the container's font so measurements are accurate.
    const style = doc.defaultView.getComputedStyle(this._element);
    span.style.font = style.font;
    span.style.letterSpacing = style.letterSpacing;
    if (bold) span.style.fontWeight = 'bold';
    doc.body.appendChild(span);
    this[field] = span;
    return span;
  }
}
