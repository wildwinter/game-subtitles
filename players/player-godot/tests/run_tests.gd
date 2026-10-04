extends SceneTree
## Headless tests for the Game Subtitles addon. Run with `npm test` in players/player-godot,
## or directly:
##   godot --headless --path players/player-godot --import
##   godot --headless --path players/player-godot -s res://tests/run_tests.gd
##
## The layout and player cases are shared with the JS (TextLayout.test.js,
## SubtitlePlayer.test.js), Unity (TextLayoutTests.cs, SubtitlePlayerTests.cs), and Unreal
## (GameSubtitleTextLayoutTest.cpp, GameSubtitlePlayerTest.cpp) players, and the duration
## table with their estimateDuration.test.js, EstimateDurationTests.cs, and
## GameSubtitleEstimateDurationTest.cpp. Keep all four in step.

const LINE := "Get down, now! They have seen us." # 33 characters
const SHY := "­"
const ELLIPSIS := "…"
const W := 100.0 # 10 monospace characters; the ellipsis takes 1, so a last line gets 90 px

var _passed := 0
var _failed := 0
var _test := ""


func _initialize() -> void:
	for method in get_method_list():
		if method.name.begins_with("test_"):
			_test = method.name
			await call(method.name)
	print("\n%d passed, %d failed" % [_passed, _failed])
	if _failed == 0:
		print("ALL TESTS PASSED")
	quit(1 if _failed > 0 else 0)


# ── Helpers ──────────────────────────────────────────────────────────────────

func _check(ok: bool, what: String) -> void:
	if ok:
		_passed += 1
	else:
		_failed += 1
		printerr("FAIL %s: %s" % [_test, what])


func _eq(actual: Variant, expected: Variant, what: String = "") -> void:
	_check(actual == expected, "%s expected %s, got %s" % [what, var_to_str(expected), var_to_str(actual)])


func _near(actual: float, expected: float, what: String = "") -> void:
	_check(absf(actual - expected) < 1e-4, "%s expected %f, got %f" % [what, expected, actual])


static func _mono(text: String) -> float:
	return text.length() * 10.0


func _wrap(text: String, max_lines: int) -> Array:
	var pages := GameSubtitleTextLayout.wrap_and_paginate(text, _mono, W, max_lines)
	var out := []
	for page in pages:
		out.append(Array(page))
	return out


func _timings(pages: Array, total: float) -> PackedFloat64Array:
	var typed: Array[PackedStringArray] = []
	for page in pages:
		typed.append(PackedStringArray(page))
	return GameSubtitleTextLayout.allocate_timings(typed, total)


# A renderer with a 100 px container and 10 px monospace characters, which records calls.
class FakeRenderer:
	extends RefCounted
	var width := 100.0
	var rendered: Array = []
	var contexts: Array = []
	var measured: Array = []
	var cleared := 0

	func measure_line_width(text: String, use_character_name_font: bool = false) -> float:
		measured.append([text, use_character_name_font])
		return text.length() * 10.0

	func get_container_width() -> float:
		return width

	func render(lines: PackedStringArray, character_context: GameSubtitleCharacterContext = null) -> void:
		rendered.append(Array(lines))
		contexts.append(character_context)

	func clear() -> void:
		cleared += 1


class CompletionCounter:
	extends RefCounted
	var count := 0

	func on_completed() -> void:
		count += 1


func _player(renderer: FakeRenderer, max_lines: int, counter: CompletionCounter = null) -> GameSubtitlePlayer:
	var player := GameSubtitlePlayer.new()
	player.initialize(renderer, max_lines)
	if counter != null:
		player.completed.connect(counter.on_completed)
	return player


# ── estimate_duration ────────────────────────────────────────────────────────

func test_estimate_duration_shared_table() -> void:
	# label, text, chars_per_second, min_seconds, max_seconds, expected seconds
	var cases := [
		["empty text clamps to the floor", "", 14.0, 1.5, 18.0, 1.5],
		["one-word bark clamps to the floor", "No!", 14.0, 1.5, 18.0, 1.5],
		["plain line is chars / 14", LINE, 14.0, 1.5, 18.0, 33.0 / 14.0],
		["soft hyphens are not counted", "Ex­tra­or­di­nary, isn’t it?", 14.0, 1.5, 18.0, 24.0 / 14.0],
		["CJK counts characters, not words", "今日はとても良い天気ですね。散歩に行きましょうか。", 14.0, 1.5, 18.0, 25.0 / 14.0],
		["astral character counts once", "\U020BB7野家で牛丼を食べました。", 4.0, 1.5, 18.0, 13.0 / 4.0],
		["long text clamps to the ceiling", "x".repeat(300), 14.0, 1.5, 18.0, 18.0],
		["min_seconds override", "Hi", 14.0, 0.5, 18.0, 0.5],
		["max_seconds override", LINE, 14.0, 1.5, 2.0, 2.0],
		["non-positive rate falls back to 14", LINE, 0.0, 1.5, 18.0, 33.0 / 14.0],
	]
	for c in cases:
		_near(GameSubtitlePlayer.estimate_duration(c[1], c[2], c[3], c[4]), c[5], c[0])


func test_estimate_duration_defaults_match_shared_rule() -> void:
	_near(GameSubtitlePlayer.estimate_duration(LINE), 33.0 / 14.0)
	_near(GameSubtitlePlayer.estimate_duration(""), 1.5)


func test_start_uses_estimate_when_duration_is_not_positive() -> void:
	# LINE estimates to 33 / 14, about 2.357 s.
	for duration in [0.0, -1.0, NAN]:
		var counter := CompletionCounter.new()
		var player := _player(FakeRenderer.new(), 2, counter)
		player.start(LINE, duration)
		player.tick(2.3)
		_eq(counter.count, 0, "duration %s, before the estimate:" % duration)
		player.tick(0.1)
		_eq(counter.count, 1, "duration %s, after the estimate:" % duration)


func test_start_uses_estimate_when_duration_is_omitted() -> void:
	var counter := CompletionCounter.new()
	var player := _player(FakeRenderer.new(), 2, counter)
	player.start(LINE)
	player.tick(2.3)
	_eq(counter.count, 0)
	player.tick(0.1)
	_eq(counter.count, 1)


# ── wrap_and_paginate ────────────────────────────────────────────────────────

func test_wrap_empty_and_whitespace_text_give_one_empty_page() -> void:
	_eq(_wrap("", 2), [[]], "empty:")
	_eq(_wrap("   ", 2), [[]], "whitespace:")


func test_wrap_short_word_fits_on_one_page() -> void:
	_eq(_wrap("hello", 2), [["hello"]])


func test_wrap_two_words_that_do_not_fit_together() -> void:
	_eq(_wrap("hello world!", 2), [["hello", "world!"]])


func test_wrap_keeps_two_words_together_when_they_fit() -> void:
	_eq(_wrap("hi bye", 2), [["hi bye"]])


func test_wrap_splits_on_any_whitespace() -> void:
	_eq(_wrap("hi\tbye\n", 2), [["hi bye"]])


func test_wrap_strips_soft_hyphens_when_word_fits_whole() -> void:
	_eq(_wrap("inter" + SHY + "net", 2), [["internet"]])


func test_wrap_breaks_at_soft_hyphen_when_word_overflows() -> void:
	_eq(_wrap("inter" + SHY + "national", 2), [["inter-", "national"]])


func test_wrap_picks_longest_syllable_prefix_that_fits() -> void:
	var pages := _wrap(SHY.join(["in", "ter", "na", "tion"]), 2)
	_eq(pages[0][0], "interna-")
	_eq(pages[0][1], "tion")


func test_wrap_forces_character_break_for_overlong_word() -> void:
	var pages := _wrap("abcdefghijklmno", 2)
	_eq(pages[0][0], "abcdefghij")
	_eq(pages[0][1], "klmno")


func test_wrap_multiple_words_across_lines() -> void:
	_eq(_wrap("aaaa bbbb cccc dddd", 2), [["aaaa bbbb", "cccc dddd"]])


func test_wrap_appends_ellipsis_to_non_final_pages() -> void:
	_eq(_wrap("aaaaa bbbbb", 1), [["aaaaa" + ELLIPSIS], ["bbbbb"]])
	var pages := _wrap("aaaaa bbbbb ccccc", 1)
	_eq(pages[pages.size() - 1], ["ccccc"], "final page:")


func test_wrap_appends_ellipsis_to_all_non_final_pages_of_three() -> void:
	var pages := _wrap("aaaaaa bbbbbb cccccc", 1)
	_eq(pages.size(), 3)
	_check(pages[0][0].ends_with(ELLIPSIS), "page 0 ends with an ellipsis")
	_check(pages[1][0].ends_with(ELLIPSIS), "page 1 ends with an ellipsis")
	_check(not pages[2][0].ends_with(ELLIPSIS), "page 2 has no ellipsis")


func test_wrap_never_puts_word_fragment_on_last_line_of_non_final_page() -> void:
	var pages := _wrap("hello inter" + SHY + "national", 2)
	_eq(pages[0], ["hello" + ELLIPSIS])
	_eq(pages[1][0], "inter-")
	_eq(pages[1][1], "national")


func test_wrap_preserves_all_words_across_pages() -> void:
	var lines := PackedStringArray()
	for page in _wrap("one two three four five six seven eight nine ten", 1):
		lines.append_array(PackedStringArray(page))
	var words := " ".join(lines).replace(ELLIPSIS, "").split(" ", false)
	_check(words.has("one"), "contains 'one'")
	_check(words.has("ten"), "contains 'ten'")


func test_wrap_no_line_contains_a_raw_soft_hyphen() -> void:
	var text := SHY.join(["in", "ter", "na", "tion", "al", "i", "za", "tion"]) + " is com" + SHY + "plex"
	for page in _wrap(text, 2):
		for line in page:
			_check(not line.contains(SHY), "raw soft hyphen in '%s'" % line)


func test_wrap_rejoins_trailing_fragment_when_whole_word_fits() -> void:
	# The 10-character word is split as "ccccc-" on line 0. Its tail is alone on the last
	# line, which has no ellipsis on the final page, so the whole word fits there.
	_eq(_wrap("aa ccccc" + SHY + "ccccc", 2), [["aa", "cccccccccc"]])


func test_wrap_first_line_indent_narrows_only_line_zero() -> void:
	var pages := GameSubtitleTextLayout.wrap_and_paginate("aaaa bbbb cccc", _mono, W, 2, 30.0)
	_eq(Array(pages[0]), ["aaaa", "bbbb cccc"])


# ── allocate_timings ─────────────────────────────────────────────────────────

func test_timings_equal_pages_get_equal_time() -> void:
	var t := _timings([["abc"], ["def"]], 4.0)
	_near(t[0], 2.0)
	_near(t[1], 2.0)


func test_timings_ignore_the_ellipsis() -> void:
	var t := _timings([["abc" + ELLIPSIS], ["def"]], 4.0)
	_near(t[0], 2.0)
	_near(t[1], 2.0)


func test_timings_are_proportional_to_character_count() -> void:
	var t := _timings([["abc"], ["de"]], 5.0)
	_near(t[0], 3.0)
	_near(t[1], 2.0)


func test_timings_ignore_whitespace() -> void:
	var t := _timings([["a b"], ["cd"]], 4.0)
	_near(t[0], 2.0)
	_near(t[1], 2.0)


func test_timings_single_page_gets_everything() -> void:
	_near(_timings([["hello world"]], 3.0)[0], 3.0)


func test_timings_empty_page_counts_as_one() -> void:
	_near(_timings([[""]], 2.0)[0], 2.0)


# ── GameSubtitlePlayer ───────────────────────────────────────────────────────

func test_player_renders_page_zero_on_start_padded_to_max_lines() -> void:
	var r := FakeRenderer.new()
	_player(r, 2).start("hello", 4.0)
	_eq(r.rendered, [["hello", " "]])


func test_player_advances_page_when_its_time_is_up() -> void:
	var r := FakeRenderer.new()
	var player := _player(r, 1)
	player.start("aaaaa bbbbb", 4.0)
	_eq(r.rendered.size(), 1)
	player.tick(2.1) # the first page gets 2 s; its ellipsis is not timed
	_eq(r.rendered.size(), 2)
	_eq(r.rendered[1], ["bbbbb"])


func test_player_emits_completed_after_last_page_and_keeps_it_visible() -> void:
	var r := FakeRenderer.new()
	var counter := CompletionCounter.new()
	var player := _player(r, 2, counter)
	player.start("hi", 2.0)
	var cleared_before := r.cleared
	player.tick(2.5)
	_eq(counter.count, 1)
	_eq(r.cleared, cleared_before, "renderer cleared on completion:")
	player.tick(5.0)
	_eq(counter.count, 1, "completed emitted again:")


func test_player_stop_prevents_completion_and_clears() -> void:
	var r := FakeRenderer.new()
	var counter := CompletionCounter.new()
	var player := _player(r, 2, counter)
	player.start("hi", 5.0)
	var cleared_before := r.cleared
	player.stop()
	player.tick(10.0)
	_eq(counter.count, 0)
	_eq(r.cleared, cleared_before + 1)


func test_player_reset_clears_and_allows_replay() -> void:
	var r := FakeRenderer.new()
	var player := _player(r, 2)
	player.start("hello", 2.0)
	player.reset()
	_eq(player.page_count, 0)
	player.start("hello", 2.0)
	_eq(r.rendered.size(), 2)


func test_player_tick_before_start_does_nothing() -> void:
	var r := FakeRenderer.new()
	_player(r, 2).tick(5.0)
	_eq(r.rendered.size(), 0)


func test_player_large_tick_skips_to_completion() -> void:
	var counter := CompletionCounter.new()
	var player := _player(FakeRenderer.new(), 1, counter)
	player.start("aaaaaa bbbbbb cccccc", 3.0)
	_eq(player.page_count, 3)
	player.tick(10.0)
	_eq(counter.count, 1)


func test_player_lays_out_preprocessor_output() -> void:
	# Entry 1 of the JS player's fixtures/processed.json.
	var text := "In­ter­na­tion­al­i­za­tion is ex­traor­di­nar­i­ly dif­fi­cult."
	var r := FakeRenderer.new()
	var player := _player(r, 2)
	player.start(text, 6.0)
	_check(player.page_count > 1, "long text needs more than one page")
	for line in r.rendered[0]:
		_check(not line.contains(SHY), "raw soft hyphen in '%s'" % line)


func test_player_passes_character_context_on_every_page() -> void:
	var r := FakeRenderer.new()
	var player := _player(r, 1)
	# "V: " takes 30 px, so line 0 has 100 - 10 - 30 = 60 px: two pages.
	player.start("aaaaa bbbbb", 4.0, "V", Color.MAGENTA)
	player.tick(2.1)
	_eq(r.contexts.size(), 2)
	for ctx in r.contexts:
		_eq(ctx.name, "V")
		_eq(ctx.color, Color.MAGENTA)
		_eq(ctx.line_color, null)


func test_player_passes_null_context_without_name_or_line_color() -> void:
	var r := FakeRenderer.new()
	_player(r, 2).start("hello", 2.0)
	_eq(r.contexts[0], null)


func test_player_passes_context_for_line_color_alone() -> void:
	var r := FakeRenderer.new()
	_player(r, 2).start("hello", 2.0, "", null, Color.RED)
	_eq(r.contexts[0].name, "")
	_eq(r.contexts[0].line_color, Color.RED)


func test_player_measures_name_prefix_with_character_name_font() -> void:
	var r := FakeRenderer.new()
	_player(r, 2).start("hello", 2.0, "Rex")
	_check(r.measured.has(["Rex: ", true]), "'Rex: ' measured with the character-name font")
	for m in r.measured:
		if m[0] != "Rex: ":
			_check(not m[1], "'%s' measured with the character-name font" % m[0])


# ── GameSubtitleWidget ───────────────────────────────────────────────────────

func _widget() -> GameSubtitleWidget:
	var widget := GameSubtitleWidget.new()
	widget.container_width_override = 400.0
	widget.subtitle_font_size = 20
	widget.character_name_font_size = 24
	get_root().add_child(widget)
	return widget


func test_widget_measurement_matches_rendered_labels() -> void:
	var widget := _widget()
	var text := "The quick brown fox jumps."
	widget.render(PackedStringArray([text]))
	await process_frame # labels pick up theme overrides on the next frame
	var label: Label = widget.get_child(0)
	_eq(label.get_minimum_size().x, widget.measure_line_width(text))
	_check(widget.measure_line_width("Rex: ", true) > widget.measure_line_width("Rex: "),
			"character-name font size is used")
	widget.queue_free()


func test_widget_renders_name_row_and_padding_lines() -> void:
	var widget := _widget()
	var player := GameSubtitlePlayer.new()
	player.initialize(widget, 3)
	player.start("Hello there.", 2.0, "Rex", Color.YELLOW, Color.CYAN)
	await process_frame
	_eq(widget.get_child_count(), 3, "line count:")

	var row := widget.get_child(0) as HBoxContainer
	_check(row != null, "line 0 is a name row")
	var name_label: Label = row.get_child(0)
	var body_label: Label = row.get_child(1)
	_eq(name_label.text, "Rex: ")
	_eq(name_label.get_minimum_size().x, widget.measure_line_width("Rex: ", true))
	_eq(name_label.get_theme_color(&"font_color"), Color.YELLOW)
	_eq(body_label.text, "Hello there.")
	_eq(body_label.get_theme_color(&"font_color"), Color.CYAN)
	_eq((widget.get_child(2) as Label).get_theme_color(&"font_color"), Color.CYAN, "padding line colour:")

	player.stop()
	_eq(widget.get_child_count(), 0, "lines left after stop:")
	widget.queue_free()


func test_widget_container_width() -> void:
	var widget := GameSubtitleWidget.new()
	_eq(widget.get_container_width(), 540.0, "before layout:")
	widget.container_width_override = 321.0
	_eq(widget.get_container_width(), 321.0, "override:")
	widget.free()
