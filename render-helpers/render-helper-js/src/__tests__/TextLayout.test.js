import { describe, it, expect } from 'vitest';
import { wrapText, paginateLines, allocateTimings } from '../TextLayout.js';

// Monospace measure: each character is 10px wide.
const mono = text => text.length * 10;
const W = 100; // 10 characters per line

describe('wrapText', () => {
  it('returns [] for empty string', () => {
    expect(wrapText('', mono, W)).toEqual([]);
  });

  it('returns [] for whitespace-only string', () => {
    expect(wrapText('   ', mono, W)).toEqual([]);
  });

  it('fits a short word on a single line', () => {
    expect(wrapText('hello', mono, W)).toEqual(['hello']);
  });

  it('wraps two words that do not fit together', () => {
    // "hello" = 50px, "world!" = 60px → "hello world!" = 110px > 100px
    expect(wrapText('hello world!', mono, W)).toEqual(['hello', 'world!']);
  });

  it('keeps two words on the same line when they fit', () => {
    // "hi" + " " + "bye" = 60px ≤ 100px
    expect(wrapText('hi bye', mono, W)).toEqual(['hi bye']);
  });

  it('strips soft hyphens when the word fits whole', () => {
    // "inter\u00ADnet" = "internet" = 80px ≤ 100px
    expect(wrapText('inter\u00ADnet', mono, W)).toEqual(['internet']);
  });

  it('breaks at a soft hyphen when the word overflows', () => {
    // "inter\u00ADnational" → "international" = 130px > 100px
    // "inter-" = 60px fits; "national" = 80px on next line
    const result = wrapText('inter\u00ADnational', mono, W);
    expect(result).toEqual(['inter-', 'national']);
  });

  it('picks the longest syllable prefix that fits', () => {
    // "in\u00ADter\u00ADna\u00ADtion" = "internation" = 110px > 100px
    // "in-" = 30px, "inter-" = 60px, "interna-" = 80px, "internation" = 110px overflows
    // So the best split with '-' on line 1 is "interna-" (80px ≤ 100px)
    const result = wrapText('in\u00ADter\u00ADna\u00ADtion', mono, W);
    expect(result[0]).toBe('interna-');
    expect(result[1]).toBe('tion');
  });

  it('forces character break for a word wider than the container', () => {
    // "abcdefghijklmno" = 15 chars × 10px = 150px > 100px, no soft hyphens
    // Splits at char 10: "abcdefghij" | "klmno"
    const result = wrapText('abcdefghijklmno', mono, W);
    expect(result).toHaveLength(2);
    expect(result[0]).toBe('abcdefghij');
    expect(result[1]).toBe('klmno');
  });

  it('wraps multiple words across multiple lines', () => {
    // 4-char words: "aaaa bbbb" = 9 chars × 10px = 90px ≤ 100px (pair fits)
    // "aaaa bbbb cccc" = 14 chars × 10px = 140px > 100px (three don't fit)
    const result = wrapText('aaaa bbbb cccc dddd', mono, W);
    expect(result).toEqual(['aaaa bbbb', 'cccc dddd']);
  });

  it('hyphenates mid-sentence when a word overflows', () => {
    // "ok in\u00ADter\u00ADnat\u00ADion" clean word "internation" = 110px
    // "ok internation" = 140px > 100px → trigger syllable break
    // "ok inter-" = 90px ≤ 100px; "nation" on next line
    const result = wrapText('ok in\u00ADter\u00ADnat\u00ADion', mono, W);
    expect(result[0]).toBe('ok inter-');
    expect(result[1]).toBe('nation');
  });

  it('handles multiple consecutive soft-hyphenated words', () => {
    const result = wrapText('ab\u00ADcd ef\u00ADgh', mono, W);
    // "abcd" = 40px, "efgh" = 40px → "abcd efgh" = 90px ≤ 100px
    expect(result).toEqual(['abcd efgh']);
  });

  it('removes soft hyphens from output when no break is taken', () => {
    const result = wrapText('a\u00ADb\u00ADc', mono, W);
    expect(result.join('')).not.toContain('\u00AD');
    expect(result).toEqual(['abc']);
  });
});

describe('paginateLines', () => {
  it('returns one empty page for no lines', () => {
    expect(paginateLines([], 2)).toEqual([[]]);
  });

  it('fits all lines on one page when count ≤ maxLines', () => {
    expect(paginateLines(['a', 'b'], 2)).toEqual([['a', 'b']]);
  });

  it('splits into multiple pages', () => {
    expect(paginateLines(['a', 'b', 'c', 'd'], 2)).toEqual([
      ['a', 'b'],
      ['c', 'd'],
    ]);
  });

  it('handles a partial last page', () => {
    expect(paginateLines(['a', 'b', 'c'], 2)).toEqual([['a', 'b'], ['c']]);
  });
});

describe('allocateTimings', () => {
  it('gives equal time to equal-length pages', () => {
    const timings = allocateTimings([['abc'], ['def']], 4);
    expect(timings[0]).toBeCloseTo(2);
    expect(timings[1]).toBeCloseTo(2);
  });

  it('allocates proportionally by non-whitespace character count', () => {
    // Page 1: "abc" → 3 chars; Page 2: "de" → 2 chars; total = 5
    const timings = allocateTimings([['abc'], ['de']], 5);
    expect(timings[0]).toBeCloseTo(3);
    expect(timings[1]).toBeCloseTo(2);
  });

  it('ignores whitespace in character count', () => {
    // "a b" → 2 non-WS chars; "cd" → 2 → equal split
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
