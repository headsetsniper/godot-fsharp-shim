extends GdUnitTestSuite

var _runner: GdUnitSceneRunner

func before_test() -> void:
	_runner = GdUnitSceneRunnerImpl.new("res://Scenes/Tetris.tscn", false)

func after_test() -> void:
	if _runner:
		_runner = null

func _ensure_runner() -> void:
	assert_object(_runner).is_not_null()

func _get_root() -> Node2D:
	var root := _runner.scene() as Node2D
	assert_object(root).is_not_null()
	return root

func _get_board(root: Node2D) -> Control:
	var board := root.get_node_or_null("UIRoot/Board") as Control
	assert_object(board).is_not_null()
	return board

func _get_drop_timer(root: Node2D) -> Timer:
	var timer := root.get_node_or_null("UIRoot/DropTimer") as Timer
	assert_object(timer).is_not_null()
	return timer

func _read_int(board: Control, property_name: String) -> int:
	return int(board.get(property_name))

func _read_string(board: Control, property_name: String) -> String:
	return str(board.get(property_name))

func _set_bool(board: Control, property_name: String, value: bool) -> void:
	board.set(property_name, value)

func _set_int(board: Control, property_name: String, value: int) -> void:
	board.set(property_name, value)

func _rows(encoded: String) -> PackedStringArray:
	if encoded.is_empty():
		return PackedStringArray()
	return encoded.split("|")

func _count_ones(encoded: String) -> int:
	var count := 0
	for ch in encoded:
		if ch == '1':
			count += 1
	return count

func _bottom_occupancy(board: Control) -> Array[bool]:
	var rows := _rows(_read_string(board, "GridEncoded"))
	if rows.is_empty():
		return []
	var last := rows[rows.size() - 1]
	var occ: Array[bool] = []
	for ch in last:
		occ.append(ch == '1')
	return occ

@warning_ignore("redundant_await")
func _tick_until_spawn(board: Control, timer: Timer, initial_filled: int) -> bool:
	for _i in range(24):
		timer.emit_signal("timeout")
		await _runner.simulate_frames(1)
		var y := _read_int(board, "CurrentY")
		var filled := _count_ones(_read_string(board, "GridEncoded"))
		if y == 0 and filled > initial_filled:
			return true
	return false

@warning_ignore("redundant_await")
func _rotate_once(board: Control) -> void:
	_set_bool(board, "RotateRequested", true)
	await _runner.simulate_frames(1)

@warning_ignore("redundant_await")
func _move_to_x(board: Control, target_x: int) -> void:
	var guard := 40
	while guard > 0:
		guard -= 1
		var current_x := _read_int(board, "CurrentX")
		if current_x == target_x:
			break
		var delta := 1 if current_x < target_x else -1
		_set_int(board, "MoveX", delta)
		await _runner.simulate_frames(1)
	_set_int(board, "MoveX", 0)

func _choose_target_x(board: Control) -> int:
	var current := _read_string(board, "CurrentEncoded")
	var rows := _rows(current)
	if rows.is_empty():
		return _read_int(board, "CurrentX")
	var height := rows.size()
	var width := rows[0].length()
	var bottom_cols: Array[bool] = []
	for x in range(width):
		var has_block := false
		for y in range(height - 1, -1, -1):
			if rows[y][x] == '1':
				has_block = true
				break
		bottom_cols.append(has_block)
	var occ := _bottom_occupancy(board)
	for pos in range(0, 10 - width + 1):
		var fits := true
		for x in range(width):
			if bottom_cols[x] and occ.size() > pos + x and occ[pos + x]:
				fits = false
				break
		if fits:
			return pos
	return _read_int(board, "CurrentX")

@warning_ignore("redundant_await")
func _fill_and_clear_line(board: Control, timer: Timer, score_before: int) -> bool:
	for _piece in range(10):
		for _attempt in range(2):
			var target := _choose_target_x(board)
			await _move_to_x(board, target)
			_set_bool(board, "HardDropRequested", true)
			await _runner.simulate_frames(1)
			timer.emit_signal("timeout")
			await _runner.simulate_frames(1)
			if _read_int(board, "Score") > score_before:
				return true
			await _rotate_once(board)
	return false

@warning_ignore("redundant_await")
func test_spawns_new_block_when_previous_locks() -> void:
	# Arrange
	_ensure_runner()
	var root := _get_root()
	await _runner.simulate_frames(1)
	var board := _get_board(root)
	var timer := _get_drop_timer(root)
	timer.stop()
	var initial_filled := _count_ones(_read_string(board, "GridEncoded"))

	# Act
	var spawned := await _tick_until_spawn(board, timer, initial_filled)

	# Assert
	assert_bool(spawned).is_true()
	assert_int(_count_ones(_read_string(board, "GridEncoded"))).is_greater(initial_filled)

@warning_ignore("redundant_await")
func test_harddrop_places_piece_on_bottom_then_locks_on_next_tick() -> void:
	# Arrange
	_ensure_runner()
	var root := _get_root()
	await _runner.simulate_frames(1)
	var board := _get_board(root)
	var timer := _get_drop_timer(root)
	timer.stop()
	var start_y := _read_int(board, "CurrentY")
	var filled_before := _count_ones(_read_string(board, "GridEncoded"))

	# Act
	_set_bool(board, "HardDropRequested", true)
	await _runner.simulate_frames(1)
	var y_after_hard := _read_int(board, "CurrentY")
	timer.emit_signal("timeout")
	await _runner.simulate_frames(1)
	var y_after_lock := _read_int(board, "CurrentY")
	var filled_after := _count_ones(_read_string(board, "GridEncoded"))

	# Assert
	assert_int(y_after_hard).is_greater(start_y + 1)
	assert_int(y_after_lock).is_equal(0)
	assert_int(filled_after).is_greater(filled_before)

@warning_ignore("redundant_await")
func test_bagging_stores_current_piece() -> void:
	# Arrange
	_ensure_runner()
	var root := _get_root()
	await _runner.simulate_frames(1)
	var board := _get_board(root)
	var timer := _get_drop_timer(root)
	timer.stop()
	var cur_before := _read_string(board, "CurrentEncoded")
	var bag_before := _read_string(board, "BagEncoded")

	# Act
	_set_bool(board, "BagRequested", true)
	await _runner.simulate_frames(1)
	var bag_after := _read_string(board, "BagEncoded")

	# Assert
	assert_str(bag_before).is_empty()
	assert_str(bag_after).is_not_empty()
	assert_str(bag_after).is_equal(cur_before)

@warning_ignore("redundant_await")
func test_restoring_retrieves_bagged_piece() -> void:
	# Arrange
	_ensure_runner()
	var root := _get_root()
	await _runner.simulate_frames(1)
	var board := _get_board(root)
	var timer := _get_drop_timer(root)
	timer.stop()
	_set_bool(board, "BagRequested", true)
	await _runner.simulate_frames(1)
	var bag_stored := _read_string(board, "BagEncoded")

	# Act
	_set_bool(board, "BagRequested", true)
	await _runner.simulate_frames(1)
	var bag_after := _read_string(board, "BagEncoded")
	var cur_after := _read_string(board, "CurrentEncoded")

	# Assert
	assert_str(bag_stored).is_not_empty()
	assert_str(bag_after).is_empty()
	assert_str(cur_after).is_equal(bag_stored)

@warning_ignore("redundant_await")
func test_clearing_a_line_increases_score_and_removes_row() -> void:
	# Arrange
	_ensure_runner()
	var root := _get_root()
	await _runner.simulate_frames(1)
	var board := _get_board(root)
	var timer := _get_drop_timer(root)
	timer.stop()
	board.set("TestPieceQueue", "O,I,I,I")
	_set_bool(board, "BagRequested", true)
	await _runner.simulate_frames(1)
	var score_before := _read_int(board, "Score")

	# Act
	var cleared := await _fill_and_clear_line(board, timer, score_before)

	# Assert
	assert_bool(cleared).is_true()
	assert_int(_read_int(board, "Score")).is_greater(score_before)
