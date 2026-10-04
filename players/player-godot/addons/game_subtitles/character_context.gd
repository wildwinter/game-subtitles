class_name GameSubtitleCharacterContext
extends RefCounted
## Optional character-name styling passed to a renderer's [code]render()[/code] for every page.
##
## The player passes one of these when a character name or a line colour is set, and
## [code]null[/code] otherwise.

## The character name to display (for example "Aria"). Empty when only a line colour is set.
var name: String = ""

## Colour for the name, or [code]null[/code] to use the renderer's default text colour.
var color: Variant = null

## Colour for the subtitle body text on all lines, or [code]null[/code] to use the
## renderer's default text colour.
var line_color: Variant = null


func _init(p_name: String = "", p_color: Variant = null, p_line_color: Variant = null) -> void:
	name = p_name
	color = p_color
	line_color = p_line_color
