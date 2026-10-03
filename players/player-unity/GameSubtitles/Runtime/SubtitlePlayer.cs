using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameSubtitles
{
    /// <summary>
    /// Manages paginated subtitle display driven by caller-supplied ticks.
    ///
    /// Create once for a given renderer and line count, then reuse across any number
    /// of subtitles by calling <see cref="Start"/> each time.
    ///
    /// Usage:
    ///   var player = new SubtitlePlayer();
    ///   player.Initialize(myRenderer, maxLines: 2);
    ///   player.OnComplete += () => Debug.Log("Done");
    ///   player.Start("Hello world", 5.0f);
    ///   player.Start("Hello world"); // no audio: duration estimated from the text
    ///   player.Tick(Time.deltaTime); // called from Update()
    /// </summary>
    public class SubtitlePlayer
    {
        /// <summary>Fired when all pages have been displayed and the subtitle has finished.</summary>
        public event Action OnComplete;

        /// <summary>Lines per page. Change takes effect on the next <see cref="Start"/>.</summary>
        public int MaxLines = 2;

        /// <summary>Number of pages in the current subtitle layout. Valid after <see cref="Start"/>; 0 before.</summary>
        public int PageCount => _pages.Count;

        private ISubtitleRenderer    _renderer;
        private List<List<string>>   _pages   = new List<List<string>>();
        private List<float>          _timings = new List<float>();
        private int                  _pageIndex;
        private float                _elapsed;
        private bool                 _running;
        private bool                 _done;
        private string               _characterName;
        private Color?               _characterNameColor;
        private Color?               _lineColor;

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Estimates how long a subtitle should stay on screen when no duration is known.
        ///
        /// Counts characters (Unicode code points, ignoring U+00AD soft hyphens), divides by
        /// the reading rate, and clamps the result. Counting characters rather than words means
        /// CJK text, which has no spaces, still gets a sensible estimate; pass a lower
        /// <paramref name="charsPerSecond"/> for languages that are read more slowly per character.
        ///
        /// The JS and Unreal players implement the same rule with the same defaults.
        /// </summary>
        /// <param name="text">Subtitle text; may contain U+00AD soft hyphens. Null counts as empty.</param>
        /// <param name="charsPerSecond">Reading rate. Values &lt;= 0 fall back to 14.</param>
        /// <param name="minSeconds">Shortest estimate returned.</param>
        /// <param name="maxSeconds">Longest estimate returned.</param>
        /// <returns>Estimated display time in seconds.</returns>
        public static float EstimateDuration(string text,
                                             float charsPerSecond = 14f,
                                             float minSeconds     = 1.5f,
                                             float maxSeconds     = 18f)
        {
            int count = 0;
            if (text != null)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    char ch = text[i];
                    if (ch == '\u00AD')
                        continue;
                    // Count a surrogate pair once, as a single code point.
                    if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                        i++;
                    count++;
                }
            }

            float rate = charsPerSecond > 0f ? charsPerSecond : 14f;
            return Math.Min(Math.Max(count / rate, minSeconds), maxSeconds);
        }

        /// <summary>
        /// Bind the renderer and set the initial lines-per-page value.
        /// Call this once before the first <see cref="Start"/>.
        /// </summary>
        /// <param name="renderer">Any object that implements <see cref="ISubtitleRenderer"/>.</param>
        /// <param name="maxLines">Lines per page (&gt;= 1). Defaults to 2.</param>
        public void Initialize(ISubtitleRenderer renderer, int maxLines = 2)
        {
            _renderer = renderer;
            MaxLines  = Math.Max(1, maxLines);
        }

        /// <summary>
        /// Loads a subtitle, lays out text, and renders page 0 immediately.
        /// Calling this while another subtitle is playing stops it first.
        /// </summary>
        /// <param name="text">Text; may contain U+00AD soft hyphens.</param>
        /// <param name="duration">
        /// Total display seconds. When omitted or &lt;= 0, <see cref="EstimateDuration"/> is used instead.
        /// </param>
        /// <param name="characterName">
        /// If non-null, "Name: " is prepended to the first line of every page.
        /// The text is laid out with space reserved for the prefix.
        /// </param>
        /// <param name="characterNameColor">
        /// Colour for the character name. Pass <c>null</c> to use the renderer's default text colour.
        /// Only used when <paramref name="characterName"/> is non-null.
        /// </param>
        /// <param name="lineColor">
        /// Colour for the subtitle body text on all lines.
        /// Pass <c>null</c> to use the renderer's default text colour.
        /// </param>
        public void Start(string text, float duration = 0f,
                          string characterName = null, Color? characterNameColor = null,
                          Color? lineColor = null)
        {
            _running = false;
            _renderer?.Clear();

            _characterName      = characterName;
            _characterNameColor = characterNameColor;
            _lineColor          = lineColor;
            _elapsed            = 0f;
            _pageIndex          = 0;
            _done               = false;

            if (_renderer == null)
                return;

            float containerWidth  = _renderer.GetContainerWidth();
            float firstLineIndent = string.IsNullOrEmpty(characterName)
                ? 0f
                : Mathf.Ceil(_renderer.MeasureLineWidth(characterName + ": ", useCharacterNameFont: true));

            _pages   = TextLayout.WrapAndPaginate(text, t => _renderer.MeasureLineWidth(t),
                                                  containerWidth, Math.Max(1, MaxLines), firstLineIndent);
            _timings = TextLayout.AllocateTimings(_pages, duration > 0f ? duration : EstimateDuration(text));

            _running = true;
            RenderCurrent();
        }

        /// <summary>
        /// Advances the internal clock. Call once per frame from <c>MonoBehaviour.Update()</c>.
        /// Advances pages automatically; fires <see cref="OnComplete"/> and stops when the last page expires.
        /// </summary>
        /// <param name="deltaSeconds">Time elapsed since the last tick.</param>
        public void Tick(float deltaSeconds)
        {
            if (!_running || _done)
                return;

            _elapsed += deltaSeconds;

            while (_elapsed >= _timings[_pageIndex])
            {
                _elapsed -= _timings[_pageIndex];
                _pageIndex++;

                if (_pageIndex >= _pages.Count)
                {
                    _done    = true;
                    _running = false;
                    //_renderer?.Clear(); // This is the responsibility of the caller after receiving the OnComplete event, so they can choose to leave the final page visible if desired. They can call Stop()
                    OnComplete?.Invoke();
                    return;
                }

                RenderCurrent();
            }
        }

        /// <summary>Stops playback and clears the renderer. Does not fire <see cref="OnComplete"/>.</summary>
        public void Stop()
        {
            _running = false;
            _renderer?.Clear();
        }

        /// <summary>
        /// Clears the renderer and resets to the pre-Start state.
        /// Call <see cref="Start"/> again to replay from the beginning.
        /// </summary>
        public void Reset()
        {
            _running   = false;
            _done      = false;
            _pageIndex = 0;
            _elapsed   = 0f;
            _pages.Clear();
            _timings.Clear();
            _renderer?.Clear();
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void RenderCurrent()
        {
            if (_renderer == null || _pageIndex >= _pages.Count)
                return;

            CharacterContext? ctx = null;
            if (!string.IsNullOrEmpty(_characterName) || _lineColor.HasValue)
            {
                ctx = new CharacterContext
                {
                    Name      = _characterName,
                    Color     = _characterNameColor,
                    LineColor = _lineColor,
                };
            }

            // Pad to MaxLines so the renderer always receives a full-height page.
            List<string> page = new List<string>(_pages[_pageIndex]);
            while (page.Count < MaxLines)
                page.Add(" ");

            _renderer.Render(page.ToArray(), ctx);
        }
    }
}
