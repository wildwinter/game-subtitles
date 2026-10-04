using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameSubtitles.Tests
{
    // The widget draws "Name:" and the body as separate segments, so the gap between them
    // must match the space the player reserves by measuring "Name: ". TextMeshPro leaves
    // trailing spaces out of its preferred width, which once lost the gap altogether.
    public class SubtitleWidgetTests
    {
        private GameObject     _canvas;
        private SubtitleWidget _widget;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("Canvas", typeof(Canvas));
            var go = new GameObject("Widget", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            _widget = go.AddComponent<SubtitleWidget>();
            _widget.SubtitleFontSize       = 20f;
            _widget.CharacterNameFontSize  = 24f;
            _widget.ContainerWidthOverride = 600f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_canvas);

        [Test]
        public void MeasuringCountsTrailingSpaces()
        {
            Assert.Greater(_widget.MeasureLineWidth("Tam: ", true), _widget.MeasureLineWidth("Tam:", true));
            Assert.Greater(_widget.MeasureLineWidth("Tam: "), _widget.MeasureLineWidth("Tam:"));
        }

        [Test]
        public void TheNameRowDrawsTheGapThePlayerReserves()
        {
            _widget.Render(new[] { "Oh, go on then." },
                           new CharacterContext { Name = "Tam", Color = Color.yellow });

            Transform row = _widget.transform.Cast<Transform>().First(t => t.name == "SubtitleLine");
            var hlg       = row.GetComponent<HorizontalLayoutGroup>();
            var nameText  = row.GetChild(0).GetComponent<TMP_Text>();
            var nameWidth = row.GetChild(0).GetComponent<LayoutElement>().preferredWidth;

            Assert.AreEqual("Tam:", nameText.text);
            Assert.AreEqual("Oh, go on then.", row.GetChild(1).GetComponent<TMP_Text>().text);
            Assert.Greater(hlg.spacing, 0f, "no gap after the name");
            Assert.AreEqual(_widget.MeasureLineWidth("Tam: ", true), nameWidth + hlg.spacing, 0.01f);
        }
    }
}
