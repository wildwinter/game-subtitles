const SOFT_HYPHEN = '\u00AD';

/**
 * Force-breaks a single word (no soft hyphens) to fit within containerWidth,
 * returning an array of line fragments (character-level wrapping).
 *
 * @param {string} word
 * @param {(t: string) => number} measureWidth
 * @param {number} containerWidth
 * @returns {string[]} At least one element.
 */
function forceBreak(word, measureWidth, containerWidth) {
  const lines = [];
  let current = '';
  for (const ch of word) {
    const next = current + ch;
    if (measureWidth(next) <= containerWidth) {
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
 * Wraps `text` to fit within `containerWidth`, using U+00AD soft hyphens as
 * preferred break points within words.
 *
 * Algorithm:
 *   1. Split on whitespace → words.
 *   2. Split each word on U+00AD → syllables.
 *   3. Greedy fill: try the full clean word first; on overflow, try the longest
 *      syllable prefix that fits with a visible '-'; if still no fit, push current
 *      line and retry on a fresh line; a word wider than the container is
 *      character-broken.
 *
 * @param {string} text         Input, may contain U+00AD.
 * @param {(t: string) => number} measureWidth  Returns pixel width of a string.
 * @param {number} containerWidth               Max pixel width per line.
 * @returns {string[]} Lines with soft hyphens removed; visible '-' where breaks
 *                     were taken.
 */
export function wrapText(text, measureWidth, containerWidth) {
  const words = text.split(/\s+/).filter(w => w.length > 0);
  if (words.length === 0) return [];

  const lines = [];
  let lineText = '';

  for (let wi = 0; wi < words.length; wi++) {
    const syllables = words[wi].split(SOFT_HYPHEN);
    const clean = syllables.join('');
    const hasSyllables = syllables.length > 1;

    // Try the full word on the current line.
    const sep = lineText ? ' ' : '';
    if (measureWidth(lineText + sep + clean) <= containerWidth) {
      lineText += sep + clean;
      continue;
    }

    // Doesn't fit. Try syllable-prefix hyphenation into the current line.
    if (hasSyllables) {
      const breakAt = findSyllableBreak(syllables, lineText, sep, measureWidth, containerWidth);
      if (breakAt >= 0) {
        const prefix = syllables.slice(0, breakAt + 1).join('') + '-';
        lines.push(lineText + sep + prefix);
        lineText = '';
        // Reprocess remaining syllables as if they form a new word fragment.
        const rest = syllables.slice(breakAt + 1).join(SOFT_HYPHEN);
        words.splice(wi + 1, 0, rest); // insert remainder back into word queue
        continue;
      }
    }

    // No syllable prefix fit (or word has no soft hyphens). Push current line
    // (if non-empty) and retry the word on a fresh line.
    if (lineText) {
      lines.push(lineText);
      lineText = '';
    }

    // Fresh line: try the whole word.
    if (measureWidth(clean) <= containerWidth) {
      lineText = clean;
      continue;
    }

    // Word still doesn't fit. Try syllable breaking on the fresh line.
    if (hasSyllables) {
      const breakAt = findSyllableBreak(syllables, '', '', measureWidth, containerWidth);
      if (breakAt >= 0) {
        const prefix = syllables.slice(0, breakAt + 1).join('') + '-';
        lines.push(prefix);
        const rest = syllables.slice(breakAt + 1).join(SOFT_HYPHEN);
        words.splice(wi + 1, 0, rest);
        continue;
      }
    }

    // Fall back to character-level breaking.
    const broken = forceBreak(clean, measureWidth, containerWidth);
    lines.push(...broken.slice(0, -1));
    lineText = broken[broken.length - 1];
  }

  if (lineText) lines.push(lineText);
  return lines;
}

/**
 * Finds the highest syllable index such that
 * `lineText + sep + syllables[0..k].join('') + '-'` fits within containerWidth.
 * Returns -1 if even the first syllable prefix doesn't fit.
 *
 * Stops early once adding another syllable causes overflow, since syllable
 * prefixes only grow longer as k increases.
 */
function findSyllableBreak(syllables, lineText, sep, measureWidth, containerWidth) {
  let acc = '';
  let last = -1;
  for (let k = 0; k < syllables.length - 1; k++) {
    acc += syllables[k];
    if (measureWidth(lineText + sep + acc + '-') <= containerWidth) {
      last = k;
    } else {
      break; // each additional syllable only makes the prefix longer
    }
  }
  return last;
}

/**
 * Splits an array of line strings into pages of at most `maxLines` lines each.
 *
 * When `measureWidth` and `containerWidth` are supplied, a continuation ellipsis
 * (U+2026 "…") is appended to the last line of every non-final page to signal
 * that more text follows.  The ellipsis is treated as real content:
 *
 *   • No word is split across a page boundary.  If the last token on the last
 *     line of a page is a soft-hyphen fragment (ends with "-"), that token is
 *     moved to the front of the next page's content so the whole word appears
 *     together.  This is repeated for any further trailing fragments on the
 *     same line.
 *   • If moving a fragment would empty the page entirely (the fragment was the
 *     only content), it stays and its trailing "-" is replaced by "…" as a
 *     fallback.
 *   • If the last line (after fragment removal) still does not fit with "…"
 *     appended, whole words are trimmed from its right end and re-queued at the
 *     front of the remaining lines until the shortened line plus "…" fits.
 *
 * @param {string[]} lines
 * @param {number} maxLines
 * @param {((t: string) => number) | null} [measureWidth]
 * @param {number} [containerWidth]
 * @returns {string[][]}
 */
export function paginateLines(lines, maxLines, measureWidth = null, containerWidth = 0) {
  if (lines.length === 0) return [[]];

  const withEllipsis = typeof measureWidth === 'function' && containerWidth > 0;
  const ELLIPSIS = '\u2026';

  const pages = [];
  const remaining = [...lines];

  while (remaining.length > 0) {
    const page = remaining.splice(0, maxLines);
    const isLastPage = remaining.length === 0;

    if (withEllipsis && !isLastPage) {
      let applyEllipsis = true;

      // Snapshot remaining[0] so we can roll back all merges if the fallback fires.
      const origR0 = remaining.length > 0 ? remaining[0] : null;

      // Move any trailing hyphenation fragment (a token ending with '-') to the
      // next page so that no word is split across a page boundary.  The fragment's
      // stem is merged onto the front of remaining[0] to reconstitute the word
      // (e.g. "carto-" merges with "graphy…" → "cartography…").
      fragLoop: while (true) {
        const li = page.length - 1;
        const tokens = page[li].split(' ');
        const lastToken = tokens[tokens.length - 1];
        if (!lastToken.endsWith('-')) break; // last token is a complete word

        const stem = lastToken.slice(0, -1); // strip the trailing '-'
        tokens.pop();

        // Prepend the stem to the next content, reconstituting the broken word.
        if (remaining.length > 0) {
          remaining[0] = stem + remaining[0];
        }

        if (tokens.length > 0) {
          page[li] = tokens.join(' ');
          // The new last token may itself be a fragment — continue the loop.
        } else {
          // The entire last line was the fragment; remove the now-empty line.
          page.pop();
          if (page.length === 0) {
            // Moving the fragment would empty the page — not allowed.
            // Undo all merges and fall back to showing stem + '…'.
            if (origR0 !== null) remaining[0] = origR0;
            page.push(stem + ELLIPSIS);
            applyEllipsis = false;
            break fragLoop;
          }
          // Continue checking the new last line of the (now shorter) page.
        }
      }

      if (applyEllipsis) {
        // The last line now ends with a complete word — append '…'.
        const i = page.length - 1;
        const base = page[i];
        if (measureWidth(base + ELLIPSIS) <= containerWidth) {
          page[i] = base + ELLIPSIS;
        } else {
          // Trim whole words from the right until the prefix fits with '…'.
          const words = base.split(' ');
          const overflow = [];
          while (words.length > 0) {
            if (measureWidth(words.join(' ') + ELLIPSIS) <= containerWidth) break;
            overflow.unshift(words.pop());
          }
          if (words.length > 0) {
            page[i] = words.join(' ') + ELLIPSIS;
            if (overflow.length > 0) remaining.unshift(overflow.join(' '));
          } else {
            // Even a single token does not fit — append anyway rather than lose text.
            page[i] = base + ELLIPSIS;
          }
        }
      }
    }

    pages.push(page);
  }

  return pages;
}

/**
 * Wraps `text` and paginates it in a single pass, so that the last line of
 * each non-final page is computed with an effective width of
 * `containerWidth − measureWidth('…')`.  This guarantees that appending '…'
 * to those lines never causes overflow — no backtracking is needed.
 *
 * Rules:
 *   • Soft-hyphen (U+00AD) syllable breaks are used on all but the last line
 *     of each page.  The last line only receives complete words, so no word
 *     is ever split across a page boundary.
 *   • '…' (U+2026) is appended to the last line of every non-final page.
 *
 * @param {string} text
 * @param {(t: string) => number} measureWidth
 * @param {number} containerWidth
 * @param {number} maxLines
 * @returns {string[][]}  Pages, each an array of line strings.
 */
export function wrapAndPaginate(text, measureWidth, containerWidth, maxLines) {
  const ELLIPSIS = '\u2026';
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
        continue; // retry wi (now pointing to the remainder)
      }
    }

    // 3. Flush the current line (if non-empty) and retry the word on the next
    //    line or page.
    if (lineText) {
      advanceLine();
      continue;
    }

    // 4a. Empty last slot with prior lines on this page: finalize the page and
    //     retry the word at slot 0 of a fresh page.  This prevents a word from
    //     starting on the last line of a page (where no syllable hyphenation is
    //     allowed), which would produce either a character-broken fragment or an
    //     ugly lone word on the last slot.
    if (isLastSlot && pageLines.length > 0) {
      pages.push(pageLines);
      pageLines = [];
      lineSlot = 0;
      continue;
    }

    // 4b. Line is empty and the word still doesn't fit.
    //     Try syllable breaking from the start of the line (non-last slots only).
    if (!isLastSlot && hasSyllables) {
      const breakAt = findSyllableBreak(syllables, '', '', measureWidth, effectiveWidth);
      if (breakAt >= 0) {
        lineText = syllables.slice(0, breakAt + 1).join('') + '-';
        words[wi] = syllables.slice(breakAt + 1).join(SOFT_HYPHEN);
        advanceLine();
        continue;
      }
    }

    // 5. Character-level break as a last resort.
    //    Use effectiveWidth on last slots so the ellipsis always fits later.
    const breakWidth = isLastSlot ? effectiveWidth : containerWidth;
    const broken = forceBreak(clean, measureWidth, breakWidth);
    for (let bi = 0; bi < broken.length - 1; bi++) {
      lineText = broken[bi];
      advanceLine();
    }
    lineText = broken[broken.length - 1];
    wi++;
  }

  // Flush remaining content.
  if (lineText) pageLines.push(lineText);
  if (pageLines.length > 0) pages.push(pageLines);
  if (pages.length === 0) return [[]];

  // Append '…' to the last line of every non-final page.
  // Those lines were built with effectiveWidth, so the ellipsis always fits.
  for (let pi = 0; pi < pages.length - 1; pi++) {
    const li = pages[pi].length - 1;
    pages[pi][li] += ELLIPSIS;
  }

  return pages;
}

/**
 * Allocates display durations to pages proportionally by non-whitespace
 * character count.
 *
 * @param {string[][]} pages
 * @param {number} totalDuration  Seconds.
 * @returns {number[]}  Duration in seconds per page (same length as `pages`).
 */
export function allocateTimings(pages, totalDuration) {
  const counts = pages.map(page =>
    // U+2026 '…' is a visual continuation marker, never vocalized — exclude it.
    page.join('').replace(/[\s\u2026]/g, '').length || 1,
  );
  const total = counts.reduce((a, b) => a + b, 0);
  return counts.map(c => (c / total) * totalDuration);
}
