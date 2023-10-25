class_name Feeder

var minSeedColor : Color = Color.TRANSPARENT
var maxSeedColor : Color = Color.WHITE
var bobDirection : Vector2 = Vector2.from_angle(PI * 0.16)

const maxAvailableSeeds : int = 40
const minSeedDist : float = 100
const minDist : float = 80
const margin : float = 50
var age : float
var availableSeeds : float
var throbStartTime : int
var scene : Node2D
var art : Node2D
var fill : Node2D
var children : Array[Feeder] = []
var elements : Array[Feeder] = []
var parent : Feeder
var velocity : Vector2 = Vector2.ZERO
var size : int = 0 : get = _get_size
func _get_size() -> int:
	return elements.size()
var opacityTween : Tween

const feederArt : PackedScene = preload("res://feeder.tscn")

func _init(id : int):
	scene = Node2D.new()
	scene.name = "Feeder{id}".format({id:id})
	art = feederArt.instantiate()
	scene.add_child(art)
	fill = art.get_node("Fill")

func reset():
	for child in children:
		scene.remove_child(child.scene)
		child.parent = null
	children.clear()
	elements.clear()
	elements.push_back(self)
	art.position = Vector2.ZERO
	parent = null
	velocity = Vector2.ZERO
	age = 0
	availableSeeds = 0
	throbStartTime = 0

	if (opacityTween != null):
		opacityTween.stop()
		opacityTween = null
	fill.set("modulate", maxSeedColor)

func try_to_seed(alga : Alga) -> bool:
	if (self.size < 3 || availableSeeds <= 0): return false
	var minSeedDistSquared : float = minSeedDist * minSeedDist
	var distSquared : float = scene.global_position.distance_squared_to(alga.scene.global_position);
	if (distSquared > minSeedDistSquared): return false

	alga.ripen()
	availableSeeds -= 1
	if (availableSeeds <= 0):
		burst()
	else:
		var opacity : float = availableSeeds / maxAvailableSeeds
		for feeder in elements: feeder.animate_opacity(opacity)
	return true

func burst():
	var oldPosition : Vector2 = scene.global_position
	var artPositions : Array[Vector2] = []
	for feeder in elements:
		artPositions.push_back(feeder.art.global_position)

	var parentNode : Node2D = scene.get_parent()
	for child in children:
		scene.remove_child(child.scene)
		child.parent = null
		parentNode.add_child(child.scene)
	children.clear()
		
	for i in 3:
		var feeder : Feeder = elements[i]
		feeder.age = 0
		feeder.scene.global_position = artPositions[i]
		feeder.velocity = (artPositions[i] - oldPosition) * 6
		feeder.art.position = Vector2.ZERO
		
	for feeder in elements: feeder.animate_opacity(1)

	elements.clear()
	elements.push_back(self)
		
	availableSeeds = 0

func animate_opacity(amount : float):
	opacityTween = art.create_tween() \
	.set_trans(Tween.TransitionType.TRANS_QUAD) \
	.set_ease(Tween.EaseType.EASE_OUT)
	var targetColor : Color = minSeedColor.lerp(maxSeedColor, amount)
	opacityTween.tween_property(fill, "modulate", targetColor, 0.1)

func try_to_combine(other : Feeder) -> bool :
	if (self.size >= 3): return false
	const minDistSquared : float = minDist * minDist
	var otherGlobalPosition : Vector2 = other.art.global_position

	for feeder in elements:
		var distSquared : float = feeder.art.global_position.distance_squared_to(otherGlobalPosition);
		if (distSquared > minDistSquared): return false

	children.push_back(other)
	elements.push_back(other)
	other.parent = self

	velocity = (velocity * (self.size - 1) + other.velocity) / self.size
	other.velocity = Vector2.ZERO
	other.age = 0

	if (self.size == 3):
		availableSeeds = maxAvailableSeeds
		throbStartTime = Time.get_ticks_msec()

	var averageGlobalPosition : Vector2 = Vector2.ZERO
	var artPositions : Array[Vector2] = []
	for feeder in elements:
		averageGlobalPosition += feeder.art.global_position
		artPositions.push_back(feeder.art.global_position)
	averageGlobalPosition /= self.size

	scene.global_position = averageGlobalPosition
	other.scene.get_parent().remove_child(other.scene)
	scene.add_child(other.scene)
	other.scene.position = Vector2.ZERO
	other.scene.rotation = 0

	for i in self.size: elements[i].art.global_position = artPositions[i]

	return true

func update(delta : float):
	if (parent != null): return
		
	age += delta

	var oldPosition : Vector2 = scene.position
		
	var pushForce : Vector2 = Vector2.ZERO
	if (Globals.isMousePressed):
		var localPushPosition : Vector2 = Globals.mousePosition - scene.position
		var force : float = 2000 / localPushPosition.length_squared()
		if (force > 0.05):
			pushForce = -localPushPosition * force

	var mag : float = 10
	velocity += pushForce * mag * delta
	var bobVelocity : float = sin((scene.position.x + scene.position.y) * 0.006 + Time.get_ticks_msec() * 0.001) * 3
	scene.position += (velocity + bobDirection * bobVelocity) * mag * delta
	scene.position += Vector2.from_angle((Globals.random.randf() * PI)) * 0.1
	velocity = velocity.lerp(Vector2.ZERO, 0.01)
		
	# Avoid the edges
	var currentRadius : float = 50
	var currentMargin : Vector2 = (Globals.screenSize - Vector2.ONE * (margin + currentRadius)) / 2
	var edgeGoalPosition : Vector2 = scene.position.clamp(-currentMargin, currentMargin)
	scene.position = scene.position.lerp(edgeGoalPosition, 0.08)

	scene.global_rotation += ((oldPosition.x + oldPosition.y) - (scene.position.x + scene.position.y)) / self.size * 0.005
		
	if (self.size == 2):
		for feeder in elements:
			var art : Node2D = feeder.art
			var goalPosition : Vector2 = art.position * (minDist / 2) / art.position.length()
			art.position = art.position.lerp(goalPosition, 0.2)
	elif (self.size == 3):
		var averagePosition : Vector2 = (
			elements[0].art.position +
			elements[1].art.position +
			elements[2].art.position
		) / 3
		for feeder in elements:
			var art : Node2D = feeder.art
			var goalPosition : Vector2 = art.position - averagePosition
			goalPosition *= (minDist / 2) / goalPosition.length()
			art.position = art.position.lerp(goalPosition, 0.2)
