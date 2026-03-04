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
 * @param {string[]} lines
 * @param {number} maxLines
 * @returns {string[][]}
 */
export function paginateLines(lines, maxLines) {
  if (lines.length === 0) return [[]];
  const pages = [];
  for (let i = 0; i < lines.length; i += maxLines) {
    pages.push(lines.slice(i, i + maxLines));
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
    page.join('').replace(/\s/g, '').length || 1,
  );
  const total = counts.reduce((a, b) => a + b, 0);
  return counts.map(c => (c / total) * totalDuration);
}
