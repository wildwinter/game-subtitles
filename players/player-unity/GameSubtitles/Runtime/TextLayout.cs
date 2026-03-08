using System;
using System.Collections.Generic;

namespace GameSubtitles
{
    /// <summary>
    /// Static text-layout utilities. Direct C# port of TextLayout.js / TextLayout.cpp.
    /// </summary>
    public static class TextLayout
    {
        private const char SoftHyphen = '\u00AD'; // U+00AD SOFT HYPHEN
        private const char Ellipsis   = '\u2026'; // U+2026 HORIZONTAL ELLIPSIS

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Wraps <paramref name="text"/> into pages of at most <paramref name="maxLines"/> lines,
        /// breaking at soft hyphens (U+00AD) where necessary.
        /// </summary>
        /// <param name="text">Input text; may contain U+00AD soft hyphens.</param>
        /// <param name="measureWidth">Callback that returns the rendered pixel-width of a string.</param>
        /// <param name="containerWidth">Maximum line width in the same units as <paramref name="measureWidth"/>.</param>
        /// <param name="maxLines">Lines per page (&gt;= 1).</param>
        /// <returns>List of pages; each page is a list of line strings.</returns>
        public static List<List<string>> WrapAndPaginate(
            string text,
            Func<string, float> measureWidth,
            float containerWidth,
            int maxLines)
        {
            float ellipsisWidth = measureWidth(Ellipsis.ToString());

            // Split on whitespace, removing empty tokens
            string[] rawWords = text.Split(new[] { ' ', '\t', '\n', '\r' },
                                           StringSplitOptions.RemoveEmptyEntries);
            if (rawWords.Length == 0)
                return new List<List<string>> { new List<string>() };

            // Mutable copy — the algorithm replaces words with soft-hyphen remainders in-place
            string[] words = (string[])rawWords.Clone();

            var pages     = new List<List<string>>();
            var pageLines = new List<string>();
            string lineText = "";
            int lineSlot = 0; // 0-indexed line position within the current page

            // Advance the current line, starting a new page when the last slot is filled
            void AdvanceLine()
            {
                pageLines.Add(lineText);
                lineText = "";
                if (lineSlot == maxLines - 1)
                {
                    pages.Add(new List<string>(pageLines));
                    pageLines.Clear();
                    lineSlot = 0;
                }
                else
                {
                    lineSlot++;
                }
            }

            int wi = 0;
            while (wi < words.Length)
            {
                bool  isLastSlot    = (lineSlot == maxLines - 1);
                float effectiveWidth = isLastSlot ? (containerWidth - ellipsisWidth) : containerWidth;

                // Split current word on soft hyphens to get syllables
                string[] syllables   = words[wi].Split(SoftHyphen);
                string   clean       = string.Join("", syllables);
                bool     hasSyllables = syllables.Length > 1;
                string   sep         = lineText.Length == 0 ? "" : " ";

                // 1. Full word fits within the effective width
                if (measureWidth(lineText + sep + clean) <= effectiveWidth)
                {
                    lineText += sep + clean;
                    wi++;
                    continue;
                }

                // 2. Syllable-prefix hyphenation — only on non-last slots with content
                if (!isLastSlot && hasSyllables && lineText.Length > 0)
                {
                    int breakAt = FindSyllableBreak(syllables, lineText, sep, measureWidth, effectiveWidth);
                    if (breakAt >= 0)
                    {
                        string prefix = string.Join("", syllables, 0, breakAt + 1);
                        var remainderArr = new string[syllables.Length - breakAt - 1];
                        Array.Copy(syllables, breakAt + 1, remainderArr, 0, remainderArr.Length);
                        string remainder = string.Join(SoftHyphen.ToString(), remainderArr);

                        lineText  += sep + prefix + "-";
                        words[wi]  = remainder;
                        AdvanceLine();
                        continue;
                    }
                }

                // 3. Flush the current line (if non-empty) and retry the word
                if (lineText.Length > 0)
                {
                    AdvanceLine();
                    continue;
                }

                // 4. Line is empty on a last slot with prior lines: close the page so the word
                //    retries at slot 0 of a fresh page where syllable-breaking is allowed
                if (isLastSlot && pageLines.Count > 0)
                {
                    pages.Add(new List<string>(pageLines));
                    pageLines.Clear();
                    lineSlot = 0;
                    continue;
                }

                // 5. Line is empty, non-last slot: try syllable breaking from the start of the line
                if (!isLastSlot && hasSyllables)
                {
                    int breakAt = FindSyllableBreak(syllables, "", "", measureWidth, effectiveWidth);
                    if (breakAt >= 0)
                    {
                        string prefix = string.Join("", syllables, 0, breakAt + 1);
                        var remainderArr = new string[syllables.Length - breakAt - 1];
                        Array.Copy(syllables, breakAt + 1, remainderArr, 0, remainderArr.Length);
                        string remainder = string.Join(SoftHyphen.ToString(), remainderArr);

                        lineText  = prefix + "-";
                        words[wi] = remainder;
                        AdvanceLine();
                        continue;
                    }
                }

                // 6. Character-level break as a last resort
                //    Use effectiveWidth on last slots so the subsequently appended ellipsis always fits
                string[] broken = ForceBreak(clean, measureWidth,
                                             isLastSlot ? effectiveWidth : containerWidth);
                for (int bi = 0; bi < broken.Length - 1; bi++)
                {
                    lineText = broken[bi];
                    AdvanceLine();
                }
                lineText = broken[broken.Length - 1];
                wi++;
            }

            // Flush any remaining content
            if (lineText.Length > 0)
                pageLines.Add(lineText);
            if (pageLines.Count > 0)
                pages.Add(new List<string>(pageLines));
            if (pages.Count == 0)
                return new List<List<string>> { new List<string>() };

            // Append ellipsis to the last line of every non-final page.
            // Those lines were built with effectiveWidth, so the ellipsis always fits.
            for (int pi = 0; pi < pages.Count - 1; pi++)
            {
                var pg = pages[pi];
                pg[pg.Count - 1] += Ellipsis;
            }

            // Last-line word reconstitution: if the very last line of the last page is a
            // single token (the tail of a soft-hyphen break) and the preceding line ends with
            // the matching hyphenated stem, rejoin the whole word when it fits in containerWidth.
            var lastPage = pages[pages.Count - 1];
            if (lastPage.Count >= 2)
            {
                string lastLine = lastPage[lastPage.Count - 1];
                if (!lastLine.Contains(" "))
                {
                    string[] prevTokens = lastPage[lastPage.Count - 2].Split(' ');
                    if (prevTokens.Length > 0 &&
                        prevTokens[prevTokens.Length - 1].EndsWith("-"))
                    {
                        string stem     = prevTokens[prevTokens.Length - 1];
                        string rejoined = stem.Substring(0, stem.Length - 1) + lastLine;
                        if (measureWidth(rejoined) <= containerWidth)
                        {
                            int lastIdx = lastPage.Count - 1;
                            int prevIdx = lastPage.Count - 2;

                            lastPage[lastIdx] = rejoined;
                            if (prevTokens.Length > 1)
                            {
                                var remaining = new string[prevTokens.Length - 1];
                                Array.Copy(prevTokens, remaining, remaining.Length);
                                lastPage[prevIdx] = string.Join(" ", remaining);
                            }
                            else
                            {
                                lastPage.RemoveAt(prevIdx);
                            }
                        }
                    }
                }
            }

            return pages;
        }

        /// <summary>
        /// Allocates display time proportionally to character count per page.
        /// </summary>
        public static List<float> AllocateTimings(List<List<string>> pages, float totalDuration)
        {
            var counts = new List<int>(pages.Count);
            foreach (var page in pages)
            {
                string pageText = string.Join("", page);
                int count = 0;
                foreach (char ch in pageText)
                {
                    if (!char.IsWhiteSpace(ch) && ch != Ellipsis)
                        count++;
                }
                counts.Add(Math.Max(1, count)); // guard against empty pages
            }

            int total = 0;
            foreach (int c in counts) total += c;

            var timings = new List<float>(counts.Count);
            foreach (int c in counts)
                timings.Add((float)c / total * totalDuration);

            return timings;
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static string[] ForceBreak(
            string word,
            Func<string, float> measureWidth,
            float maxWidth)
        {
            var lines = new List<string>();
            string current = "";

            foreach (char ch in word)
            {
                string next = current + ch;
                if (measureWidth(next) <= maxWidth)
                {
                    current = next;
                }
                else
                {
                    if (current.Length > 0)
                        lines.Add(current);
                    current = ch.ToString();
                }
            }
            if (current.Length > 0)
                lines.Add(current);

            return lines.Count > 0 ? lines.ToArray() : new[] { word };
        }

        private static int FindSyllableBreak(
            string[] syllables,
            string lineText,
            string sep,
            Func<string, float> measureWidth,
            float maxWidth)
        {
            string acc  = "";
            int    last = -1;

            // Test all syllable prefixes except the final one
            for (int k = 0; k < syllables.Length - 1; k++)
            {
                acc += syllables[k];
                if (measureWidth(lineText + sep + acc + "-") <= maxWidth)
                    last = k;
                else
                    break; // prefixes only grow, so no later prefix can fit
            }

            return last;
        }
    }
}
