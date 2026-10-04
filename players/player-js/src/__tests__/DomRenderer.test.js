import { describe, it, expect } from 'vitest';
import { DomRenderer, CHARACTER_NAME_CLASS } from '../renderers/DomRenderer.js';

// jsdom does no layout, so widths cannot be checked here; this pins the style that makes
// them right in a browser. Checked in headless Chrome: with white-space:nowrap the hidden
// span dropped the trailing space, so "Tam: " measured the same as "Tam:", while the drawn
// prefix kept it and the first line could overflow by a space.
describe('DomRenderer', () => {
  it('measures with white-space: pre so a trailing space is counted', () => {
    const el = document.createElement('div');
    document.body.appendChild(el);
    const renderer = new DomRenderer(el);
    renderer.measureLineWidth('Tam: ', true);
    renderer.measureLineWidth('Oh, go on then.');
    const spans = [...el.querySelectorAll('span')];
    expect(spans).toHaveLength(2);
    for (const span of spans) expect(span.style.whiteSpace).toBe('pre');
    expect(spans.some(s => s.classList.contains(CHARACTER_NAME_CLASS))).toBe(true);
    el.remove();
  });

  it('keeps the space after the name in the rendered prefix', () => {
    const el = document.createElement('div');
    const renderer = new DomRenderer(el);
    renderer.render(['Oh, go on then.'], { name: 'Tam', color: null, lineColor: null });
    expect(el.querySelector(`p .${CHARACTER_NAME_CLASS}`).textContent).toBe('Tam: ');
  });
});
