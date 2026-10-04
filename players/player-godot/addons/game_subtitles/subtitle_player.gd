class_name GameSubtitlePlayer
extends RefCounted
## Manages paginated subtitle display driven by caller-supplied ticks.
##
## Create once for a given renderer and line count, then reuse across any number of
## subtitles by calling [method start] each time.
## [codeblock]
## var player := GameSubtitlePlayer.new()
## player.initialize($SubtitleWidget, 2)
## player.completed.connect(func(): player.stop())
## player.start("Hello world", 5.0)
## player.start("Hello world") # no audio: duration estimated from the text
## player.tick(delta) # called from _process()
## [/codeblock]
## The renderer can be any object with these four methods, such as [GameSubtitleWidget]:
## [codeblock]
## func measure_line_width(text: String, use_character_name_font: bool = false) -> float
## func get_container_width() -> float
## func render(lines: PackedStringArray, character_context: GameSubtitleCharacterContext) -> void
## func clear() -> void
## [/codeblock]

## Emitted when all pages have been displayed and the subtitle has finished. The last page
## stays on screen; call [method stop] to clear it.
signal completed

const _RENDERER_METHODS: Array[StringName] = [
	&"measure_line_width", &"get_container_width", &"render", &"clear",
]

## Lines per page. A change takes effect on the next [method start].
var max_lines: int = 2

## Number of pages in the current subtitle layout. Valid after [method start]; 0 before.
var page_count: int:
	get:
		return _pages.size()

var _renderer: Object
var _pages: Array[PackedStringArray] = []
var _timings := PackedFloat64Array()
var _page_index := 0
var _elapsed := 0.0
var _running := false
var _done := false
var _character_name := ""
var _character_name_color: Variant = null
var _line_color: Variant = null


## Estimates how long a subtitle should stay on screen when no duration is known.
## [br][br]
## Counts characters (Unicode code points, ignoring U+00AD soft hyphens), divides by the
## reading rate, and clamps the result. Counting characters rather than words means CJK
## text, which has no spaces, still gets a sensible estimate; pass a lower
## [param chars_per_second] for languages that are read more slowly per character.
## Values of [param chars_per_second] of 0 or less fall back to 14.
## [br][br]
## The JS, Unity, and Unreal players implement the same rule with the same defaults.
static func estimate_duration(text: String, chars_per_second: float = 14.0,
		min_seconds: float = 1.5, max_seconds: float = 18.0) -> float:
	# Godot strings hold one code point per character, so length() already counts an
	# astral character once.
	var count := text.length() - text.count("­")
	var rate := chars_per_second if chars_per_second > 0.0 else 14.0
	return minf(maxf(count / rate, min_seconds), max_seconds)


## Binds the renderer and sets the initial lines-per-page value.
## Call this once before the first [method start].
func initialize(renderer: Object, p_max_lines: int = 2) -> void:
	for method in _RENDERER_METHODS:
		if renderer != null and not renderer.has_method(method):
			push_error("GameSubtitlePlayer: the renderer has no %s() method." % method)
	_renderer = renderer
	max_lines = maxi(1, p_max_lines)


## Loads a subtitle, lays out the text, and renders page 0 immediately.
## Calling this while another subtitle is playing stops it first.
## [br][br]
## [param text] may contain U+00AD soft hyphens. When [param duration] is omitted or 0 or
## less, [method estimate_duration] is used instead.
## [br][br]
## When [param character_name] is not empty, "Name: " is prepended to the first line of
## every page, and the text is laid out with space reserved for it.
## [param character_name_color] and [param line_color] are each a [Color], or
## [code]null[/code] to use the renderer's default text colour.
func start(text: String, duration: float = 0.0, character_name: String = "",
		character_name_color: Variant = null, line_color: Variant = null) -> void:
	_running = false
	if _has_renderer():
		_renderer.clear()

	_character_name = character_name
	_character_name_color = character_name_color
	_line_color = line_color
	_elapsed = 0.0
	_page_index = 0
	_done = false

	if not _has_renderer():
		return

	var container_width: float = _renderer.get_container_width()
	# Ceil so subpixel measurement differences never tip the rendered line over the edge.
	# Measure the prefix with the character-name font so the body always fits beside it.
	var first_line_indent := 0.0
	if not character_name.is_empty():
		first_line_indent = ceilf(_renderer.measure_line_width(character_name + ": ", true))

	var measure := func(t: String) -> float: return _renderer.measure_line_width(t, false)
	_pages = GameSubtitleTextLayout.wrap_and_paginate(text, measure, container_width,
			maxi(1, max_lines), first_line_indent)
	_timings = GameSubtitleTextLayout.allocate_timings(_pages,
			duration if duration > 0.0 else estimate_duration(text))

	_running = true
	_render_current()


## Advances the internal clock. Call once per frame, for example from [code]_process()[/code].
## Advances pages automatically; emits [signal completed] and stops when the last page expires.
func tick(delta: float) -> void:
	if not _running or _done:
		return

	_elapsed += delta

	while _elapsed >= _timings[_page_index]:
		_elapsed -= _timings[_page_index]
		_page_index += 1

		if _page_index >= _pages.size():
			_done = true
			_running = false
			# Clearing is left to the caller, who can call stop() after completed, so the
			# final page can stay visible if wanted.
			completed.emit()
			return

		_render_current()


## Stops playback and clears the renderer. Does not emit [signal completed].
func stop() -> void:
	_running = false
	if _has_renderer():
		_renderer.clear()


## Clears the renderer and resets to the state before [method start].
## Call [method start] again to replay from the beginning.
func reset() -> void:
	_running = false
	_done = false
	_page_index = 0
	_elapsed = 0.0
	_pages.clear()
	_timings.clear()
	if _has_renderer():
		_renderer.clear()


func _has_renderer() -> bool:
	return is_instance_valid(_renderer)


func _render_current() -> void:
	if not _has_renderer() or _page_index >= _pages.size():
		return

	var ctx: GameSubtitleCharacterContext = null
	if not _character_name.is_empty() or _line_color != null:
		ctx = GameSubtitleCharacterContext.new(_character_name, _character_name_color, _line_color)

	# Pad to max_lines so the renderer always receives a full-height page.
	var page := _pages[_page_index].duplicate()
	while page.size() < max_lines:
		page.append(" ")

	_renderer.render(page, ctx)
