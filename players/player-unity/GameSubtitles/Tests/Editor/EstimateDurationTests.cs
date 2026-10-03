using NUnit.Framework;

namespace GameSubtitles.Tests
{
    // The same table is pinned in the JS (estimateDuration.test.js) and Unreal
    // (GameSubtitleEstimateDurationTest.cpp) players. Keep all three in step.
    public class EstimateDurationTests
    {
        private const string Line = "Get down, now! They have seen us."; // 33 characters
        private const float  Tolerance = 1e-4f;

        // label, text, charsPerSecond, minSeconds, maxSeconds, expected seconds
        private static readonly object[][] Cases =
        {
            new object[] { "empty text clamps to the floor",     "",                                                 14f, 1.5f, 18f, 1.5f },
            new object[] { "one-word bark clamps to the floor",  "No!",                                              14f, 1.5f, 18f, 1.5f },
            new object[] { "plain line is chars / 14",           Line,                                               14f, 1.5f, 18f, 33f / 14f },
            new object[] { "soft hyphens are not counted",       "Ex­tra­or­di­nary, isn’t it?", 14f, 1.5f, 18f, 24f / 14f },
            new object[] { "CJK counts characters, not words",   "今日はとても良い天気ですね。散歩に行きましょうか。",       14f, 1.5f, 18f, 25f / 14f },
            new object[] { "astral character counts once",       "\U00020BB7野家で牛丼を食べました。",                      4f, 1.5f, 18f, 13f / 4f },
            new object[] { "long text clamps to the ceiling",    new string('x', 300),                               14f, 1.5f, 18f, 18f },
            new object[] { "minSeconds override",                "Hi",                                               14f, 0.5f, 18f, 0.5f },
            new object[] { "maxSeconds override",                Line,                                               14f, 1.5f, 2f,  2f },
            new object[] { "non-positive rate falls back to 14", Line,                                               0f,  1.5f, 18f, 33f / 14f },
        };

        [TestCaseSource(nameof(Cases))]
        public void EstimateDuration_MatchesSharedTable(string label, string text,
                                                        float charsPerSecond, float minSeconds, float maxSeconds,
                                                        float expected)
        {
            float actual = SubtitlePlayer.EstimateDuration(text, charsPerSecond, minSeconds, maxSeconds);
            Assert.AreEqual(expected, actual, Tolerance, label);
        }

        [Test]
        public void EstimateDuration_DefaultsMatchSharedRule()
        {
            Assert.AreEqual(33f / 14f, SubtitlePlayer.EstimateDuration(Line), Tolerance);
            Assert.AreEqual(1.5f, SubtitlePlayer.EstimateDuration(null), Tolerance);
        }

        // Line estimates to 33 / 14 ≈ 2.357 s.
        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void Start_UsesEstimateWhenDurationIsNotPositive(float duration)
        {
            var player = new SubtitlePlayer();
            player.Initialize(new FakeRenderer(), 2);
            int completed = 0;
            player.OnComplete += () => completed++;

            player.Start(Line, duration);
            player.Tick(2.3f);
            Assert.AreEqual(0, completed);
            player.Tick(0.1f);
            Assert.AreEqual(1, completed);
        }

        [Test]
        public void Start_UsesEstimateWhenDurationIsOmitted()
        {
            var player = new SubtitlePlayer();
            player.Initialize(new FakeRenderer(), 2);
            int completed = 0;
            player.OnComplete += () => completed++;

            player.Start(Line);
            player.Tick(2.3f);
            Assert.AreEqual(0, completed);
            player.Tick(0.1f);
            Assert.AreEqual(1, completed);
        }

        private class FakeRenderer : ISubtitleRenderer
        {
            public float MeasureLineWidth(string text, bool useCharacterNameFont = false) => text.Length * 10f;
            public float GetContainerWidth() => 1000f;
            public void Render(string[] lines, CharacterContext? characterContext = null) { }
            public void Clear() { }
        }
    }
}
