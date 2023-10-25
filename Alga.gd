class_name Alga

var scene : Node2D
var art : Node2D
var muck : Node2D
var neighbors : Array[Alga] = []
var fruitAnimation : AnimationTree

const algaArt : PackedScene = preload("res://alga.tscn")

var ripe : bool = false
var mucky : bool = false
var restingPosition : Vector2
var goalPosition : Vector2
var occupant : Forager = null

var muckTween : Tween
var fruitTween : Tween

func _init(row : int, column : int, position : Vector2):
	scene = Node2D.new()
	scene.name = "Alga{row}_{column}".format({"row": row, "column": column})

	restingPosition = position
	goalPosition = position
	scene.position = position

	art = algaArt.instantiate()
	fruitAnimation = art.get_node("AnimationTree")
	scene.add_child(art)

	muck = art.get_node("Muck")
	muck.set("modulate", Color(1, 1, 1, 0))
	muck.set("scale", Vector2(0, 0))
	muck.visible = false

func reset():
	mucky = false
	ripe = false
	if (occupant != null):
		scene.remove_child(occupant.scene)
		occupant = null

	if (muckTween != null):
		muckTween.stop()
		muckTween = null

	if (fruitTween != null):
		fruitTween.stop()
		fruitTween = null

	muck.visible = false
	muck.position = Vector2.ZERO
	muck.modulate = Color.WHITE

	fruitAnimation.set("parameters/FruitBlend/blend_position", Vector2.ZERO)
	art.position = Vector2.ZERO

func animate_muck():
	muck.visible = true
	muckTween = muck.create_tween() \
		.set_parallel(true) \
		.set_trans(Tween.TransitionType.TRANS_QUAD) \
		.set_ease(Tween.EaseType.EASE_OUT)
	var duration = 0.3
	var isHere = 1 if mucky else 0
	muckTween.tween_property(muck, "position", Vector2.ZERO, duration)
	muckTween.tween_property(muck, "scale", Vector2(isHere, isHere), duration)
	muckTween.tween_property(muck, "modulate", Color(1, 1, 1, isHere), duration)
	muckTween.tween_property(muck, "visible", mucky, duration)

var occupied : bool = false : get = _get_occupied
func _get_occupied() -> bool:
	return occupant != null

func animate_fruit():
	fruitTween = art.create_tween()
	fruitTween.tween_property(fruitAnimation, "parameters/FruitBlend/blend_position", \
	Vector2(
		1 if mucky else 0,
		1 if ripe else 0
	), 0.5) \
	.set_trans(Tween.TransitionType.TRANS_QUAD) \
	.set_ease(Tween.EaseType.EASE_OUT)

func ripen():
	if (!ripe && occupant == null):
		ripe = true
		animate_fruit()

func eat():
	if (ripe):
		ripe = false
		mucky = false
		animate_muck()
		animate_fruit()
		Globals.muckChanged.sig.emit(self)

func wait_to_spread_muck():
	Globals.get_timer(Globals.random.randf_range(1, 4),
		func():
			if (!mucky): return
			if (Globals.random.randf() < 0.25): spread_muck()
			wait_to_spread_muck()
	)

func spread_muck():
	var cleanNeighbor : Alga = Alga.get_random_neighbor(self,
		func(neighbor): return !neighbor.mucky
	)
	if (cleanNeighbor != null):
		cleanNeighbor.receive_muck_from(scene.global_position)

func receive_muck_from(origin):
	mucky = true
	muck.global_position = origin
	animate_muck()
	animate_fruit()
	Globals.muckChanged.sig.emit(self)
	wait_to_spread_muck()

static func get_random_neighbor(alga : Alga, pred : Callable = Callable()) -> Alga:
	var candidates : Array[Alga] = []
	for neighbor in alga.neighbors:
		if (pred.is_null() || pred.call(neighbor)):
			candidates.push_back(neighbor)
	if (candidates.size() == 0): return null
	return candidates[Globals.random.randi_range(0, candidates.size() - 1)]
