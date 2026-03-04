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
  }

  /** @returns {number} Pixel width of `text` in the element's font. */
  measureLineWidth(text) {
    const el = this._ensureMeasureEl();
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
   * @param {string[]} lines
   */
  render(lines) {
    this._element.innerHTML = '';
    for (const line of lines) {
      const p = this._element.ownerDocument.createElement('p');
      p.textContent = line;
      this._element.appendChild(p);
    }
  }

  /** Removes all content from the container. */
  clear() {
    this._element.innerHTML = '';
  }

  _ensureMeasureEl() {
    if (this._measureEl) return this._measureEl;
    const doc = this._element.ownerDocument;
    const span = doc.createElement('span');
    span.style.cssText =
      'position:absolute;visibility:hidden;white-space:nowrap;left:-9999px;top:-9999px';
    // Mirror the container's font so measurements are accurate.
    const style = doc.defaultView.getComputedStyle(this._element);
    span.style.font = style.font;
    span.style.letterSpacing = style.letterSpacing;
    doc.body.appendChild(span);
    this._measureEl = span;
    return span;
  }
}
