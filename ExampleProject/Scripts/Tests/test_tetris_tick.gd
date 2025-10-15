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

func _read_board_int(board: Control, property_name: String) -> int:
	return int(board.get(property_name))

func test_sanity_checks() -> void:
	# Arrange
	var value := 1 + 1

	# Act
	var result := value

	# Assert
	assert_int(result).is_equal(2)

@warning_ignore("redundant_await")
func test_tick_lets_the_block_fall_down() -> void:
	# Arrange
	_ensure_runner()
	var root := _get_root()
	await _runner.simulate_frames(1)
	var board := _get_board(root)
	var timer := _get_drop_timer(root)
	await _runner.simulate_frames(1)
	var start_y := _read_board_int(board, "CurrentY")

	# Act
	timer.stop()
	for _i in range(3):
		timer.emit_signal("timeout")
		await _runner.simulate_frames(1)
	var end_y := _read_board_int(board, "CurrentY")

	# Assert
	assert_int(end_y).is_greater(start_y)
