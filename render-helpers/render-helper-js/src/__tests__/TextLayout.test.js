import { describe, it, expect } from 'vitest';
import { wrapAndPaginate, allocateTimings } from '../TextLayout.js';

// Monospace measure: each character is 10 px wide.
const mono = text => text.length * 10;
const W = 100; // 10-char container; '…' = 1 char = 10 px → effectiveWidth = 90 px

describe('wrapAndPaginate', () => {
  it('returns one empty page for empty text', () => {
    expect(wrapAndPaginate('', mono, W, 2)).toEqual([[]]);
  });

  it('returns one empty page for whitespace-only text', () => {
    expect(wrapAndPaginate('   ', mono, W, 2)).toEqual([[]]);
  });

  it('fits a short word on a single page with no ellipsis', () => {
    expect(wrapAndPaginate('hello', mono, W, 2)).toEqual([['hello']]);
  });

  it('wraps two words that do not fit together', () => {
    // "hello" = 50 px, "world!" = 60 px; slot 0 uses 100 px → both fit on
    // slot 0 if they go together (110 px > 100 px → they don't), so "hello"
    // on slot 0, "world!" on slot 1 (last, effectiveWidth = 90 px, 60 ≤ 90).
    expect(wrapAndPaginate('hello world!', mono, W, 2)).toEqual([['hello', 'world!']]);
  });

  it('keeps two words on the same line when they fit', () => {
    // "hi bye" = 60 px ≤ 90 px effectiveWidth of slot 1 → single line.
    expect(wrapAndPaginate('hi bye', mono, W, 2)).toEqual([['hi bye']]);
  });

  it('strips soft hyphens when the word fits whole', () => {
    expect(wrapAndPaginate('inter\u00ADnet', mono, W, 2)).toEqual([['internet']]);
  });

  it('breaks at a soft hyphen when the word overflows', () => {
    // "inter\u00ADnational" = 130 px > 100 px; "inter-" (60 px) on slot 0,
    // "national" (80 px) on slot 1.
    expect(wrapAndPaginate('inter\u00ADnational', mono, W, 2)).toEqual([
      ['inter-', 'national'],
    ]);
  });

  it('picks the longest syllable prefix that fits', () => {
    // "in\u00ADter\u00ADna\u00ADtion" = 110 px; "interna-" (80 px) is the
    // longest prefix that fits in 100 px (slot 0).
    const pages = wrapAndPaginate('in\u00ADter\u00ADna\u00ADtion', mono, W, 2);
    expect(pages[0][0]).toBe('interna-');
    expect(pages[0][1]).toBe('tion');
  });

  it('forces a character break for a word wider than the container', () => {
    // "abcdefghijklmno" = 150 px; character-broken at 100 px (slot 0).
    const pages = wrapAndPaginate('abcdefghijklmno', mono, W, 2);
    expect(pages[0][0]).toBe('abcdefghij');
    expect(pages[0][1]).toBe('klmno');
  });

  it('wraps multiple words across multiple lines on one page', () => {
    // 4-char words: pairs fit in 100 px (90 px), triples do not.
    const pages = wrapAndPaginate('aaaa bbbb cccc dddd', mono, W, 2);
    expect(pages).toEqual([['aaaa bbbb', 'cccc dddd']]);
  });

  it('appends … to the last line of non-final pages', () => {
    // maxLines=1; "aaaaa" (50 px) fits in effectiveWidth 90 px; page break.
    expect(wrapAndPaginate('aaaaa bbbbb', mono, W, 1)).toEqual([
      ['aaaaa\u2026'],
      ['bbbbb'],
    ]);
  });

  it('does not append … to the final page', () => {
    const pages = wrapAndPaginate('aaaaa bbbbb ccccc', mono, W, 1);
    expect(pages[pages.length - 1]).toEqual(['ccccc']);
  });

  it('appends … to all non-final pages in a 3-page layout', () => {
    // 6-char words: "aaaaaa bbbbb" = 120 px > effectiveWidth 90 px → page break
    const pages = wrapAndPaginate('aaaaaa bbbbbb cccccc', mono, W, 1);
    expect(pages.length).toBe(3);
    expect(pages[0][0].endsWith('\u2026')).toBe(true);
    expect(pages[1][0].endsWith('\u2026')).toBe(true);
    expect(pages[2][0].endsWith('\u2026')).toBe(false);
  });

  it('never places a word fragment on the last line of a non-final page', () => {
    // "inter\u00ADnational" (130 px) arrives when slot 1 (last) is empty.
    // The page closes so the word retries on slot 0 of page 2 where
    // syllable-breaking is permitted.
    const pages = wrapAndPaginate('hello inter\u00ADnational', mono, W, 2);
    expect(pages[0]).toEqual(['hello\u2026']);
    expect(pages[1][0]).toBe('inter-');
    expect(pages[1][1]).toBe('national');
  });

  it('preserves all words across pages', () => {
    const text = 'one two three four five six seven eight nine ten';
    const pages = wrapAndPaginate(text, mono, W, 1);
    const allWords = pages.flat().join(' ').replace(/\u2026/g, '').trim().split(/\s+/);
    expect(allWords).toContain('one');
    expect(allWords).toContain('ten');
  });

  it('no rendered line contains a raw soft hyphen', () => {
    const text = 'in\u00adter\u00adna\u00adtion\u00adal\u00adi\u00adza\u00adtion is com\u00adplex';
    const pages = wrapAndPaginate(text, mono, W, 2);
    pages.flat().forEach(line => {
      expect(line).not.toContain('\u00ad');
    });
  });
});

describe('allocateTimings', () => {
  it('gives equal time to equal-length pages', () => {
    const timings = allocateTimings([['abc'], ['def']], 4);
    expect(timings[0]).toBeCloseTo(2);
    expect(timings[1]).toBeCloseTo(2);
  });

  it('does not count the continuation ellipsis towards timing', () => {
    // 'abc…' has the same vocalized length as 'abc'; timing must equal 'def'.
    const timings = allocateTimings([['abc\u2026'], ['def']], 4);
    expect(timings[0]).toBeCloseTo(2);
    expect(timings[1]).toBeCloseTo(2);
  });

  it('allocates proportionally by non-whitespace character count', () => {
    const timings = allocateTimings([['abc'], ['de']], 5);
    expect(timings[0]).toBeCloseTo(3);
    expect(timings[1]).toBeCloseTo(2);
  });

  it('ignores whitespace in character count', () => {
    const timings = allocateTimings([['a b'], ['cd']], 4);
    expect(timings[0]).toBeCloseTo(2);
    expect(timings[1]).toBeCloseTo(2);
  });

  it('handles a single page', () => {
    const timings = allocateTimings([['hello world']], 3);
    expect(timings[0]).toBeCloseTo(3);
  });

  it('gives a minimum of 1 count to an empty page (no div-by-zero)', () => {
    const timings = allocateTimings([['']], 2);
    expect(timings[0]).toBeCloseTo(2);
  });
});
