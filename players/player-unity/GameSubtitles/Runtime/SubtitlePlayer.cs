using System;
using System.Collections.Generic;

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
    ///   // In MonoBehaviour.Update:
    ///   player.Tick(Time.deltaTime);
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

        // ── Public API ────────────────────────────────────────────────────────────

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
        /// <param name="duration">Total display seconds (&gt; 0).</param>
        public void Start(string text, float duration)
        {
            _running = false;
            _renderer?.Clear();

            _elapsed   = 0f;
            _pageIndex = 0;
            _done      = false;

            if (_renderer == null)
                return;

            float containerWidth = _renderer.GetContainerWidth();
            _pages   = TextLayout.WrapAndPaginate(text, _renderer.MeasureLineWidth, containerWidth, Math.Max(1, MaxLines));
            _timings = TextLayout.AllocateTimings(_pages, duration);

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
                    _renderer?.Clear();
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
            if (_renderer != null && _pageIndex < _pages.Count)
                _renderer.Render(_pages[_pageIndex].ToArray());
        }
    }
}
