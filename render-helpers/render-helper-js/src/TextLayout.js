const SOFT_HYPHEN = '\u00AD';
const ELLIPSIS = '\u2026';

/**
 * Force-breaks a single word (no soft hyphens) into fragments that each fit
 * within `maxWidth`, returning at least one element.
 *
 * @param {string} word
 * @param {(t: string) => number} measureWidth
 * @param {number} maxWidth
 * @returns {string[]}
 */
function forceBreak(word, measureWidth, maxWidth) {
  const lines = [];
  let current = '';
  for (const ch of word) {
    const next = current + ch;
    if (measureWidth(next) <= maxWidth) {
      current = next;
    } else {
      if (current) lines.push(current);
      current = ch;
    }
  }
  if (current) lines.push(current);
  return lines.length > 0 ? lines : [word];
}

/**
 * Returns the highest syllable index k such that
 * `lineText + sep + syllables[0..k].join('') + '-'` fits within `maxWidth`.
 * Returns -1 if even the first syllable prefix doesn't fit.
 *
 * Stops early once adding another syllable overflows, since prefixes only grow.
 */
function findSyllableBreak(syllables, lineText, sep, measureWidth, maxWidth) {
  let acc = '';
  let last = -1;
  for (let k = 0; k < syllables.length - 1; k++) {
    acc += syllables[k];
    if (measureWidth(lineText + sep + acc + '-') <= maxWidth) {
      last = k;
    } else {
      break;
    }
  }
  return last;
}

/**
 * Wraps `text` and paginates it in a single pass.
 *
 * The last line of every non-final page is built with an effective width of
 * `containerWidth − measureWidth('…')`, so that appending '…' afterwards
 * never overflows — no backtracking is required.
 *
 * Rules:
 *   • Soft-hyphen (U+00AD) syllable breaks are used on all but the last line
 *     of each page.  The last line of each page only receives complete words,
 *     so no word is ever split across a page boundary.
 *   • If a word cannot start on a last-line slot (because it would overflow
 *     and syllable-breaking is not permitted there), the current page is
 *     closed and the word retries at slot 0 of a fresh page.
 *   • '…' (U+2026) is appended to the last line of every non-final page.
 *
 * @param {string} text           Input, may contain U+00AD soft hyphens.
 * @param {(t: string) => number} measureWidth  Returns pixel width of a string.
 * @param {number} containerWidth               Max pixel width per line.
 * @param {number} maxLines                     Lines per page (integer ≥ 1).
 * @returns {string[][]}  Pages, each an array of line strings.
 */
export function wrapAndPaginate(text, measureWidth, containerWidth, maxLines) {
  const ellipsisWidth = measureWidth(ELLIPSIS);

  const rawWords = text.split(/\s+/).filter(w => w.length > 0);
  if (rawWords.length === 0) return [[]];

  const pages = [];
  let pageLines = [];
  let lineText = '';
  let lineSlot = 0; // 0-indexed line position within the current page

  function advanceLine() {
    pageLines.push(lineText);
    lineText = '';
    if (lineSlot === maxLines - 1) {
      pages.push(pageLines);
      pageLines = [];
      lineSlot = 0;
    } else {
      lineSlot++;
    }
  }

  const words = [...rawWords];
  let wi = 0;

  while (wi < words.length) {
    const isLastSlot = lineSlot === maxLines - 1;
    // Reserve ellipsis space on last-line slots so '…' always fits later.
    const effectiveWidth = isLastSlot ? containerWidth - ellipsisWidth : containerWidth;
    const syllables = words[wi].split(SOFT_HYPHEN);
    const clean = syllables.join('');
    const hasSyllables = syllables.length > 1;
    const sep = lineText ? ' ' : '';

    // 1. Full word fits within the effective width.
    if (measureWidth(lineText + sep + clean) <= effectiveWidth) {
      lineText += sep + clean;
      wi++;
      continue;
    }

    // 2. Syllable-prefix hyphenation — only on non-last slots with content.
    if (!isLastSlot && hasSyllables && lineText) {
      const breakAt = findSyllableBreak(syllables, lineText, sep, measureWidth, effectiveWidth);
      if (breakAt >= 0) {
        lineText += sep + syllables.slice(0, breakAt + 1).join('') + '-';
        words[wi] = syllables.slice(breakAt + 1).join(SOFT_HYPHEN);
        advanceLine();
        continue;
      }
    }

    // 3. Flush the current line (if non-empty) and retry the word.
    if (lineText) {
      advanceLine();
      continue;
    }

    // 4. Line is empty on a last slot with prior lines: close the page so the
    //    word retries at slot 0 of a fresh page, where syllable-breaking is
    //    allowed and the word can be laid out properly.
    if (isLastSlot && pageLines.length > 0) {
      pages.push(pageLines);
      pageLines = [];
      lineSlot = 0;
      continue;
    }

    // 5. Line is empty, non-last slot: try syllable breaking from the start.
    if (!isLastSlot && hasSyllables) {
      const breakAt = findSyllableBreak(syllables, '', '', measureWidth, effectiveWidth);
      if (breakAt >= 0) {
        lineText = syllables.slice(0, breakAt + 1).join('') + '-';
        words[wi] = syllables.slice(breakAt + 1).join(SOFT_HYPHEN);
        advanceLine();
        continue;
      }
    }

    // 6. Character-level break as a last resort.  Use effectiveWidth on last
    //    slots so the subsequently appended '…' always fits.
    const broken = forceBreak(clean, measureWidth, isLastSlot ? effectiveWidth : containerWidth);
    for (let bi = 0; bi < broken.length - 1; bi++) {
      lineText = broken[bi];
      advanceLine();
    }
    lineText = broken[broken.length - 1];
    wi++;
  }

  if (lineText) pageLines.push(lineText);
  if (pageLines.length > 0) pages.push(pageLines);
  if (pages.length === 0) return [[]];

  // Append '…' to the last line of every non-final page.
  // Those lines were built with effectiveWidth, so the ellipsis always fits.
  for (let pi = 0; pi < pages.length - 1; pi++) {
    pages[pi][pages[pi].length - 1] += ELLIPSIS;
  }

  // Last-line word reconstitution: if the very last line of the last page is a
  // single token (the tail of a soft-hyphen break) and the preceding line ends
  // with the matching hyphenated stem, reconstitute the whole word when it fits
  // within containerWidth.  The last line was built with effectiveWidth, so a
  // word up to containerWidth wide may now fit there without any ellipsis.
  const lp = pages[pages.length - 1];
  if (lp.length >= 2) {
    const lastLine = lp[lp.length - 1];
    if (!lastLine.includes(' ')) {
      const prevTokens = lp[lp.length - 2].split(' ');
      const prevLastToken = prevTokens[prevTokens.length - 1];
      if (prevLastToken.endsWith('-')) {
        const rejoined = prevLastToken.slice(0, -1) + lastLine;
        if (measureWidth(rejoined) <= containerWidth) {
          prevTokens.pop();
          lp[lp.length - 1] = rejoined;
          if (prevTokens.length > 0) {
            lp[lp.length - 2] = prevTokens.join(' ');
          } else {
            lp.splice(lp.length - 2, 1);
          }
        }
      }
    }
  }

  return pages;
}

/**
 * Allocates display durations to pages proportionally by non-whitespace
 * character count.  U+2026 '…' is excluded as it is never vocalized.
 *
 * @param {string[][]} pages
 * @param {number} totalDuration  Seconds.
 * @returns {number[]}  Duration in seconds per page (same length as `pages`).
 */
export function allocateTimings(pages, totalDuration) {
  const counts = pages.map(page =>
    page.join('').replace(/[\s\u2026]/g, '').length || 1,
  );
  const total = counts.reduce((a, b) => a + b, 0);
  return counts.map(c => (c / total) * totalDuration);
}
