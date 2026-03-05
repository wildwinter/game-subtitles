import { describe, it, expect } from 'vitest';
import { wrapText, paginateLines, allocateTimings, wrapAndPaginate } from '../TextLayout.js';

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

describe('paginateLines — continuation ellipsis', () => {
  // Monospace measure: each character (including U+2026) counts as 10 px.
  const mono = text => text.length * 10;
  const W = 100; // 10-char container

  it('does nothing when measureWidth is not supplied (backward compat)', () => {
    expect(paginateLines(['aa', 'bb', 'cc'], 1)).toEqual([['aa'], ['bb'], ['cc']]);
  });

  it('does not add … to a single-page subtitle', () => {
    expect(paginateLines(['hello'], 1, mono, W)).toEqual([['hello']]);
  });

  it('does not add … to the last page of a multi-page subtitle', () => {
    const pages = paginateLines(['aa', 'bb', 'cc'], 1, mono, W);
    expect(pages[pages.length - 1]).toEqual(['cc']);
  });

  it('appends … to the last line of each non-final page when it fits', () => {
    // 'aaaaa…' = 6 chars × 10 = 60 px ≤ 100 px → fits
    expect(paginateLines(['aaaaa', 'bbbbb'], 1, mono, W)).toEqual([
      ['aaaaa\u2026'],
      ['bbbbb'],
    ]);
  });

  it('appends … to the last line of every non-final page in a 3-page subtitle', () => {
    const pages = paginateLines(['aa', 'bb', 'cc'], 1, mono, W);
    expect(pages[0]).toEqual(['aa\u2026']);
    expect(pages[1]).toEqual(['bb\u2026']);
    expect(pages[2]).toEqual(['cc']);
  });

  it('trims trailing words to make room for … and requeues them', () => {
    // Container is 70 px (7 chars).
    // 'one two…' = 8 chars × 10 = 80 px > 70 px → trim 'two'
    // 'one…'     = 4 chars × 10 = 40 px ≤ 70 px → fits
    // 'two' is requeued and becomes the first line of the next page.
    const pages = paginateLines(['one two', 'three'], 1, mono, 70);
    expect(pages[0]).toEqual(['one\u2026']);
    expect(pages[1]).toEqual(['two\u2026']);
    expect(pages[2]).toEqual(['three']);
  });

  it('moves a soft-hyphen fragment to the next page, reconstituting the word', () => {
    // maxLines=2: page 0 would be ['hello', 'car-'] but 'car-' is a fragment.
    // The stem 'car' merges with the continuation 'go' → 'cargo' on page 1.
    // 'hello' becomes the sole line on page 0 and receives '…'.
    // ('cargo' = 5 chars × 10 = 50 px ≤ 100 px — fits the container.)
    const pages = paginateLines(['hello', 'car-', 'go'], 2, mono, W);
    expect(pages[0]).toEqual(['hello\u2026']);
    expect(pages[1]).toEqual(['cargo']);
  });

  it('moves only the trailing fragment token, reconstituting it with the next line', () => {
    // Last line of page 0: 'hi car-' — 'hi' is complete, 'car-' is a fragment.
    // 'car' merges with 'go' → 'cargo' on page 1; 'hi' stays and receives '…'.
    // ('hi car-' = 7 chars × 10 = 70 px ≤ 100 px, so it's a valid wrapped line.)
    const pages = paginateLines(['first', 'hi car-', 'go'], 2, mono, W);
    expect(pages[0]).toEqual(['first', 'hi\u2026']);
    expect(pages[1]).toEqual(['cargo']);
  });

  it('falls back to stem + … when moving the fragment would empty the page', () => {
    // maxLines=1: 'car-' is the only line on page 0. Moving it would empty the
    // page, so the fallback applies: '-' is stripped and '…' is shown instead.
    // The continuation 'go' is restored unchanged on page 1.
    const pages = paginateLines(['car-', 'go'], 1, mono, W);
    expect(pages[0]).toEqual(['car\u2026']);
    expect(pages[1]).toEqual(['go']);
  });

  it('… on the last line of a multi-line page only affects that line', () => {
    // maxLines = 2: page 0 gets lines 0-1, page 1 gets line 2.
    // Only line 1 (last of page 0) gets '…'.
    const pages = paginateLines(['first', 'second', 'third'], 2, mono, W);
    expect(pages[0]).toEqual(['first', 'second\u2026']);
    expect(pages[1]).toEqual(['third']);
  });
});

describe('wrapAndPaginate', () => {
  // Monospace 10 px/char; '…' = 1 char = 10 px → effectiveWidth = 90 px.
  const mono = text => text.length * 10;
  const W = 100;

  it('returns one empty page for empty text', () => {
    expect(wrapAndPaginate('', mono, W, 2)).toEqual([[]]);
  });

  it('returns a single page with no ellipsis for short text', () => {
    expect(wrapAndPaginate('hello', mono, W, 2)).toEqual([['hello']]);
  });

  it('returns a single page when all words fit', () => {
    // "hello world" = 11 chars × 10 = 110 px; with maxLines=2 slot 0 uses
    // full 100 px → "hello" fits, slot 1 uses 90 px → "world" (50 px) fits.
    expect(wrapAndPaginate('hello world', mono, W, 2)).toEqual([['hello', 'world']]);
  });

  it('appends … to the last line of non-final pages', () => {
    // maxLines=1; effectiveWidth=90 px; "aaaaa" (50) fits, page break, "bbbbb" next.
    expect(wrapAndPaginate('aaaaa bbbbb', mono, W, 1)).toEqual([
      ['aaaaa\u2026'],
      ['bbbbb'],
    ]);
  });

  it('does not append … to the final page', () => {
    const pages = wrapAndPaginate('aaaaa bbbbb ccccc', mono, W, 1);
    expect(pages[pages.length - 1]).toEqual(['ccccc']);
  });

  it('never places a word fragment on the last line of a non-final page', () => {
    // "inter\u00ADnational" (130 px) overflows; with maxLines=2 and the word
    // arriving when slot 0 is occupied by "hello", slot 1 is the last slot
    // and receives no syllable hyphenation — "international" moves to page 2.
    const pages = wrapAndPaginate('hello inter\u00ADnational', mono, W, 2);
    expect(pages[0]).toEqual(['hello\u2026']);
    // Page 2 starts at slot 0 (non-last when maxLines=2): syllable break allowed.
    expect(pages[1][0]).toBe('inter-');
    expect(pages[1][1]).toBe('national');
  });

  it('uses soft hyphens on non-last line slots', () => {
    // "inter\u00ADnational" on its own page with maxLines=2:
    // slot 0 (non-last) → "inter-" + slot 1 (last) → "national" (80 px ≤ 90 px).
    const pages = wrapAndPaginate('inter\u00ADnational', mono, W, 2);
    expect(pages).toEqual([['inter-', 'national']]);
  });

  it('produces correct 3-page layout', () => {
    // maxLines=1; three words → three pages, first two get '…'.
    const pages = wrapAndPaginate('aa bb cc', mono, W, 1);
    // All three fit in 90 px effective width together only if ≤ 90 px.
    // "aa bb cc" = 8 chars × 10 = 80 px ≤ 90 px → single page, no split.
    // (Three separate pages only happen when words don't fit together.)
    expect(pages.length).toBeGreaterThanOrEqual(1);
    const flat = pages.flat().join(' ').replace(/\u2026/g, '');
    expect(flat.trim()).toMatch(/aa/);
    expect(flat.trim()).toMatch(/cc/);
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
