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
        [Header("Subtitle Font")]
        public TMP_FontAsset SubtitleFontAsset;
        public float         SubtitleFontSize = 16f;
        public Color         TextColor        = Color.white;

        [Header("Character Name Font")]
        [Tooltip("Font for the character-name prefix. Leave unset to use the subtitle font.")]
        public TMP_FontAsset CharacterNameFontAsset;
        [Tooltip("Size for the character-name prefix. Leave 0 to use the subtitle font size.")]
        public float         CharacterNameFontSize = 0f;

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
        public float MeasureLineWidth(string text, bool useCharacterNameFont = false)
        {
            EnsureProbe();
            if (_probe == null) return 0f;

            // Sync the probe to the font being measured (settings may have changed).
            if (useCharacterNameFont)
                ApplyFont(_probe, CharacterFontOrFallback, CharacterSizeOrFallback);
            else
                ApplyFont(_probe, SubtitleFontAsset, SubtitleFontSize);

            // TextMeshPro leaves trailing whitespace out of the preferred width, so "Name: " would
            // measure the same as "Name:". Add trailing spaces back, so the indent the player
            // reserves for the name prefix includes the gap the name row draws.
            string trimmed  = text.TrimEnd(' ');
            int    trailing = text.Length - trimmed.Length;
            float  width    = _probe.GetPreferredValues(trimmed).x;
            return trailing > 0 ? width + trailing * ProbeSpaceWidth() : width;
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
        public void Render(string[] lines, CharacterContext? characterContext = null)
        {
            ClearLineObjects();

            float y = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                bool hasName = i == 0 && characterContext.HasValue
                                       && !string.IsNullOrEmpty(characterContext.Value.Name);
                float lineH = hasName
                    ? CreateNameLine(lines[i], characterContext.Value, y)
                    : CreateBodyLine(lines[i], characterContext, y);
                y += lineH;
            }

            SetPreferredHeight(y);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            ClearLineObjects();
            SetPreferredHeight(0f);
        }

        // ── Private ───────────────────────────────────────────────────────────────

        /// <summary>Font asset for the character name, falling back to the subtitle font.</summary>
        private TMP_FontAsset CharacterFontOrFallback =>
            CharacterNameFontAsset != null ? CharacterNameFontAsset : SubtitleFontAsset;

        /// <summary>Font size for the character name, falling back to the subtitle size.</summary>
        private float CharacterSizeOrFallback =>
            CharacterNameFontSize > 0f ? CharacterNameFontSize : SubtitleFontSize;

        /// <summary>Width of one space in the character-name font.</summary>
        private float CharacterNameSpaceWidth()
        {
            EnsureProbe();
            if (_probe == null) return 0f;
            ApplyFont(_probe, CharacterFontOrFallback, CharacterSizeOrFallback);
            return ProbeSpaceWidth();
        }

        // Width of one space in the probe's current font. A lone space measures as zero (see
        // MeasureLineWidth), so measure it between two letters.
        private float ProbeSpaceWidth() =>
            _probe.GetPreferredValues("a a").x - _probe.GetPreferredValues("aa").x;

        private static void ApplyFont(TMP_Text t, TMP_FontAsset font, float size)
        {
            if (t == null) return;
            if (font != null) t.font = font;
            t.fontSize = size;
        }

        /// <summary>A single full-width centred line in the subtitle (body) font.</summary>
        private float CreateBodyLine(string text, CharacterContext? ctx, float y)
        {
            var go = new GameObject("SubtitleLine");
            go.transform.SetParent(transform, false);
            _lineObjects.Add(go);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            ApplyFont(tmp, SubtitleFontAsset, SubtitleFontSize);
            tmp.color            = (ctx.HasValue && ctx.Value.LineColor.HasValue)
                                       ? ctx.Value.LineColor.Value : TextColor;
            tmp.alignment        = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap; // layout is already done by WrapAndPaginate
            tmp.text             = text;

            var rt = go.GetComponent<RectTransform>();
            AnchorRow(rt, tmp.preferredHeight, y);
            return tmp.preferredHeight;
        }

        /// <summary>
        /// Line 0 with a character name: a horizontal row holding the name segment
        /// (character-name font) and the body segment (subtitle font), centred as a unit.
        /// </summary>
        private float CreateNameLine(string text, CharacterContext ctx, float y)
        {
            var rowGo = new GameObject("SubtitleLine");
            rowGo.transform.SetParent(transform, false);
            _lineObjects.Add(rowGo);
            var rowRt = rowGo.AddComponent<RectTransform>();

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment         = TextAnchor.MiddleCenter;
            hlg.childControlWidth       = true;
            hlg.childControlHeight      = true;
            hlg.childForceExpandWidth   = false;
            hlg.childForceExpandHeight  = false;
            // The gap after "Name:" is the row's spacing, because TextMeshPro would drop a
            // trailing space from the name segment's width. It is one space in the
            // character-name font, matching what MeasureLineWidth("Name: ", true) reserves.
            hlg.spacing                 = CharacterNameSpaceWidth();

            Vector2 nameSize = AddRowSegment(rowGo.transform, ctx.Name + ":",
                                             CharacterFontOrFallback, CharacterSizeOrFallback,
                                             ctx.Color ?? TextColor);
            Vector2 bodySize = AddRowSegment(rowGo.transform, text,
                                             SubtitleFontAsset, SubtitleFontSize,
                                             ctx.LineColor ?? TextColor);

            float lineH = Mathf.Max(nameSize.y, bodySize.y);
            AnchorRow(rowRt, lineH, y);
            return lineH;
        }

        /// <summary>Creates one TMP segment inside a name row and returns its preferred size.</summary>
        private Vector2 AddRowSegment(Transform parent, string text, TMP_FontAsset font, float size, Color color)
        {
            var go = new GameObject("Segment");
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            ApplyFont(tmp, font, size);
            tmp.color            = color;
            tmp.alignment        = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.text             = text;

            Vector2 pref = tmp.GetPreferredValues(text);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = pref.x;
            le.preferredHeight = pref.y;
            return pref;
        }

        // Anchor a line as a full-width strip, top-aligned, stacked downward.
        private static void AnchorRow(RectTransform rt, float height, float y)
        {
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.sizeDelta        = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, -y);
        }

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
            ApplyFont(tmp, SubtitleFontAsset, SubtitleFontSize);

            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(-99999f, -99999f);
            rt.sizeDelta        = new Vector2(9999f, 200f);

            // Exclude from any LayoutGroup on this widget
            var le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            _probe = tmp;
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
