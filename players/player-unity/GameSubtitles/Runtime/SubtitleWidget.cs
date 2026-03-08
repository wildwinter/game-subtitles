using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameSubtitles
{
    /// <summary>
    /// Ready-made uGUI renderer. Attach to any GameObject that has a <see cref="RectTransform"/>.
    ///
    /// Measures text via a hidden off-screen <see cref="TMP_Text"/> probe and renders each line
    /// as a <see cref="TextMeshProUGUI"/> child stacked top-to-bottom.
    ///
    /// Assign <see cref="FontAsset"/>, <see cref="FontSize"/>, and <see cref="TextColor"/>
    /// before calling <c>SubtitlePlayer.Start()</c>.
    ///
    /// If the RectTransform is not yet laid out at Start-time (e.g. first frame),
    /// set <see cref="ContainerWidthOverride"/> to the known pixel width.
    ///
    /// To customise layout, place a <see cref="VerticalLayoutGroup"/> and
    /// <see cref="ContentSizeFitter"/> on this GameObject; the widget will populate it with
    /// TextMeshProUGUI children and the layout group will arrange them automatically.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SubtitleWidget : MonoBehaviour, ISubtitleRenderer
    {
        [Header("Font")]
        public TMP_FontAsset FontAsset;
        public float         FontSize  = 16f;
        public Color         TextColor = Color.white;

        [Header("Layout")]
        [Tooltip("Override container width in pixels. Leave 0 to use the RectTransform width.")]
        public float ContainerWidthOverride = 0f;

        private RectTransform        _rectTransform;
        private TMP_Text             _probe;
        private readonly List<GameObject> _lineObjects = new List<GameObject>();

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            EnsureProbe();
        }

        private void OnDestroy()
        {
            if (_probe != null)
                Destroy(_probe.gameObject);
        }

        // ── ISubtitleRenderer ─────────────────────────────────────────────────────

        /// <inheritdoc/>
        public float MeasureLineWidth(string text)
        {
            EnsureProbe();
            if (_probe == null) return 0f;

            // Always sync font settings in case FontAsset or FontSize changed since probe was created
            SyncProbeFont();
            return _probe.GetPreferredValues(text).x;
        }

        /// <inheritdoc/>
        public float GetContainerWidth()
        {
            if (ContainerWidthOverride > 0f)
                return ContainerWidthOverride;

            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            float w = _rectTransform != null ? _rectTransform.rect.width : 0f;
            return w > 0f ? w : 540f; // fall back to a sensible default before first layout pass
        }

        /// <inheritdoc/>
        public void Render(string[] lines)
        {
            ClearLineObjects();

            float y = 0f;
            foreach (string line in lines)
            {
                var go = new GameObject("SubtitleLine");
                go.transform.SetParent(transform, false);
                _lineObjects.Add(go);

                var tmp = go.AddComponent<TextMeshProUGUI>();
                if (FontAsset != null) tmp.font = FontAsset;
                tmp.fontSize          = FontSize;
                tmp.color             = TextColor;
                tmp.alignment         = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap; // layout is already done by WrapAndPaginate
                tmp.text              = line;

                // Anchor: full-width strip, top-aligned, stacked downward
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin        = new Vector2(0f, 1f);
                rt.anchorMax        = new Vector2(1f, 1f);
                rt.pivot            = new Vector2(0.5f, 1f);
                float lineH         = tmp.preferredHeight;
                rt.sizeDelta        = new Vector2(0f, lineH);
                rt.anchoredPosition = new Vector2(0f, -y);
                y += lineH;
            }

            // Tell the parent layout system how tall we are so containers resize correctly
            SetPreferredHeight(y);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            ClearLineObjects();
            SetPreferredHeight(0f);
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void EnsureProbe()
        {
            if (_probe != null)
                return;

            // Hidden off-screen TMP_Text used only for width measurement
            var go = new GameObject("__SubtitleProbe__");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(transform, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            SyncProbeFont(tmp);

            // Position off-screen so it never appears in the rendered output
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-99999f, -99999f);
            rt.sizeDelta        = new Vector2(9999f, 200f);

            // Exclude from any LayoutGroup on this widget
            var le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            _probe = tmp;
        }

        private void SyncProbeFont(TMP_Text tmp = null)
        {
            var t = tmp ?? _probe;
            if (t == null) return;
            if (FontAsset != null) t.font = FontAsset;
            t.fontSize = FontSize;
        }

        private void ClearLineObjects()
        {
            foreach (var go in _lineObjects)
            {
                if (go != null)
                    Destroy(go);
            }
            _lineObjects.Clear();
        }

        private void SetPreferredHeight(float h)
        {
            var le = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight       = h;
            LayoutRebuilder.MarkLayoutForRebuild(_rectTransform);
        }
    }
}
