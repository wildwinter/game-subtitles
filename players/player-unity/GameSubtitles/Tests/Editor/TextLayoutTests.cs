using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace GameSubtitles.Tests
{
    // The same cases are in the JS (TextLayout.test.js), Unreal (GameSubtitleTextLayoutTest.cpp),
    // and Godot (tests/run_tests.gd) players. Keep all four in step.
    public class TextLayoutTests
    {
        private const string Shy      = "­";
        private const string Ellipsis = "…";
        private const float  W        = 100f; // 10 monospace characters; the ellipsis takes 1, so a last line gets 90

        private static float Mono(string text) => text.Length * 10f;

        private static List<List<string>> Wrap(string text, int maxLines, float firstLineIndent = 0f) =>
            TextLayout.WrapAndPaginate(text, Mono, W, maxLines, firstLineIndent);

        private static List<List<string>> Pages(params string[][] pages) =>
            pages.Select(p => p.ToList()).ToList();

        // ── WrapAndPaginate ──────────────────────────────────────────────────────

        [TestCase("")]
        [TestCase("   ")]
        public void EmptyOrWhitespaceTextGivesOneEmptyPage(string text)
        {
            Assert.AreEqual(Pages(new string[0]), Wrap(text, 2));
        }

        [Test]
        public void ShortWordFitsOnOnePage()
        {
            Assert.AreEqual(Pages(new[] { "hello" }), Wrap("hello", 2));
        }

        [Test]
        public void TwoWordsThatDoNotFitTogetherWrap()
        {
            Assert.AreEqual(Pages(new[] { "hello", "world!" }), Wrap("hello world!", 2));
        }

        [Test]
        public void TwoWordsThatFitStayTogether()
        {
            Assert.AreEqual(Pages(new[] { "hi bye" }), Wrap("hi bye", 2));
        }

        [Test]
        public void SplitsOnAnyWhitespace()
        {
            Assert.AreEqual(Pages(new[] { "hi bye" }), Wrap("hi\tbye\n", 2));
        }

        [Test]
        public void SoftHyphensAreStrippedWhenTheWordFitsWhole()
        {
            Assert.AreEqual(Pages(new[] { "internet" }), Wrap("inter" + Shy + "net", 2));
        }

        [Test]
        public void BreaksAtASoftHyphenWhenTheWordOverflows()
        {
            Assert.AreEqual(Pages(new[] { "inter-", "national" }), Wrap("inter" + Shy + "national", 2));
        }

        [Test]
        public void PicksTheLongestSyllablePrefixThatFits()
        {
            var pages = Wrap(string.Join(Shy, "in", "ter", "na", "tion"), 2);
            Assert.AreEqual("interna-", pages[0][0]);
            Assert.AreEqual("tion", pages[0][1]);
        }

        [Test]
        public void ForcesACharacterBreakForAnOverlongWord()
        {
            var pages = Wrap("abcdefghijklmno", 2);
            Assert.AreEqual("abcdefghij", pages[0][0]);
            Assert.AreEqual("klmno", pages[0][1]);
        }

        [Test]
        public void WrapsMultipleWordsAcrossLines()
        {
            Assert.AreEqual(Pages(new[] { "aaaa bbbb", "cccc dddd" }), Wrap("aaaa bbbb cccc dddd", 2));
        }

        [Test]
        public void AppendsAnEllipsisToNonFinalPages()
        {
            Assert.AreEqual(Pages(new[] { "aaaaa" + Ellipsis }, new[] { "bbbbb" }), Wrap("aaaaa bbbbb", 1));
            var pages = Wrap("aaaaa bbbbb ccccc", 1);
            Assert.AreEqual(new List<string> { "ccccc" }, pages[pages.Count - 1]);
        }

        [Test]
        public void AppendsAnEllipsisToAllNonFinalPagesOfThree()
        {
            var pages = Wrap("aaaaaa bbbbbb cccccc", 1);
            Assert.AreEqual(3, pages.Count);
            Assert.IsTrue(pages[0][0].EndsWith(Ellipsis));
            Assert.IsTrue(pages[1][0].EndsWith(Ellipsis));
            Assert.IsFalse(pages[2][0].EndsWith(Ellipsis));
        }

        [Test]
        public void NeverPutsAWordFragmentOnTheLastLineOfANonFinalPage()
        {
            var pages = Wrap("hello inter" + Shy + "national", 2);
            Assert.AreEqual(new List<string> { "hello" + Ellipsis }, pages[0]);
            Assert.AreEqual("inter-", pages[1][0]);
            Assert.AreEqual("national", pages[1][1]);
        }

        [Test]
        public void PreservesAllWordsAcrossPages()
        {
            var pages = Wrap("one two three four five six seven eight nine ten", 1);
            var words = string.Join(" ", pages.SelectMany(p => p)).Replace(Ellipsis, "")
                              .Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            CollectionAssert.Contains(words, "one");
            CollectionAssert.Contains(words, "ten");
        }

        [Test]
        public void NoLineContainsARawSoftHyphen()
        {
            string text = string.Join(Shy, "in", "ter", "na", "tion", "al", "i", "za", "tion") + " is com" + Shy + "plex";
            foreach (string line in Wrap(text, 2).SelectMany(p => p))
                StringAssert.DoesNotContain(Shy, line);
        }

        [Test]
        public void RejoinsATrailingFragmentWhenTheWholeWordFits()
        {
            // The 10-character word is split as "ccccc-" on line 0. Its tail is alone on the last
            // line, which has no ellipsis on the final page, so the whole word fits there.
            Assert.AreEqual(Pages(new[] { "aa", "cccccccccc" }), Wrap("aa ccccc" + Shy + "ccccc", 2));
        }

        [Test]
        public void FirstLineIndentNarrowsOnlyLineZero()
        {
            Assert.AreEqual(Pages(new[] { "aaaa", "bbbb cccc" }), Wrap("aaaa bbbb cccc", 2, 30f));
        }

        // ── AllocateTimings ──────────────────────────────────────────────────────

        private static List<float> Timings(float total, params string[][] pages) =>
            TextLayout.AllocateTimings(Pages(pages), total);

        [Test]
        public void EqualPagesGetEqualTime()
        {
            var t = Timings(4f, new[] { "abc" }, new[] { "def" });
            Assert.AreEqual(2f, t[0], 1e-4f);
            Assert.AreEqual(2f, t[1], 1e-4f);
        }

        [Test]
        public void TheEllipsisIsNotTimed()
        {
            var t = Timings(4f, new[] { "abc" + Ellipsis }, new[] { "def" });
            Assert.AreEqual(2f, t[0], 1e-4f);
            Assert.AreEqual(2f, t[1], 1e-4f);
        }

        [Test]
        public void TimeIsProportionalToCharacterCount()
        {
            var t = Timings(5f, new[] { "abc" }, new[] { "de" });
            Assert.AreEqual(3f, t[0], 1e-4f);
            Assert.AreEqual(2f, t[1], 1e-4f);
        }

        [Test]
        public void WhitespaceIsNotCounted()
        {
            var t = Timings(4f, new[] { "a b" }, new[] { "cd" });
            Assert.AreEqual(2f, t[0], 1e-4f);
            Assert.AreEqual(2f, t[1], 1e-4f);
        }

        [Test]
        public void ASinglePageGetsAllTheTime()
        {
            Assert.AreEqual(3f, Timings(3f, new[] { "hello world" })[0], 1e-4f);
        }

        [Test]
        public void AnEmptyPageCountsAsOneCharacter()
        {
            Assert.AreEqual(2f, Timings(2f, new[] { "" })[0], 1e-4f);
        }
    }
}
