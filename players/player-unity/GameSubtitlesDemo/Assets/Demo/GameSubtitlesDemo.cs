using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameSubtitles.Demo
{
    /// <summary>
    /// Full-screen demo scene for the GameSubtitles package.
    ///
    /// Attach to an empty GameObject in any scene. The MonoBehaviour constructs its
    /// entire Canvas UI tree programmatically in <see cref="Awake"/> — no prefab or
    /// scene hierarchy is required.
    ///
    /// Features (mirrors the Unreal and JS demos):
    ///   - Loads Resources/subtitles.json at start
    ///   - Language selector (English / Français / Svenska / Español)
    ///   - Script dropdown, ▶ Start / ■ Stop / ↺ Reset buttons
    ///   - 1× / 2× speed toggle
    ///   - Lines −/+ control (1–5 lines per page)
    ///   - Font −/+ control (10–32 px)
    ///   - Progress bar + elapsed / total time display
    ///   - Status line
    ///
    /// JSON data files must be placed in a Resources/ folder adjacent to this script
    /// (or anywhere under Assets/). Copy the files from
    ///   player-unreal/GameSubtitlesDemo/Content/Demo/
    /// into
    ///   Assets/Demo/Resources/
    /// </summary>
    public class GameSubtitlesDemo : MonoBehaviour
    {
        // ── Colour palette ────────────────────────────────────────────────────────

        private static readonly Color ColBgDark    = HexColor("#0d1117");
        private static readonly Color ColBgPanel   = HexColor("#161b22");
        private static readonly Color ColBgScene   = HexColor("#160a25"); // dark purple sky
        private static readonly Color ColBgSubBar  = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color ColTextMain  = HexColor("#c9d1d9");
        private static readonly Color ColTextMuted = HexColor("#8b949e");
        private static readonly Color ColAccent    = HexColor("#f0cc88");
        private static readonly Color ColBtnGreen  = HexColor("#238636");
        private static readonly Color ColBtnGray   = HexColor("#21262d");

        // ── State ─────────────────────────────────────────────────────────────────

        private List<SubtitleEntry> _scripts = new List<SubtitleEntry>();
        private SubtitlePlayer      _player;
        private SubtitleWidget      _subWidget;

        private bool  _isRunning    = false;
        private float _elapsedMs    = 0f;
        private float _totalMs      = 0f;
        private int   _maxLines     = 2;
        private int   _fontSize     = 16;
        private bool  _doubleSpeed  = false;

        // ── Built UI widgets ──────────────────────────────────────────────────────

        private TMP_Dropdown  _langDropdown;
        private TMP_Dropdown  _scriptDropdown;
        private Button        _btnStart;
        private Button        _btnStop;
        private Button        _btnReset;
        private Button        _btnSpeed;
        private Button        _btnLinesDec;
        private Button        _btnLinesInc;
        private Button        _btnFontDec;
        private Button        _btnFontInc;
        private Slider        _progressBar;
        private TMP_Text      _statusText;
        private TMP_Text      _pageInfoText;
        private TMP_Text      _timeInfoText;
        private TMP_Text      _linesCountText;
        private TMP_Text      _fontSizeText;
        private TMP_Text      _speakerNameText;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            LoadSubtitles("subtitles");
            BuildUI();
        }

        private void Start()
        {
            _player = new SubtitlePlayer();
            _player.Initialize(_subWidget, _maxLines);
            _player.OnComplete += OnSubtitleComplete;

            SetRunning(false);

            if (_scripts.Count == 0)
            {
                SetStatus("No subtitle data found. Copy JSON files into Assets/Demo/Resources/.");
                if (_btnStart != null) _btnStart.interactable = false;
            }
            else
            {
                SetStatus("Select a subtitle entry and press Start.");
            }
        }

        private void OnDestroy()
        {
            _player?.Stop();
        }

        private void Update()
        {
            if (!_isRunning || _player == null)
                return;

            float multiplier = _doubleSpeed ? 2f : 1f;
            float delta      = Time.deltaTime * multiplier;

            _elapsedMs = Mathf.Min(_elapsedMs + Time.deltaTime * 1000f * multiplier, _totalMs);
            _player.Tick(delta);
            UpdateProgress();
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void LoadSubtitles(string resourceName)
        {
            var asset = Resources.Load<TextAsset>(resourceName);
            if (asset == null)
            {
                Debug.LogWarning($"GameSubtitlesDemo: could not load Resources/{resourceName}.json");
                return;
            }

            string json = asset.text;

            // Strip UTF-8 BOM if present (preprocessor tool emits it)
            if (json.Length > 0 && json[0] == '\uFEFF')
                json = json.Substring(1);

            // Unity's JsonUtility cannot deserialize a top-level array; wrap it
            string wrapped = "{\"items\":" + json + "}";
            SubtitleEntryList list;
            try   { list = JsonUtility.FromJson<SubtitleEntryList>(wrapped); }
            catch { Debug.LogWarning($"GameSubtitlesDemo: failed to parse {resourceName}.json"); return; }

            if (list?.items == null) return;

            _scripts.Clear();
            foreach (var raw in list.items)
            {
                if (string.IsNullOrEmpty(raw.subtitle)) continue;

                // Duration: ~14 chars/s, clamped to [3, 18] seconds
                string clean = raw.subtitle.Replace("\u00AD", "");
                float  dur   = Mathf.Clamp(Mathf.Round(clean.Length / 14f), 3f, 18f);

                _scripts.Add(new SubtitleEntry
                {
                    id       = raw.id,
                    speaker  = raw.speaker,
                    text     = raw.subtitle,
                    duration = dur
                });
            }

            // If called after initial build (i.e. on language switch), refresh dropdown
            if (_scriptDropdown != null)
                PopulateScriptDropdown();
        }

        private void PopulateScriptDropdown()
        {
            if (_scriptDropdown == null) return;

            _scriptDropdown.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>();

            if (_scripts.Count == 0)
            {
                options.Add(new TMP_Dropdown.OptionData("(no subtitles loaded)"));
            }
            else
            {
                foreach (var s in _scripts)
                {
                    string preview = s.text.Replace("\u00AD", "");
                    if (preview.Length > 42)
                        preview = preview.Substring(0, 42) + "\u2026";
                    else
                        preview = preview.Left(42);
                    options.Add(new TMP_Dropdown.OptionData(
                        $"[{s.id}] {s.speaker} \u2014 {preview}"));
                }
            }

            _scriptDropdown.AddOptions(options);
            if (_btnStart != null) _btnStart.interactable = _scripts.Count > 0;
        }

        // ── UI construction ───────────────────────────────────────────────────────

        private void BuildUI()
        {
            // Root Canvas (ScreenSpaceOverlay)
            var canvasGo = new GameObject("GameSubtitlesDemoCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>(); // default pixel-perfect
            canvasGo.AddComponent<GraphicRaycaster>();

            // Dark full-screen background
            var bg = MakePanel(canvasGo.transform, ColBgDark, "Background");
            StretchFill(bg);

            // Content: centred vertical stack
            var content = MakeVerticalLayout(canvasGo.transform, "Content",
                padding: new RectOffset(0, 0, 16, 24), spacing: 0f);
            StretchFill(content.transform.parent == null ? content : content);
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin = contentRT.anchorMax = new Vector2(0.5f, 0.5f);
            contentRT.pivot     = new Vector2(0.5f, 0.5f);
            contentRT.sizeDelta = new Vector2(560f, 0f);
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Title ─────────────────────────────────────────────────────────────
            {
                var title = MakeLabel(content.transform, "GAME SUBTITLES \u2014 PLAYER DEMO", 11f, ColAccent);
                var slot  = content.GetComponent<VerticalLayoutGroup>();
                AddLayoutElement(title, minHeight: 28f);
                title.alignment = TextAlignmentOptions.Center;
            }

            // ── Scene area ────────────────────────────────────────────────────────
            {
                // 540×304 container
                var sceneGo = new GameObject("SceneArea");
                sceneGo.transform.SetParent(content.transform, false);
                AddLayoutElement(sceneGo, minWidth: 540f, minHeight: 304f, flexWidth: 0f);
                var sceneRT = sceneGo.AddComponent<RectTransform>();
                sceneRT.sizeDelta = new Vector2(540f, 304f);

                // Purple sky background
                var sceneBg = MakePanel(sceneGo.transform, ColBgScene, "SceneBg");
                StretchFill(sceneBg);

                // Footer panel: speaker name + subtitle bar (bottom-anchored)
                var footerGo = new GameObject("Footer");
                footerGo.transform.SetParent(sceneGo.transform, false);
                var footerRT = footerGo.AddComponent<RectTransform>();
                footerRT.anchorMin = new Vector2(0f, 0f);
                footerRT.anchorMax = new Vector2(1f, 0f);
                footerRT.pivot     = new Vector2(0.5f, 0f);
                footerRT.sizeDelta = Vector2.zero;
                var footerLayout = footerGo.AddComponent<VerticalLayoutGroup>();
                footerLayout.childAlignment = TextAnchor.LowerCenter;
                footerLayout.childForceExpandWidth = true;
                footerLayout.childForceExpandHeight = false;
                footerLayout.spacing = 0f;
                var footerFit = footerGo.AddComponent<ContentSizeFitter>();
                footerFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Speaker name
                _speakerNameText = MakeLabel(footerGo.transform, "", 8.5f, ColAccent);
                _speakerNameText.alignment = TextAlignmentOptions.Center;
                AddLayoutElement(_speakerNameText.gameObject, minHeight: 14f);
                var speakerLE = _speakerNameText.GetComponent<LayoutElement>()
                                ?? _speakerNameText.gameObject.AddComponent<LayoutElement>();
                speakerLE.minHeight = 14f;

                // Dark subtitle bar
                var subBarGo = new GameObject("SubBar");
                subBarGo.transform.SetParent(footerGo.transform, false);
                AddLayoutElement(subBarGo, minHeight: 0f);
                var subBarImg = subBarGo.AddComponent<Image>();
                subBarImg.color = ColBgSubBar;
                var subBarRT = subBarGo.GetComponent<RectTransform>();
                subBarRT.sizeDelta = Vector2.zero;
                var subBarLayout = subBarGo.AddComponent<VerticalLayoutGroup>();
                subBarLayout.padding = new RectOffset(0, 0, 8, 12);
                subBarLayout.childForceExpandWidth  = true;
                subBarLayout.childForceExpandHeight = false;
                subBarLayout.childAlignment = TextAnchor.LowerCenter;
                var subBarFit = subBarGo.AddComponent<ContentSizeFitter>();
                subBarFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // SubtitleWidget renderer
                var subWidgetGo = new GameObject("SubtitleWidget");
                subWidgetGo.transform.SetParent(subBarGo.transform, false);
                subWidgetGo.AddComponent<RectTransform>();
                _subWidget = subWidgetGo.AddComponent<SubtitleWidget>();
                _subWidget.FontSize            = _fontSize;
                _subWidget.TextColor           = Color.white;
                _subWidget.ContainerWidthOverride = 540f; // set before Start() since widget may not be laid out yet
                AddLayoutElement(subWidgetGo, minHeight: 0f);
                var swFit = subWidgetGo.AddComponent<ContentSizeFitter>();
                swFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                AddLayoutElement(sceneGo, minWidth: 540f, minHeight: 304f, flexWidth: 0f);
            }

            // Spacer after scene
            MakeSpacer(content.transform, 12f);

            // ── Controls row: language, script, Start/Stop/Reset ─────────────────
            {
                var row = MakeHorizontalLayout(content.transform, "CtrlRow", spacing: 6f);
                AddLayoutElement(row, minHeight: 28f);

                // Language dropdown
                _langDropdown = MakeDropdown(row.transform, 110f,
                    new[] { "English", "Fran\u00E7ais", "Svenska", "Espa\u00F1ol" });
                _langDropdown.onValueChanged.AddListener(OnLangChanged);

                // Script dropdown (fill remaining space)
                _scriptDropdown = MakeDropdown(row.transform, 0f, Array.Empty<string>());
                AddLayoutElement(_scriptDropdown.gameObject, flexWidth: 1f);
                _scriptDropdown.onValueChanged.AddListener(idx => DoReset());
                PopulateScriptDropdown();

                _btnStart = MakeButton(row.transform, "\u25B6 Start", ColBtnGreen, () => DoStart());
                _btnStop  = MakeButton(row.transform, "\u25A0 Stop",  ColBtnGray,  () => DoStop());
                _btnReset = MakeButton(row.transform, "\u21BA Reset", ColBtnGray,  () => DoReset());
            }

            MakeSpacer(content.transform, 8f);

            // ── Options row: speed, lines, font ──────────────────────────────────
            {
                var row = MakeHorizontalLayout(content.transform, "OptRow", spacing: 6f);
                AddLayoutElement(row, minHeight: 28f);

                _btnSpeed = MakeButton(row.transform, "1\xD7 Speed", ColBtnGray, OnSpeedToggle);
                AddHorizontalSpacer(row.transform, 10f);

                MakeSmallLabel(row.transform, "Lines:");
                _btnLinesDec    = MakeButton(row.transform, "\u2212", ColBtnGray, () => ChangeLine(-1));
                _linesCountText = MakeSmallLabel(row.transform, _maxLines.ToString());
                _btnLinesInc    = MakeButton(row.transform, "+",     ColBtnGray, () => ChangeLine(+1));
                AddHorizontalSpacer(row.transform, 10f);

                MakeSmallLabel(row.transform, "Font:");
                _btnFontDec  = MakeButton(row.transform, "\u2212",        ColBtnGray, () => ChangeFont(-2));
                _fontSizeText = MakeSmallLabel(row.transform, $"{_fontSize}px");
                _btnFontInc  = MakeButton(row.transform, "+",            ColBtnGray, () => ChangeFont(+2));
            }

            MakeSpacer(content.transform, 12f);

            // ── Progress bar ──────────────────────────────────────────────────────
            {
                var sliderGo = new GameObject("ProgressBar");
                sliderGo.transform.SetParent(content.transform, false);
                AddLayoutElement(sliderGo, minWidth: 540f, minHeight: 8f, flexWidth: 0f);
                var sliderRT = sliderGo.AddComponent<RectTransform>();
                sliderRT.sizeDelta = new Vector2(540f, 8f);

                _progressBar = sliderGo.AddComponent<Slider>();
                _progressBar.minValue    = 0f;
                _progressBar.maxValue    = 1f;
                _progressBar.value       = 0f;
                _progressBar.interactable = false;
                _progressBar.transition  = Selectable.Transition.None;

                // Background
                var bgImg = MakePanel(sliderGo.transform, ColBgPanel, "BG");
                StretchFill(bgImg);

                // Fill area
                var fillAreaGo = new GameObject("FillArea");
                fillAreaGo.transform.SetParent(sliderGo.transform, false);
                var fillAreaRT = fillAreaGo.AddComponent<RectTransform>();
                fillAreaRT.anchorMin = Vector2.zero;
                fillAreaRT.anchorMax = Vector2.one;
                fillAreaRT.sizeDelta = Vector2.zero;

                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(fillAreaGo.transform, false);
                var fillImg = fillGo.AddComponent<Image>();
                fillImg.color = ColAccent;
                var fillRT = fillGo.GetComponent<RectTransform>();
                fillRT.anchorMin = Vector2.zero;
                fillRT.anchorMax = new Vector2(0f, 1f);
                fillRT.sizeDelta = Vector2.zero;

                _progressBar.fillRect = fillRT;
            }

            MakeSpacer(content.transform, 4f);

            // ── Progress meta row ─────────────────────────────────────────────────
            {
                var row = MakeHorizontalLayout(content.transform, "MetaRow", spacing: 0f);
                AddLayoutElement(row, minWidth: 540f, minHeight: 16f, flexWidth: 0f);

                _pageInfoText = MakeLabel(row.transform, "", 9f, ColTextMuted);
                AddLayoutElement(_pageInfoText.gameObject, flexWidth: 1f);

                _timeInfoText = MakeLabel(row.transform, "", 9f, ColTextMuted);
                _timeInfoText.alignment = TextAlignmentOptions.Right;
                AddLayoutElement(_timeInfoText.gameObject, flexWidth: 1f);
            }

            MakeSpacer(content.transform, 8f);

            // ── Status line ───────────────────────────────────────────────────────
            {
                _statusText = MakeLabel(content.transform, "Loading\u2026", 10.5f, ColTextMuted);
                _statusText.alignment = TextAlignmentOptions.Center;
                AddLayoutElement(_statusText.gameObject, minHeight: 20f);
            }

            UpdateLinesDisplay();
            UpdateFontDisplay();
        }

        // ── Controls ──────────────────────────────────────────────────────────────

        private void DoStart()
        {
            if (_player == null || _scripts.Count == 0) return;

            _player.Stop();

            int idx = _scriptDropdown != null ? _scriptDropdown.value : 0;
            if (idx < 0 || idx >= _scripts.Count) return;

            var s = _scripts[idx];

            _totalMs   = s.duration * 1000f;
            _elapsedMs = 0f;

            if (_speakerNameText != null)
                _speakerNameText.text = s.speaker.ToUpper();

            _player.MaxLines = _maxLines;
            _player.Start(s.text, s.duration);

            SetRunning(true);
            SetStatus($"Playing: [{s.id}] {s.speaker}");
            UpdateProgress();
        }

        private void DoStop()
        {
            _player?.Stop();
            SetRunning(false);
            SetStatus("Stopped.");
        }

        private void DoReset()
        {
            _player?.Reset();
            SetRunning(false);
            _elapsedMs = 0f;
            if (_progressBar      != null) _progressBar.value = 0f;
            if (_pageInfoText      != null) _pageInfoText.text = "";
            if (_timeInfoText      != null) _timeInfoText.text = "";
            if (_speakerNameText   != null) _speakerNameText.text = "";
            SetStatus("Select a subtitle entry and press Start.");
        }

        private void OnSpeedToggle()
        {
            _doubleSpeed = !_doubleSpeed;
            if (_btnSpeed != null)
            {
                var lbl = _btnSpeed.GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.text = _doubleSpeed ? "2\xD7 Speed" : "1\xD7 Speed";
            }
        }

        private void ChangeLine(int delta)
        {
            int next = Mathf.Clamp(_maxLines + delta, 1, 5);
            if (next == _maxLines) return;
            _maxLines = next;
            if (_player != null) _player.MaxLines = _maxLines;
            UpdateLinesDisplay();
            if (_isRunning) DoStart();
        }

        private void ChangeFont(int delta)
        {
            int next = Mathf.Clamp(_fontSize + delta, 10, 32);
            if (next == _fontSize) return;
            _fontSize = next;
            if (_subWidget != null) _subWidget.FontSize = _fontSize;
            UpdateFontDisplay();
            if (_isRunning) DoStart();
        }

        private void OnLangChanged(int idx)
        {
            string[] files = { "subtitles", "subtitles-fr", "subtitles-sv", "subtitles-es" };
            DoReset();
            LoadSubtitles(idx >= 0 && idx < files.Length ? files[idx] : "subtitles");
            SetStatus("Select a subtitle entry and press Start.");
        }

        private void OnSubtitleComplete()
        {
            SetRunning(false);
            _elapsedMs = _totalMs;
            UpdateProgress();
            if (_speakerNameText != null) _speakerNameText.text = "";
            SetStatus("Finished.");
        }

        private void SetRunning(bool running)
        {
            _isRunning = running;
            if (_btnStart != null) _btnStart.interactable = !running && _scripts.Count > 0;
            if (_btnStop  != null) _btnStop.interactable  = running;
        }

        private void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
        }

        private void UpdateProgress()
        {
            float pct = _totalMs > 0f ? Mathf.Clamp01(_elapsedMs / _totalMs) : 0f;
            if (_progressBar != null) _progressBar.value = pct;

            if (_player != null && _pageInfoText != null)
            {
                int pages = _player.PageCount;
                _pageInfoText.text = pages > 1 ? $"Page ? / {pages}" : "";
            }

            if (_timeInfoText != null)
                _timeInfoText.text = $"{_elapsedMs / 1000f:F1} s / {_totalMs / 1000f:F1} s";
        }

        private void UpdateLinesDisplay()
        {
            if (_linesCountText != null) _linesCountText.text = _maxLines.ToString();
            if (_btnLinesDec    != null) _btnLinesDec.interactable = _maxLines > 1;
            if (_btnLinesInc    != null) _btnLinesInc.interactable = _maxLines < 5;
        }

        private void UpdateFontDisplay()
        {
            if (_fontSizeText != null) _fontSizeText.text = $"{_fontSize}px";
            if (_btnFontDec   != null) _btnFontDec.interactable = _fontSize > 10;
            if (_btnFontInc   != null) _btnFontInc.interactable = _fontSize < 32;
        }

        // ── UI factory helpers ────────────────────────────────────────────────────

        private static GameObject MakePanel(Transform parent, Color color, string name = "Panel")
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            go.AddComponent<RectTransform>();
            return go;
        }

        private static GameObject MakeVerticalLayout(Transform parent, string name,
            RectOffset padding = null, float spacing = 4f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var vl = go.AddComponent<VerticalLayoutGroup>();
            vl.padding                  = padding ?? new RectOffset(0, 0, 0, 0);
            vl.spacing                  = spacing;
            vl.childAlignment           = TextAnchor.UpperCenter;
            vl.childForceExpandWidth    = true;
            vl.childForceExpandHeight   = false;
            vl.childControlWidth        = true;
            vl.childControlHeight       = true;
            return go;
        }

        private static GameObject MakeHorizontalLayout(Transform parent, string name, float spacing = 4f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.spacing                 = spacing;
            hl.childAlignment          = TextAnchor.MiddleLeft;
            hl.childForceExpandWidth   = false;
            hl.childForceExpandHeight  = false;
            hl.childControlWidth       = true;
            hl.childControlHeight      = true;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        private TMP_Text MakeLabel(Transform parent, string text, float size, Color color)
        {
            var go  = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = size;
            tmp.color     = color;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.enableWordWrapping = false;
            return tmp;
        }

        private TMP_Text MakeSmallLabel(Transform parent, string text)
            => MakeLabel(parent, text, 11f, ColTextMain);

        private Button MakeButton(Transform parent, string label, Color bgColor, Action onClick)
        {
            var go = new GameObject("Button");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = bgColor;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor     = bgColor * 0.85f;
            colors.disabledColor    = new Color(bgColor.r, bgColor.g, bgColor.b, 0.38f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());

            var labelGo  = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRT  = labelGo.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.sizeDelta = new Vector2(-8f, -4f);
            var lbl = labelGo.AddComponent<TextMeshProUGUI>();
            lbl.text      = label;
            lbl.fontSize  = 11f;
            lbl.color     = ColTextMain;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.enableWordWrapping = false;

            AddLayoutElement(go, minWidth: 60f, minHeight: 24f);
            return btn;
        }

        private TMP_Dropdown MakeDropdown(Transform parent, float width, string[] options)
        {
            var go = new GameObject("Dropdown");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var img = go.AddComponent<Image>();
            img.color = ColBgPanel;

            var dd = go.AddComponent<TMP_Dropdown>();
            dd.captionText = MakeLabel(go.transform, "", 11f, ColTextMain);
            dd.captionText.alignment = TextAlignmentOptions.Left;
            dd.captionText.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            dd.captionText.GetComponent<RectTransform>().anchorMax = Vector2.one;
            dd.captionText.GetComponent<RectTransform>().sizeDelta = new Vector2(-8f, 0f);

            foreach (var opt in options)
                dd.options.Add(new TMP_Dropdown.OptionData(opt));

            if (width > 0f)
                AddLayoutElement(go, minWidth: width, minHeight: 24f, flexWidth: 0f);
            else
                AddLayoutElement(go, minHeight: 24f);

            return dd;
        }

        private static void MakeSpacer(Transform parent, float height)
        {
            var go = new GameObject("Spacer");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            AddLayoutElement(go, minHeight: height, flexHeight: 0f);
        }

        private static void AddHorizontalSpacer(Transform parent, float width)
        {
            var go = new GameObject("HSpacer");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            AddLayoutElement(go, minWidth: width, flexWidth: 0f);
        }

        private static void StretchFill(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AddLayoutElement(GameObject go,
            float minWidth = -1f, float minHeight = -1f,
            float flexWidth = -1f, float flexHeight = -1f)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (minWidth   >= 0f) le.minWidth       = minWidth;
            if (minHeight  >= 0f) le.minHeight      = minHeight;
            if (flexWidth  >= 0f) le.flexibleWidth  = flexWidth;
            if (flexHeight >= 0f) le.flexibleHeight = flexHeight;
        }

        private static void AddLayoutElement(Component c,
            float minWidth = -1f, float minHeight = -1f,
            float flexWidth = -1f, float flexHeight = -1f)
            => AddLayoutElement(c.gameObject, minWidth, minHeight, flexWidth, flexHeight);

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        // ── Data types ────────────────────────────────────────────────────────────

        [Serializable]
        private class RawEntry
        {
            public string id;
            public string speaker;
            public string subtitle;
        }

        [Serializable]
        private class SubtitleEntryList
        {
            public RawEntry[] items;
        }

        private class SubtitleEntry
        {
            public string id;
            public string speaker;
            public string text;
            public float  duration;
        }
    }

    // ── String extension ──────────────────────────────────────────────────────────

    internal static class StringExtensions
    {
        public static string Left(this string s, int n)
            => s.Length <= n ? s : s.Substring(0, n);
    }
}
