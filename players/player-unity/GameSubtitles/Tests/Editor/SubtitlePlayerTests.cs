using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GameSubtitles.Tests
{
    // The same cases are in the JS (SubtitlePlayer.test.js), Unreal (GameSubtitlePlayerTest.cpp),
    // and Godot (tests/run_tests.gd) players. Keep all four in step.
    public class SubtitlePlayerTests
    {
        private const string Shy = "­";

        // A renderer with a 100-wide container and 10-wide monospace characters, which records calls.
        private class FakeRenderer : ISubtitleRenderer
        {
            public readonly List<string[]>          Rendered = new List<string[]>();
            public readonly List<CharacterContext?> Contexts = new List<CharacterContext?>();
            public readonly List<(string Text, bool UseCharacterNameFont)> Measured = new List<(string, bool)>();
            public int Cleared;

            public float MeasureLineWidth(string text, bool useCharacterNameFont = false)
            {
                Measured.Add((text, useCharacterNameFont));
                return text.Length * 10f;
            }

            public float GetContainerWidth() => 100f;

            public void Render(string[] lines, CharacterContext? characterContext = null)
            {
                Rendered.Add(lines);
                Contexts.Add(characterContext);
            }

            public void Clear() => Cleared++;
        }

        private FakeRenderer   _renderer;
        private SubtitlePlayer _player;
        private int            _completed;

        private void Create(int maxLines)
        {
            _renderer  = new FakeRenderer();
            _player    = new SubtitlePlayer();
            _player.Initialize(_renderer, maxLines);
            _completed = 0;
            _player.OnComplete += () => _completed++;
        }

        [Test]
        public void RendersPageZeroOnStartPaddedToMaxLines()
        {
            Create(2);
            _player.Start("hello", 4f);
            Assert.AreEqual(1, _renderer.Rendered.Count);
            Assert.AreEqual(new[] { "hello", " " }, _renderer.Rendered[0]);
        }

        [Test]
        public void AdvancesAPageWhenItsTimeIsUp()
        {
            Create(1);
            _player.Start("aaaaa bbbbb", 4f);
            Assert.AreEqual(1, _renderer.Rendered.Count);
            _player.Tick(2.1f); // the first page gets 2 s; its ellipsis is not timed
            Assert.AreEqual(2, _renderer.Rendered.Count);
            Assert.AreEqual(new[] { "bbbbb" }, _renderer.Rendered[1]);
        }

        [Test]
        public void CompletesAfterTheLastPageAndKeepsItVisible()
        {
            Create(2);
            _player.Start("hi", 2f);
            int clearedBefore = _renderer.Cleared;
            _player.Tick(2.5f);
            Assert.AreEqual(1, _completed);
            Assert.AreEqual(clearedBefore, _renderer.Cleared, "the renderer was cleared on completion");
            _player.Tick(5f);
            Assert.AreEqual(1, _completed, "OnComplete fired again");
        }

        [Test]
        public void StopPreventsCompletionAndClears()
        {
            Create(2);
            _player.Start("hi", 5f);
            int clearedBefore = _renderer.Cleared;
            _player.Stop();
            _player.Tick(10f);
            Assert.AreEqual(0, _completed);
            Assert.AreEqual(clearedBefore + 1, _renderer.Cleared);
        }

        [Test]
        public void ResetClearsAndAllowsReplay()
        {
            Create(2);
            _player.Start("hello", 2f);
            _player.Reset();
            Assert.AreEqual(0, _player.PageCount);
            _player.Start("hello", 2f);
            Assert.AreEqual(2, _renderer.Rendered.Count);
        }

        [Test]
        public void TickBeforeStartDoesNothing()
        {
            Create(2);
            _player.Tick(5f);
            Assert.AreEqual(0, _renderer.Rendered.Count);
        }

        [Test]
        public void ALargeTickSkipsToCompletion()
        {
            Create(1);
            _player.Start("aaaaaa bbbbbb cccccc", 3f);
            Assert.AreEqual(3, _player.PageCount);
            _player.Tick(10f);
            Assert.AreEqual(1, _completed);
        }

        [Test]
        public void LaysOutPreprocessorOutput()
        {
            // Entry 1 of the JS player's fixtures/processed.json.
            string text = "In­ter­na­tion­al­i­za­tion is ex­traor­di­nar­i­ly dif­fi­cult.";
            Create(2);
            _player.Start(text, 6f);
            Assert.Greater(_player.PageCount, 1);
            foreach (string line in _renderer.Rendered[0])
                StringAssert.DoesNotContain(Shy, line);
        }

        [Test]
        public void PassesTheCharacterContextOnEveryPage()
        {
            Create(1);
            // "V: " takes 30, so line 0 has 100 - 10 - 30 = 60: two pages.
            _player.Start("aaaaa bbbbb", 4f, "V", Color.magenta);
            _player.Tick(2.1f);
            Assert.AreEqual(2, _renderer.Contexts.Count);
            foreach (var ctx in _renderer.Contexts)
            {
                Assert.IsTrue(ctx.HasValue);
                Assert.AreEqual("V", ctx.Value.Name);
                Assert.AreEqual(Color.magenta, ctx.Value.Color);
                Assert.IsNull(ctx.Value.LineColor);
            }
        }

        [Test]
        public void PassesNoContextWithoutANameOrLineColour()
        {
            Create(2);
            _player.Start("hello", 2f);
            Assert.IsFalse(_renderer.Contexts[0].HasValue);
        }

        [Test]
        public void PassesAContextForALineColourAlone()
        {
            Create(2);
            _player.Start("hello", 2f, lineColor: Color.red);
            Assert.IsTrue(_renderer.Contexts[0].HasValue);
            Assert.IsTrue(string.IsNullOrEmpty(_renderer.Contexts[0].Value.Name));
            Assert.AreEqual(Color.red, _renderer.Contexts[0].Value.LineColor);
        }

        [Test]
        public void MeasuresTheNamePrefixWithTheCharacterNameFont()
        {
            Create(2);
            _player.Start("hello", 2f, "Rex");
            Assert.IsTrue(_renderer.Measured.Contains(("Rex: ", true)));
            Assert.IsFalse(_renderer.Measured.Any(m => m.Text != "Rex: " && m.UseCharacterNameFont));
        }
    }
}
