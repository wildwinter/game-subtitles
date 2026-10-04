class_name GameSubtitleTextLayout
extends RefCounted
## Static text-layout utilities for wrapping and paginating subtitle text.
##
## The same algorithm is implemented by the JS, Unity, and Unreal players. Keep them in step.

const SOFT_HYPHEN := "­"
const ELLIPSIS := "…"


## Wraps [param text] into pages of at most [param max_lines] lines, breaking at
## soft hyphens (U+00AD) where necessary.
## [br][br]
## The last line of every non-final page is built with an effective width of
## [code]container_width - measure_width.call("…")[/code], so appending the ellipsis
## afterwards never overflows. Soft-hyphen breaks are used on all but the last line of
## each page, so no word is ever split across a page boundary.
## [br][br]
## [param measure_width] is a [Callable] taking a [String] and returning its rendered width.
## [param first_line_indent] is the width already used on line 0 of every page (for
## example by a character-name prefix). Only that slot is narrowed.
## [br][br]
## Returns the pages; each page is a [PackedStringArray] of lines.
static func wrap_and_paginate(text: String, measure_width: Callable, container_width: float,
		max_lines: int, first_line_indent: float = 0.0) -> Array[PackedStringArray]:
	var ellipsis_width: float = measure_width.call(ELLIPSIS)

	# Split on whitespace, removing empty tokens.
	var normalised := text.replace("\t", " ").replace("\n", " ").replace("\r", " ")
	var words := normalised.split(" ", false)
	if words.is_empty():
		return [PackedStringArray()]

	var out := _Pages.new(max_lines)

	var wi := 0
	while wi < words.size():
		var is_last_slot := out.line_slot == max_lines - 1
		var is_first_slot := out.line_slot == 0
		var effective_width := container_width - ellipsis_width if is_last_slot else container_width
		if is_first_slot and first_line_indent > 0.0:
			effective_width -= first_line_indent

		# Split the current word on soft hyphens to get syllables.
		var syllables := words[wi].split(SOFT_HYPHEN)
		var clean := "".join(syllables)
		var has_syllables := syllables.size() > 1
		var sep := "" if out.line_text.is_empty() else " "

		# Full word fits.
		if measure_width.call(out.line_text + sep + clean) <= effective_width:
			out.line_text += sep + clean
			wi += 1
			continue

		# Syllable-prefix hyphenation: non-last slots with existing content only.
		if not is_last_slot and has_syllables and not out.line_text.is_empty():
			var break_at := _find_syllable_break(syllables, out.line_text, sep, measure_width, effective_width)
			if break_at >= 0:
				out.line_text += sep + "".join(syllables.slice(0, break_at + 1)) + "-"
				words[wi] = SOFT_HYPHEN.join(syllables.slice(break_at + 1))
				out.advance_line()
				continue

		# Flush the current line and retry the word.
		if not out.line_text.is_empty():
			out.advance_line()
			continue

		# Empty line on the last slot: close the page so syllable breaking is available on retry.
		if is_last_slot and not out.page_lines.is_empty():
			out.close_page()
			continue

		# Empty line, non-last slot: try syllable breaking from the start of the word.
		if not is_last_slot and has_syllables:
			var break_at := _find_syllable_break(syllables, "", "", measure_width, effective_width)
			if break_at >= 0:
				out.line_text = "".join(syllables.slice(0, break_at + 1)) + "-"
				words[wi] = SOFT_HYPHEN.join(syllables.slice(break_at + 1))
				out.advance_line()
				continue

		# Character-level break as a last resort.
		var broken := _force_break(clean, measure_width,
				effective_width if is_last_slot else container_width)
		for bi in broken.size() - 1:
			out.line_text = broken[bi]
			out.advance_line()
		out.line_text = broken[broken.size() - 1]
		wi += 1

	# Flush any remaining content.
	if not out.line_text.is_empty():
		out.page_lines.append(out.line_text)
	if not out.page_lines.is_empty():
		out.pages.append(out.page_lines)
	var pages := out.pages
	if pages.is_empty():
		return [PackedStringArray()]

	# Append the ellipsis to the last line of every non-final page.
	for pi in pages.size() - 1:
		var pg := pages[pi]
		pg[pg.size() - 1] += ELLIPSIS
		pages[pi] = pg

	# Rejoin a trailing hyphen fragment with its stem if the whole word fits. The last line
	# was built with the narrower effective width, so a word up to container_width may fit.
	var last_page := pages[pages.size() - 1]
	if last_page.size() >= 2:
		var last_line := last_page[last_page.size() - 1]
		if not last_line.contains(" "):
			var prev_tokens := last_page[last_page.size() - 2].split(" ")
			var stem := prev_tokens[prev_tokens.size() - 1]
			if stem.ends_with("-"):
				var rejoined := stem.left(-1) + last_line
				if measure_width.call(rejoined) <= container_width:
					last_page[last_page.size() - 1] = rejoined
					if prev_tokens.size() > 1:
						last_page[last_page.size() - 2] = " ".join(prev_tokens.slice(0, -1))
					else:
						last_page.remove_at(last_page.size() - 2)
					pages[pages.size() - 1] = last_page

	return pages


## Allocates display time to pages in proportion to their non-whitespace character count.
## The continuation ellipsis is not counted, as it is never spoken.
static func allocate_timings(pages: Array[PackedStringArray], total_duration: float) -> PackedFloat64Array:
	var counts := PackedInt32Array()
	var total := 0
	for page in pages:
		var count := 0
		for ch in "".join(page):
			if ch != ELLIPSIS and ch.strip_edges() != "":
				count += 1
		count = maxi(1, count) # guard against empty pages
		counts.append(count)
		total += count

	var timings := PackedFloat64Array()
	for c in counts:
		timings.append(float(c) / total * total_duration)
	return timings


# Pages built so far, plus the page and line in progress.
class _Pages:
	var pages: Array[PackedStringArray] = []
	var page_lines := PackedStringArray()
	var line_text := ""
	var line_slot := 0 # 0-indexed line position within the current page
	var _max_lines: int

	func _init(max_lines: int) -> void:
		_max_lines = max_lines

	# Ends the current line, starting a new page when the last slot is filled.
	func advance_line() -> void:
		page_lines.append(line_text)
		line_text = ""
		if line_slot == _max_lines - 1:
			close_page()
		else:
			line_slot += 1

	func close_page() -> void:
		pages.append(page_lines)
		page_lines = PackedStringArray()
		line_slot = 0


static func _force_break(word: String, measure_width: Callable, max_width: float) -> PackedStringArray:
	var lines := PackedStringArray()
	var current := ""
	for ch in word:
		var next := current + ch
		if measure_width.call(next) <= max_width:
			current = next
		else:
			if not current.is_empty():
				lines.append(current)
			current = ch
	if not current.is_empty():
		lines.append(current)
	return lines if not lines.is_empty() else PackedStringArray([word])


# Returns the highest syllable index k such that line_text + sep + syllables[0..k] + "-"
# fits within max_width, or -1 if even the first syllable does not fit.
static func _find_syllable_break(syllables: PackedStringArray, line_text: String, sep: String,
		measure_width: Callable, max_width: float) -> int:
	var acc := ""
	var last := -1
	# Test all syllable prefixes except the final one.
	for k in syllables.size() - 1:
		acc += syllables[k]
		if measure_width.call(line_text + sep + acc + "-") <= max_width:
			last = k
		else:
			break # prefixes only grow, so no later prefix can fit
	return last
