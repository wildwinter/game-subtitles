using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameSubtitles.Demo
{
    /// <summary>
    /// Full-screen demo scene for the GameSubtitles package.
    ///
    /// Attach to an empty GameObject in any scene. The MonoBehaviour constructs its
    /// entire Canvas UI tree programmatically in Awake — no prefab or scene hierarchy
    /// is required.
    ///
    /// Features:
    ///   - Loads Resources/subtitles.json at start
    ///   - Language selector (4 toggle buttons)
    ///   - Script selector (◀ / label / ▶ navigation)
    ///   - Start / Stop / Reset buttons
    ///   - 1× / 2× speed toggle
    ///   - Lines −/+ control (1–5 lines per page)
    ///   - Font −/+ control (10–32 px)
    ///   - Progress bar and elapsed / total time
    ///   - Status line
    ///
    /// JSON data files must be placed in a Resources/ folder adjacent to this script.
    /// The four subtitle JSON files are in the shared demo data folder at
    /// demo/data/ in the repository root.
    /// </summary>
    public class GameSubtitlesDemo : MonoBehaviour
    {
        // ── Colour palette ────────────────────────────────────────────────────────

        private static readonly Color ColBgDark   = Hex("#0d1117");
        private static readonly Color ColBgScene  = Hex("#160a25");
        private static readonly Color ColBgSubBar = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color ColBgPanel  = Hex("#161b22");
        private static readonly Color ColTextMain = Hex("#c9d1d9");
        private static readonly Color ColMuted    = Hex("#8b949e");
        private static readonly Color ColAccent   = Hex("#f0cc88");
        private static readonly Color ColGreen    = Hex("#238636");
        private static readonly Color ColGray     = Hex("#21262d");
        private static readonly Color ColGrayLit  = Hex("#2d333b");

        // ── Constant data ─────────────────────────────────────────────────────────

        private static readonly string[] LangFiles  = { "subtitles", "subtitles-fr", "subtitles-sv", "subtitles-es" };
        private static readonly string[] LangLabels = { "English", "Fran\u00E7ais", "Svenska", "Espa\u00F1ol" };

        // ── State ─────────────────────────────────────────────────────────────────

        private readonly List<SubtitleEntry> _scripts = new();
        private SubtitlePlayer _player;
        private SubtitleWidget _subWidget;

        private int   _scriptIndex = 0;
        private int   _langIndex   = 0;
        private bool  _isRunning   = false;
        private float _elapsedMs   = 0f;
        private float _totalMs     = 0f;
        private int   _maxLines    = 2;
        private int   _fontSize    = 16;
        private bool  _doubleSpeed = false;

        // ── UI references ─────────────────────────────────────────────────────────

        private Button   _btnStart, _btnStop, _btnReset, _btnSpeed;
        private Button   _btnLinesDec, _btnLinesInc;
        private Button   _btnFontDec, _btnFontInc;
        private Button   _btnScriptPrev, _btnScriptNext;
        private Button[] _langBtns = new Button[4];
        private TMP_Text _scriptLabel;
        private TMP_Text _statusText, _pageInfoText, _timeInfoText;
        private TMP_Text _linesCountText, _fontSizeText;
        private TMP_Text _speakerNameText;
        private RectTransform _progressFillRT;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureCamera();
            EnsureEventSystem();
            BuildUI();
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.AddComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private void Start()
        {
            LoadSubtitles(LangFiles[_langIndex]);

            _player = new SubtitlePlayer();
            _player.Initialize(_subWidget, _maxLines);
            _player.OnComplete += OnSubtitleComplete;

            SetRunning(false);
            RefreshScriptLabel();

            SetStatus(_scripts.Count == 0
                ? "No subtitle data found. Copy JSON files into Assets/Demo/Resources/."
                : "Select a subtitle entry and press Start.");

            if (_scripts.Count == 0 && _btnStart != null)
                _btnStart.interactable = false;
        }

        private void OnDestroy() => _player?.Stop();

        private void Update()
        {
            if (!_isRunning || _player == null) return;

            float mult  = _doubleSpeed ? 2f : 1f;
            float delta = Time.deltaTime * mult;
            _elapsedMs  = Mathf.Min(_elapsedMs + Time.deltaTime * 1000f * mult, _totalMs);
            _player.Tick(delta);
            UpdateProgress();
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void LoadSubtitles(string resourceName)
        {
            var asset = Resources.Load<TextAsset>(resourceName);
            if (asset == null)
            {
                Debug.LogWarning($"GameSubtitlesDemo: Resources/{resourceName} not found.");
                return;
            }

            string json = asset.text;
            if (json.Length > 0 && json[0] == '\uFEFF') json = json[1..]; // strip BOM

            // Unity's JsonUtility can't deserialise a bare JSON array; wrap it first
            string wrapped = "{\"items\":" + json + "}";
            SubtitleEntryList list;
            try   { list = JsonUtility.FromJson<SubtitleEntryList>(wrapped); }
            catch { Debug.LogWarning($"GameSubtitlesDemo: failed to parse {resourceName}.json"); return; }

            if (list?.items == null) return;

            _scripts.Clear();
            foreach (var r in list.items)
            {
                if (string.IsNullOrEmpty(r.subtitle)) continue;
                string clean = r.subtitle.Replace("\u00AD", "");
                _scripts.Add(new SubtitleEntry
                {
                    id       = r.id,
                    speaker  = r.speaker,
                    text     = r.subtitle,
                    duration = Mathf.Clamp(Mathf.Round(clean.Length / 14f), 3f, 18f),
                });
            }

            _scriptIndex = 0;
            RefreshScriptLabel();
        }

        // ── Controls ──────────────────────────────────────────────────────────────

        private void DoStart()
        {
            if (_player == null || _scripts.Count == 0) return;
            _player.Stop();

            if (_scriptIndex < 0 || _scriptIndex >= _scripts.Count) return;
            var s = _scripts[_scriptIndex];

            _totalMs   = s.duration * 1000f;
            _elapsedMs = 0f;
            if (_speakerNameText != null) _speakerNameText.text = s.speaker.ToUpper();

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
            if (_progressFillRT  != null) _progressFillRT.anchorMax = new Vector2(0f, 1f);
            if (_pageInfoText    != null) _pageInfoText.text = "";
            if (_timeInfoText    != null) _timeInfoText.text = "";
            if (_speakerNameText != null) _speakerNameText.text = "";
            SetStatus("Select a subtitle entry and press Start.");
        }

        private void OnSpeedToggle()
        {
            _doubleSpeed = !_doubleSpeed;
            var lbl = _btnSpeed?.GetComponentInChildren<TMP_Text>();
            if (lbl != null) lbl.text = _doubleSpeed ? "2x Speed" : "1x Speed";
        }

        private void OnLangSelected(int idx)
        {
            if (idx == _langIndex) return;
            _langIndex = idx;
            DoReset();
            LoadSubtitles(LangFiles[_langIndex]);
            RefreshLangButtons();
            SetStatus("Select a subtitle entry and press Start.");
        }

        private void OnScriptPrev()
        {
            if (_scripts.Count == 0) return;
            _scriptIndex = (_scriptIndex - 1 + _scripts.Count) % _scripts.Count;
            DoReset();
            RefreshScriptLabel();
        }

        private void OnScriptNext()
        {
            if (_scripts.Count == 0) return;
            _scriptIndex = (_scriptIndex + 1) % _scripts.Count;
            DoReset();
            RefreshScriptLabel();
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

        private void OnSubtitleComplete()
        {
            SetRunning(false);
            _elapsedMs = _totalMs;
            UpdateProgress();
            if (_speakerNameText != null) _speakerNameText.text = "";
            _player?.Stop();
            SetStatus("Finished.");
        }

        // ── State helpers ─────────────────────────────────────────────────────────

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
            if (_progressFillRT != null) _progressFillRT.anchorMax = new Vector2(pct, 1f);

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

        private void RefreshLangButtons()
        {
            for (int i = 0; i < _langBtns.Length; i++)
            {
                if (_langBtns[i] == null) continue;
                bool active = (i == _langIndex);
                var img = _langBtns[i].GetComponent<Image>();
                if (img != null) img.color = active ? ColGrayLit : ColGray;
                var lbl = _langBtns[i].GetComponentInChildren<TMP_Text>();
                if (lbl != null) lbl.color = active ? ColAccent : ColTextMain;
            }
        }

        private void RefreshScriptLabel()
        {
            if (_scriptLabel == null) return;

            if (_scripts.Count == 0)
            {
                _scriptLabel.text = "(no subtitles loaded)";
                if (_btnScriptPrev != null) _btnScriptPrev.interactable = false;
                if (_btnScriptNext != null) _btnScriptNext.interactable = false;
                return;
            }

            var s = _scripts[_scriptIndex];
            string preview = s.text.Replace("\u00AD", "");
            if (preview.Length > 38) preview = preview[..38] + "\u2026";
            _scriptLabel.text = $"[{s.id}] {s.speaker} \u2014 {preview}";

            bool multi = _scripts.Count > 1;
            if (_btnScriptPrev != null) _btnScriptPrev.interactable = multi;
            if (_btnScriptNext != null) _btnScriptNext.interactable = multi;
        }

        // ── UI construction ───────────────────────────────────────────────────────

        private void BuildUI()
        {
            // Canvas
            var canvasGo = new GameObject("DemoCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Full-screen dark background
            StretchFill(MakePanel(canvasGo.transform, ColBgDark, "BG"));

            // Centre column — fixed 540px wide, auto-height via ContentSizeFitter
            var col = new GameObject("Column");
            col.transform.SetParent(canvasGo.transform, false);
            var colRT = col.AddComponent<RectTransform>();
            colRT.anchorMin = colRT.anchorMax = new Vector2(0.5f, 0.5f);
            colRT.pivot     = new Vector2(0.5f, 0.5f);
            colRT.sizeDelta = new Vector2(540f, 0f);
            var colVL = col.AddComponent<VerticalLayoutGroup>();
            colVL.padding               = new RectOffset(0, 0, 16, 16);
            colVL.spacing               = 8f;
            colVL.childAlignment        = TextAnchor.UpperCenter;
            colVL.childForceExpandWidth = true;
            colVL.childForceExpandHeight = false;
            colVL.childControlWidth      = true;
            colVL.childControlHeight     = true;
            col.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Title
            var title = Label(col.transform, "GAME SUBTITLES \u2014 PLAYER DEMO", 11f, ColAccent);
            title.alignment = TextAlignmentOptions.Center;
            LE(title, minH: 18f);

            // ── 540×304 scene area ────────────────────────────────────────────────
            {
                var scene = new GameObject("Scene");
                scene.transform.SetParent(col.transform, false);
                var sceneRT = scene.AddComponent<RectTransform>();
                sceneRT.sizeDelta = new Vector2(540f, 304f);
                LE(scene, minW: 540f, minH: 304f, flexW: 0f, flexH: 0f);

                // Sky background
                StretchFill(MakePanel(scene.transform, ColBgScene, "Sky"));

                // Footer (speaker + subtitle bar) anchored to bottom edge
                var footer = new GameObject("Footer");
                footer.transform.SetParent(scene.transform, false);
                var fRT = footer.AddComponent<RectTransform>();
                fRT.anchorMin = new Vector2(0f, 0f);
                fRT.anchorMax = new Vector2(1f, 0f);
                fRT.pivot     = new Vector2(0.5f, 0f);
                fRT.sizeDelta = Vector2.zero;
                var fVL = footer.AddComponent<VerticalLayoutGroup>();
                fVL.childForceExpandWidth   = true;
                fVL.childForceExpandHeight  = false;
                fVL.childControlWidth       = true;
                fVL.childControlHeight      = true;
                footer.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Speaker name
                _speakerNameText = Label(footer.transform, "", 8.5f, ColAccent);
                _speakerNameText.alignment = TextAlignmentOptions.Center;
                LE(_speakerNameText, minH: 14f);

                // Dark semi-transparent subtitle bar
                var bar = MakePanel(footer.transform, ColBgSubBar, "SubBar");
                var barVL = bar.AddComponent<VerticalLayoutGroup>();
                barVL.padding               = new RectOffset(0, 0, 8, 12);
                barVL.childForceExpandWidth  = true;
                barVL.childForceExpandHeight = false;
                barVL.childControlWidth      = true;
                barVL.childControlHeight     = true;
                bar.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // SubtitleWidget renderer
                var swGo = new GameObject("SubtitleWidget");
                swGo.transform.SetParent(bar.transform, false);
                swGo.AddComponent<RectTransform>();
                _subWidget = swGo.AddComponent<SubtitleWidget>();
                _subWidget.FontSize               = _fontSize;
                _subWidget.TextColor              = Color.white;
                _subWidget.ContainerWidthOverride = 540f; // explicit until widget is on-screen
            }

            // ── Language buttons ──────────────────────────────────────────────────
            {
                var row = HRow(col.transform, "LangRow", 4f);
                LE(row, minH: 26f);
                for (int i = 0; i < 4; i++)
                {
                    int idx = i; // capture for lambda
                    _langBtns[i] = Btn(row.transform, LangLabels[i], ColGray, () => OnLangSelected(idx), 100f);
                }
            }

            // ── Script selector: ◀ label ▶ ───────────────────────────────────────
            {
                var row = HRow(col.transform, "ScriptRow", 4f);
                LE(row, minH: 26f);
                _btnScriptPrev = Btn(row.transform, "<", ColGray, OnScriptPrev, 28f);
                _scriptLabel   = Label(row.transform, "", 10f, ColTextMain);
                _scriptLabel.alignment     = TextAlignmentOptions.Center;
                _scriptLabel.overflowMode  = TextOverflowModes.Ellipsis;
                LE(_scriptLabel, minW: 400f, flexW: 1f);
                _btnScriptNext = Btn(row.transform, ">", ColGray, OnScriptNext, 28f);
            }

            // ── Playback buttons ──────────────────────────────────────────────────
            {
                var row = HRow(col.transform, "PlayRow", 6f);
                LE(row, minH: 26f);
                _btnStart = Btn(row.transform, "Start", ColGreen, DoStart,  90f);
                _btnStop  = Btn(row.transform, "Stop",  ColGray,  DoStop,   90f);
                _btnReset = Btn(row.transform, "Reset", ColGray,  DoReset,  90f);
            }

            // ── Options: speed | lines | font ─────────────────────────────────────
            {
                var row = HRow(col.transform, "OptRow", 6f);
                LE(row, minH: 26f);
                _btnSpeed = Btn(row.transform, "1x Speed", ColGray, OnSpeedToggle, 90f);
                HSpacer(row.transform, 12f);

                Label(row.transform, "Lines:", 11f, ColTextMain);
                _btnLinesDec    = Btn(row.transform, "-", ColGray, () => ChangeLine(-1), 28f);
                _linesCountText = Label(row.transform, "2", 11f, ColTextMain);
                LE(_linesCountText, minW: 20f);
                _linesCountText.alignment = TextAlignmentOptions.Center;
                _btnLinesInc    = Btn(row.transform, "+", ColGray, () => ChangeLine(+1), 28f);
                HSpacer(row.transform, 12f);

                Label(row.transform, "Font:", 11f, ColTextMain);
                _btnFontDec  = Btn(row.transform, "-", ColGray, () => ChangeFont(-2), 28f);
                _fontSizeText = Label(row.transform, "16px", 11f, ColTextMain);
                LE(_fontSizeText, minW: 34f);
                _fontSizeText.alignment = TextAlignmentOptions.Center;
                _btnFontInc  = Btn(row.transform, "+", ColGray, () => ChangeFont(+2), 28f);
            }

            // ── Progress bar (anchor-based fill — no sprite required) ────────────
            {
                var bg = MakePanel(col.transform, ColBgPanel, "ProgressBg");
                LE(bg, minH: 8f, flexW: 0f);

                // Fill: left-anchored child; move anchorMax.x to animate progress
                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(bg.transform, false);
                fillGo.AddComponent<Image>().color = ColAccent;
                _progressFillRT             = fillGo.GetComponent<RectTransform>();
                _progressFillRT.anchorMin   = Vector2.zero;
                _progressFillRT.anchorMax   = Vector2.zero; // starts empty
                _progressFillRT.offsetMin   = Vector2.zero;
                _progressFillRT.offsetMax   = Vector2.zero;
            }

            // ── Meta row: page info | time ────────────────────────────────────────
            {
                var row = HRow(col.transform, "MetaRow", 0f);
                LE(row, minH: 16f);
                _pageInfoText = Label(row.transform, "", 9f, ColMuted);
                LE(_pageInfoText, flexW: 1f);
                _timeInfoText = Label(row.transform, "", 9f, ColMuted);
                _timeInfoText.alignment = TextAlignmentOptions.Right;
                LE(_timeInfoText, flexW: 1f);
            }

            // ── Status line ───────────────────────────────────────────────────────
            {
                _statusText = Label(col.transform, "Loading\u2026", 10.5f, ColMuted);
                _statusText.alignment = TextAlignmentOptions.Center;
                LE(_statusText, minH: 20f);
            }

            RefreshLangButtons();
            UpdateLinesDisplay();
            UpdateFontDisplay();
        }

        // ── UI factory helpers ────────────────────────────────────────────────────

        private TMP_Text Label(Transform parent, string text, float size, Color color)
        {
            var go = new GameObject("Lbl");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text               = text;
            t.fontSize           = size;
            t.color              = color;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private Button Btn(Transform parent, string label, Color bg, Action onClick, float minW = 60f)
        {
            var go = new GameObject("Btn");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>().color = bg;

            var btn = go.AddComponent<Button>();
            var c = btn.colors;
            c.normalColor      = bg;
            c.highlightedColor = bg * 1.3f;
            c.pressedColor     = bg * 0.8f;
            c.disabledColor    = new Color(bg.r, bg.g, bg.b, 0.38f);
            c.fadeDuration     = 0.05f;
            btn.colors = c;
            btn.onClick.AddListener(() => onClick());

            // Button label (child fills the button rect)
            var lblGo = new GameObject("Lbl");
            lblGo.transform.SetParent(go.transform, false);
            var lblRT = lblGo.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(4f, 2f);
            lblRT.offsetMax = new Vector2(-4f, -2f);
            var t = lblGo.AddComponent<TextMeshProUGUI>();
            t.text               = label;
            t.fontSize           = 11f;
            t.color              = ColTextMain;
            t.alignment          = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;

            LE(go, minW: minW, minH: 24f);
            return btn;
        }

        private static GameObject MakePanel(Transform parent, Color color, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static GameObject HRow(Transform parent, string name, float spacing)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.spacing                = spacing;
            hl.childAlignment         = TextAnchor.MiddleCenter;
            hl.childForceExpandWidth  = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth      = true;
            hl.childControlHeight     = true;
            return go;
        }

        private static void HSpacer(Transform parent, float w)
        {
            var go = new GameObject("HSp");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            LE(go, minW: w, flexW: 0f);
        }

        private static void StretchFill(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void LE(GameObject go,
            float minW = -1f, float minH = -1f, float flexW = -1f, float flexH = -1f)
        {
            var e = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (minW  >= 0f) e.minWidth       = minW;
            if (minH  >= 0f) e.minHeight      = minH;
            if (flexW >= 0f) e.flexibleWidth  = flexW;
            if (flexH >= 0f) e.flexibleHeight = flexH;
        }

        private static void LE(Component c,
            float minW = -1f, float minH = -1f, float flexW = -1f, float flexH = -1f)
            => LE(c.gameObject, minW, minH, flexW, flexH);

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        // ── Data types ────────────────────────────────────────────────────────────

        [Serializable] private class RawEntry { public string id; public string speaker; public string subtitle; }
        [Serializable] private class SubtitleEntryList { public RawEntry[] items; }

        private class SubtitleEntry
        {
            public string id, speaker, text;
            public float  duration;
        }
    }
}
