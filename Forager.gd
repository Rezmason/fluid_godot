class_name Forager

var scene : Node2D
var art : Node2D
var clicker : Clicker
var alga : Alga
var jumpTween : Tween

const foragerArt : PackedScene = preload("res://forager.tscn")

func _init(id : int):
	scene = Node2D.new()
	scene.name = "Forager{id}".format({id: id})
	art = foragerArt.instantiate()
	scene.add_child(art)
	clicker = Clicker.new(art.get_node("Area2D"), func(): alga.spread_muck())

func reset():
	if (jumpTween != null):
		jumpTween.stop()
		jumpTween = null

func place(alga : Alga):
	alga.occupant = self
	self.alga = alga
	alga.scene.add_child(scene)
	scene.look_at(Alga.get_random_neighbor(alga).scene.global_position)
	wait_to_jump()

func wait_to_jump():
	Globals.get_timer(Globals.random.randf_range(0.5, 2), jump)

func jump():
	jumpTween = scene.create_tween()
	var startAngle : float = scene.global_rotation
	scene.rotation = startAngle

	var nextAlga : Alga = Alga.get_random_neighbor(alga, func(neighbor): return !neighbor.occupied && neighbor.ripe && neighbor.mucky)

	if (nextAlga == null):
		nextAlga = Alga.get_random_neighbor(alga, func(neighbor): return !neighbor.occupied && neighbor.ripe)

	if (nextAlga != null):
		var oldAlga : Alga = alga
		alga = nextAlga
		oldAlga.occupant = null
		alga.occupant = self

		var angleToAlga : float = oldAlga.scene.get_angle_to(alga.scene.global_position)
		if (angleToAlga - startAngle >  PI) : angleToAlga -= TAU
		if (angleToAlga - startAngle < -PI) : angleToAlga += TAU

		var position : Vector2 = scene.global_position
		oldAlga.scene.remove_child(scene)
		alga.scene.add_child(scene)
		scene.global_position = position

		jumpTween.set_parallel(true)

		jumpTween.tween_property(scene, "rotation", angleToAlga, 0.1) \
		.set_trans(Tween.TransitionType.TRANS_QUAD) \
		.set_ease(Tween.EaseType.EASE_OUT)
		jumpTween.tween_property(scene, "position", Vector2.ZERO, 0.3) \
		.set_trans(Tween.TransitionType.TRANS_QUAD) \
		.set_ease(Tween.EaseType.EASE_OUT)
		jumpTween.tween_callback(
			func():
				if (alga.ripe && alga.occupant == self): alga.eat()
				wait_to_jump()
		).set_delay(0.15)
	else:
		var someAlgaPosition : Vector2 = Alga.get_random_neighbor(alga).scene.global_position
		var angleToRandomAlga : float = alga.scene.get_angle_to(someAlgaPosition)
		if (angleToRandomAlga - startAngle >  PI): angleToRandomAlga -= TAU
		if (angleToRandomAlga - startAngle < -PI): angleToRandomAlga += TAU
		jumpTween.tween_property(scene, "rotation", angleToRandomAlga, 0.3) \
		.set_trans(Tween.TransitionType.TRANS_QUAD) \
		.set_ease(Tween.EaseType.EASE_OUT)
		jumpTween.tween_callback(
			func():
				wait_to_jump()
		).set_delay(0.3)
