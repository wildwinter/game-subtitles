extends Control
## Demo scene for the Game Subtitles addon, mirroring the JS, Unity, and Unreal demos.
##
## The whole UI is built in code, so the scene is just this script on a full-screen Control.
## Subtitle data is read from demo/data/, the same files the other demos use.

const LANG_FILES := ["subtitles", "subtitles-fr", "subtitles-sv", "subtitles-es"]
const LANG_LABELS := ["English", "Français", "Svenska", "Español"]

const COL_BG_DARK := Color("#0d1117")
const COL_BG_SCENE := Color("#160a25")
const COL_BG_SUB_BAR := Color(0, 0, 0, 0.75)
const COL_BG_PANEL := Color("#161b22")
const COL_TEXT := Color("#c9d1d9")
const COL_MUTED := Color("#8b949e")
const COL_ACCENT := Color("#f0cc88")
const COL_GREEN := Color("#238636")
const COL_GRAY := Color("#21262d")
const COL_GRAY_LIT := Color("#2d333b")

# Character-name colour swatches; amber is the default.
const CHAR_COLORS := [Color("#f0cc88"), Color.WHITE, Color("#00d4ff"), Color("#ff6b9d"), Color("#4ade80")]

var _scripts: Array[Dictionary] = []
var _player := GameSubtitlePlayer.new()
var _widget: GameSubtitleWidget

var _script_index := 0
var _lang_index := 0
var _running := false
var _elapsed := 0.0
var _total := 0.0
var _max_lines := 2
var _font_size := 16
var _double_speed := false
var _char_name_enabled := true
var _char_color_index := 0

var _btn_start: Button
var _btn_stop: Button
var _btn_speed: Button
var _btn_script_prev: Button
var _btn_script_next: Button
var _btn_lines_dec: Button
var _btn_lines_inc: Button
var _btn_font_dec: Button
var _btn_font_inc: Button
var _btn_char_toggle: Button
var _lang_buttons: Array[Button] = []
var _swatches: Array[Button] = []
var _script_label: Label
var _speaker_label: Label
var _status_label: Label
var _page_label: Label
var _time_label: Label
var _lines_label: Label
var _font_label: Label
var _progress: ProgressBar


func _ready() -> void:
	_build_ui()
	_player.initialize(_widget, _max_lines)
	_player.completed.connect(_on_subtitle_completed)
	_load_subtitles(LANG_FILES[_lang_index])
	_refresh_all()
	_set_running(false)
	_set_status("Select a subtitle entry and press Start." if not _scripts.is_empty()
			else "No subtitle data found in demo/data/.")


func _process(delta: float) -> void:
	if not _running:
		return
	var scaled := delta * (2.0 if _double_speed else 1.0)
	_elapsed = minf(_elapsed + scaled, _total)
	_player.tick(scaled)
	_update_progress()


# ── Data ─────────────────────────────────────────────────────────────────────

func _load_subtitles(file_name: String) -> void:
	_scripts.clear()
	_script_index = 0
	var path := "res://demo/data/%s.json" % file_name
	var json := FileAccess.get_file_as_string(path).trim_prefix("﻿")
	var data: Variant = JSON.parse_string(json)
	if not data is Array:
		push_warning("Game Subtitles demo: could not read %s" % path)
		return
	for entry: Variant in data:
		if entry is Dictionary and not str(entry.get("subtitle", "")).is_empty():
			_scripts.append({
				id = str(entry.get("id", "")),
				speaker = str(entry.get("speaker", "")),
				text = str(entry.subtitle),
			})


# ── Controls ─────────────────────────────────────────────────────────────────

func _do_start() -> void:
	if _scripts.is_empty():
		return
	var s := _scripts[_script_index]
	_total = GameSubtitlePlayer.estimate_duration(s.text)
	_elapsed = 0.0

	var char_name: String = s.speaker if _char_name_enabled else ""
	# Show the separate speaker label only when the inline name is off.
	_speaker_label.text = "" if _char_name_enabled else s.speaker.to_upper()

	_player.max_lines = _max_lines
	_player.start(s.text, _total, char_name, CHAR_COLORS[_char_color_index] if not char_name.is_empty() else null)

	_set_running(true)
	_set_status("Playing: [%s] %s" % [s.id, s.speaker])
	_update_progress()


func _do_stop() -> void:
	_player.stop()
	_set_running(false)
	_set_status("Stopped.")


func _do_reset() -> void:
	_player.reset()
	_set_running(false)
	_elapsed = 0.0
	_total = 0.0
	_speaker_label.text = ""
	_update_progress()
	_set_status("Select a subtitle entry and press Start.")


func _on_subtitle_completed() -> void:
	_set_running(false)
	_elapsed = _total
	_update_progress()
	_speaker_label.text = ""
	_player.stop()
	_set_status("Finished.")


func _select_language(index: int) -> void:
	if index == _lang_index:
		return
	_lang_index = index
	_do_reset()
	_load_subtitles(LANG_FILES[index])
	_refresh_all()


func _step_script(step: int) -> void:
	if _scripts.is_empty():
		return
	_script_index = posmod(_script_index + step, _scripts.size())
	_do_reset()
	_refresh_script_label()


func _toggle_speed() -> void:
	_double_speed = not _double_speed
	_btn_speed.text = "2x Speed" if _double_speed else "1x Speed"


func _change_lines(step: int) -> void:
	_max_lines = clampi(_max_lines + step, 1, 5)
	_refresh_lines()
	if _running:
		_do_start()


func _change_font(step: int) -> void:
	_font_size = clampi(_font_size + step, 10, 32)
	_widget.subtitle_font_size = _font_size
	_widget.character_name_font_size = _font_size + 2
	_refresh_font()
	if _running:
		_do_start()


func _toggle_char_name() -> void:
	_char_name_enabled = not _char_name_enabled
	_refresh_char_name()
	if _running:
		_do_start()


func _select_char_color(index: int) -> void:
	_char_color_index = index
	_refresh_char_name()
	if _running:
		_do_start()


# ── Display ──────────────────────────────────────────────────────────────────

func _set_running(running: bool) -> void:
	_running = running
	_btn_start.disabled = running or _scripts.is_empty()
	_btn_stop.disabled = not running


func _set_status(message: String) -> void:
	_status_label.text = message


func _update_progress() -> void:
	_progress.value = _elapsed / _total if _total > 0.0 else 0.0
	_page_label.text = "%d pages" % _player.page_count if _player.page_count > 1 else ""
	_time_label.text = "%.1f s / %.1f s" % [_elapsed, _total] if _total > 0.0 else ""


func _refresh_all() -> void:
	for i in _lang_buttons.size():
		_style_button(_lang_buttons[i], COL_GRAY_LIT if i == _lang_index else COL_GRAY)
		_lang_buttons[i].add_theme_color_override(&"font_color", COL_ACCENT if i == _lang_index else COL_TEXT)
	_refresh_script_label()
	_refresh_lines()
	_refresh_font()
	_refresh_char_name()


func _refresh_script_label() -> void:
	if _scripts.is_empty():
		_script_label.text = "(no subtitles loaded)"
	else:
		var s := _scripts[_script_index]
		var preview: String = s.text.replace("­", "")
		if preview.length() > 38:
			preview = preview.left(38) + "…"
		_script_label.text = "[%s] %s: %s" % [s.id, s.speaker, preview]
	_btn_script_prev.disabled = _scripts.size() < 2
	_btn_script_next.disabled = _scripts.size() < 2
	_btn_start.disabled = _running or _scripts.is_empty()


func _refresh_lines() -> void:
	_lines_label.text = str(_max_lines)
	_btn_lines_dec.disabled = _max_lines <= 1
	_btn_lines_inc.disabled = _max_lines >= 5


func _refresh_font() -> void:
	_font_label.text = "%dpx" % _font_size
	_btn_font_dec.disabled = _font_size <= 10
	_btn_font_inc.disabled = _font_size >= 32


func _refresh_char_name() -> void:
	_btn_char_toggle.text = "ON" if _char_name_enabled else "OFF"
	_style_button(_btn_char_toggle, COL_GREEN if _char_name_enabled else COL_GRAY)
	for i in _swatches.size():
		_swatches[i].text = "X" if i == _char_color_index else ""


# ── UI construction ──────────────────────────────────────────────────────────

func _build_ui() -> void:
	var bg := ColorRect.new()
	bg.color = COL_BG_DARK
	bg.set_anchors_and_offsets_preset(PRESET_FULL_RECT)
	add_child(bg)

	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(PRESET_FULL_RECT)
	add_child(center)

	var col := VBoxContainer.new()
	col.custom_minimum_size.x = 540
	col.add_theme_constant_override(&"separation", 8)
	center.add_child(col)

	var title := _label(col, "GAME SUBTITLES: PLAYER DEMO", 13, COL_ACCENT)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	# Scene area, with the speaker label and subtitle bar along its bottom edge.
	var scene := Panel.new()
	scene.custom_minimum_size = Vector2(540, 304)
	scene.add_theme_stylebox_override(&"panel", _flat(COL_BG_SCENE))
	col.add_child(scene)

	var footer := VBoxContainer.new()
	footer.set_anchors_and_offsets_preset(PRESET_BOTTOM_WIDE)
	footer.grow_vertical = GROW_DIRECTION_BEGIN
	footer.add_theme_constant_override(&"separation", 2)
	scene.add_child(footer)

	_speaker_label = _label(footer, "", 11, COL_ACCENT)
	_speaker_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER

	var bar := PanelContainer.new()
	var bar_style := _flat(COL_BG_SUB_BAR)
	bar_style.content_margin_left = 12
	bar_style.content_margin_right = 12
	bar_style.content_margin_top = 8
	bar_style.content_margin_bottom = 12
	bar.add_theme_stylebox_override(&"panel", bar_style)
	footer.add_child(bar)

	_widget = GameSubtitleWidget.new()
	_widget.subtitle_font_size = _font_size
	# Give the character name a slightly larger size to show the separate styling.
	_widget.character_name_font_size = _font_size + 2
	bar.add_child(_widget)

	var lang_row := _row(col, 4)
	for i in LANG_LABELS.size():
		var b := _button(lang_row, LANG_LABELS[i], _select_language.bind(i), 100)
		_lang_buttons.append(b)

	var script_row := _row(col, 4)
	_btn_script_prev = _button(script_row, "<", _step_script.bind(-1), 28)
	_script_label = _label(script_row, "", 12, COL_TEXT)
	_script_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_script_label.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
	_script_label.clip_text = true
	_script_label.size_flags_horizontal = SIZE_EXPAND_FILL
	_btn_script_next = _button(script_row, ">", _step_script.bind(1), 28)

	var play_row := _row(col, 6)
	_btn_start = _button(play_row, "Start", _do_start, 90)
	_style_button(_btn_start, COL_GREEN)
	_btn_stop = _button(play_row, "Stop", _do_stop, 90)
	_button(play_row, "Reset", _do_reset, 90)

	var opt_row := _row(col, 6)
	_btn_speed = _button(opt_row, "1x Speed", _toggle_speed, 90)
	_spacer(opt_row, 12)
	_label(opt_row, "Lines:", 13, COL_TEXT)
	_btn_lines_dec = _button(opt_row, "-", _change_lines.bind(-1), 28)
	_lines_label = _label(opt_row, "", 13, COL_TEXT)
	_lines_label.custom_minimum_size.x = 20
	_lines_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_btn_lines_inc = _button(opt_row, "+", _change_lines.bind(1), 28)
	_spacer(opt_row, 12)
	_label(opt_row, "Font:", 13, COL_TEXT)
	_btn_font_dec = _button(opt_row, "-", _change_font.bind(-2), 28)
	_font_label = _label(opt_row, "", 13, COL_TEXT)
	_font_label.custom_minimum_size.x = 40
	_font_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_btn_font_inc = _button(opt_row, "+", _change_font.bind(2), 28)

	var char_row := _row(col, 6)
	_label(char_row, "Char name:", 13, COL_TEXT)
	_btn_char_toggle = _button(char_row, "ON", _toggle_char_name, 46)
	_spacer(char_row, 10)
	_label(char_row, "Colour:", 13, COL_TEXT)
	for i in CHAR_COLORS.size():
		var swatch := _button(char_row, "", _select_char_color.bind(i), 26)
		_style_button(swatch, CHAR_COLORS[i])
		for state in [&"font_color", &"font_hover_color", &"font_pressed_color", &"font_focus_color"]:
			swatch.add_theme_color_override(state, Color.BLACK)
		_swatches.append(swatch)

	_progress = ProgressBar.new()
	_progress.max_value = 1.0
	_progress.show_percentage = false
	_progress.custom_minimum_size.y = 8
	_progress.add_theme_stylebox_override(&"background", _flat(COL_BG_PANEL))
	_progress.add_theme_stylebox_override(&"fill", _flat(COL_ACCENT))
	col.add_child(_progress)

	var meta_row := _row(col, 0)
	_page_label = _label(meta_row, "", 11, COL_MUTED)
	_page_label.size_flags_horizontal = SIZE_EXPAND_FILL
	_time_label = _label(meta_row, "", 11, COL_MUTED)
	_time_label.size_flags_horizontal = SIZE_EXPAND_FILL
	_time_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT

	_status_label = _label(col, "", 12, COL_MUTED)
	_status_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER


func _row(parent: Control, separation: int) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.add_theme_constant_override(&"separation", separation)
	parent.add_child(row)
	return row


func _label(parent: Control, text: String, font_size: int, color: Color) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override(&"font_size", font_size)
	label.add_theme_color_override(&"font_color", color)
	parent.add_child(label)
	return label


func _button(parent: Control, text: String, on_pressed: Callable, min_width: float) -> Button:
	var button := Button.new()
	button.text = text
	button.custom_minimum_size = Vector2(min_width, 26)
	button.focus_mode = FOCUS_NONE
	button.add_theme_font_size_override(&"font_size", 13)
	button.pressed.connect(on_pressed)
	_style_button(button, COL_GRAY)
	parent.add_child(button)
	return button


func _spacer(parent: Control, width: float) -> void:
	var spacer := Control.new()
	spacer.custom_minimum_size.x = width
	parent.add_child(spacer)


func _style_button(button: Button, color: Color) -> void:
	button.add_theme_stylebox_override(&"normal", _flat(color))
	button.add_theme_stylebox_override(&"hover", _flat(color.lightened(0.15)))
	button.add_theme_stylebox_override(&"pressed", _flat(color.darkened(0.2)))
	button.add_theme_stylebox_override(&"disabled", _flat(Color(color, 0.38)))


func _flat(color: Color) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(3)
	return style
