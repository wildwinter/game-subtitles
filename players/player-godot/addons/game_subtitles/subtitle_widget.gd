class_name GameSubtitleWidget
extends VBoxContainer
## Ready-made subtitle renderer. Add it anywhere in your UI and pass it to
## [method GameSubtitlePlayer.initialize].
##
## Each subtitle line is a centred [Label] child, stacked top to bottom. Text is measured
## with [method Font.get_string_size] using the same font and size the labels are given,
## so measurements always match what is drawn.
## [br][br]
## When [member subtitle_font] is not set, the [Label] font from the widget's theme is used.
## Anything else in the theme for [Label], such as an outline or shadow, also applies.
## [br][br]
## If the widget has not been laid out yet when [method GameSubtitlePlayer.start] is called
## (for example on the first frame), set [member container_width_override].

@export_group("Subtitle Font")
## Font for the subtitle text. Leave unset to use the theme's [Label] font.
@export var subtitle_font: Font
## Font size for the subtitle text.
@export var subtitle_font_size: int = 16
## Default text colour, used when the player gives no colour.
@export var text_color: Color = Color.WHITE

@export_group("Character Name Font")
## Font for the character-name prefix. Leave unset to use the subtitle font.
@export var character_name_font: Font
## Size for the character-name prefix. Leave 0 to use the subtitle font size.
@export var character_name_font_size: int = 0

@export_group("Layout")
## Container width in pixels. Leave 0 to use the widget's own width.
@export var container_width_override: float = 0.0

var _line_nodes: Array[Control] = []


func _init() -> void:
	mouse_filter = MOUSE_FILTER_IGNORE
	add_theme_constant_override(&"separation", 0)


## Returns the rendered width of [param text] in the subtitle font, or in the
## character-name font when [param use_character_name_font] is true.
func measure_line_width(text: String, use_character_name_font: bool = false) -> float:
	var font := _character_font() if use_character_name_font else _subtitle_font()
	var font_size := _character_font_size() if use_character_name_font else subtitle_font_size
	return font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x


## Returns the maximum line width available, in pixels.
func get_container_width() -> float:
	if container_width_override > 0.0:
		return container_width_override
	# Fall back to a sensible default before the first layout pass.
	return size.x if size.x > 0.0 else 540.0


## Displays one page of lines. When [param character_context] has a name, "Name: " is
## drawn before the first line in the character-name font.
func render(lines: PackedStringArray, character_context: GameSubtitleCharacterContext = null) -> void:
	_clear_lines()

	var body_color: Color = text_color
	if character_context != null and character_context.line_color != null:
		body_color = character_context.line_color

	for i in lines.size():
		var line: Control
		if i == 0 and character_context != null and not character_context.name.is_empty():
			line = _make_name_line(lines[i], character_context, body_color)
		else:
			line = _make_label(lines[i], _subtitle_font(), subtitle_font_size, body_color)
		add_child(line)
		_line_nodes.append(line)


## Removes all displayed lines.
func clear() -> void:
	_clear_lines()


func _subtitle_font() -> Font:
	return subtitle_font if subtitle_font != null else get_theme_font(&"font", &"Label")


func _character_font() -> Font:
	return character_name_font if character_name_font != null else _subtitle_font()


func _character_font_size() -> int:
	return character_name_font_size if character_name_font_size > 0 else subtitle_font_size


func _make_label(text: String, font: Font, font_size: int, color: Color) -> Label:
	var label := Label.new()
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.add_theme_font_override(&"font", font)
	label.add_theme_font_size_override(&"font_size", font_size)
	label.add_theme_color_override(&"font_color", color)
	label.text = text # layout is already done, so the label never wraps
	return label


# Line 0 with a character name: the name and the body text side by side, centred as a unit.
func _make_name_line(text: String, ctx: GameSubtitleCharacterContext, body_color: Color) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	row.mouse_filter = MOUSE_FILTER_IGNORE
	row.add_theme_constant_override(&"separation", 0)

	var name_color: Color = ctx.color if ctx.color != null else text_color
	row.add_child(_make_label(ctx.name + ": ", _character_font(), _character_font_size(), name_color))
	row.add_child(_make_label(text, _subtitle_font(), subtitle_font_size, body_color))
	return row


func _clear_lines() -> void:
	for node in _line_nodes:
		if is_instance_valid(node):
			remove_child(node)
			node.queue_free()
	_line_nodes.clear()
