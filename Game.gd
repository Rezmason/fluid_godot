extends Node2D
class_name Game

var numMuckyAlgae : int = 0
var gameCanEnd : bool = false
var resetting : bool = false
var fade : Node2D
var feederMetaballs : ShaderMaterial

var algae : Array[Alga] = []
var foragers : Array[Forager] = []
var feeders : Array[Feeder] = []

var metaballData : Array[Color] = []
var metaballGroupData : Array[Color] = []

func _ready():
	Globals.init(self)
	Globals.muckChanged.sig.connect(detect_endgame)
	fade = get_node("FullscreenFade")
	feederMetaballs = get_node("FeederMetaballs").material

	spawn_algae()
	spawn_foragers()
	spawn_feeders()

	var emptyColor : Color = Color(Color.BLACK, 0)
	for i in 10:
		metaballData.push_back(emptyColor)
	for i in 3:
		metaballGroupData.push_back(Color.WHITE)

	var tween : Tween = fade.create_tween() \
	.set_trans(Tween.TransitionType.TRANS_QUAD) \
	.set_ease(Tween.EaseType.EASE_OUT)
	tween.tween_property(fade, "modulate", Color(Color.WHITE, 0), 5)
	tween.tween_property(fade, "visible", false, 0)

func _unhandled_input(inputEvent : InputEvent):
	if (inputEvent is InputEventMouse):
		const left = MouseButtonMask.MOUSE_BUTTON_MASK_LEFT
		Globals.isMousePressed = (inputEvent.button_mask & left) == left
		Globals.mousePosition = get_local_mouse_position()

		for alga in algae:
			if (alga.mucky || !Globals.isMousePressed):
				alga.goalPosition = alga.restingPosition
			else:
				var localPushPosition : Vector2 = Globals.mousePosition - alga.restingPosition
				var offset : float = -localPushPosition.length() / 50
				offset *= pow(3, offset)
				alga.goalPosition = alga.restingPosition + localPushPosition * offset

func _process(delta : float):
	for feeder in feeders:
		feeder.update(delta)

	var seedingFeeders : Array[Feeder] = []

	const minAge : float = 3
		
	for i in feeders.size():
		var feeder : Feeder = feeders[i]
		if (feeder.parent != null || feeder.age < minAge): continue
		if (feeder.size >= 3):
			seedingFeeders.push_back(feeder)
		else:
			for j in range(i + 1, feeders.size()):
				var other : Feeder = feeders[j]
				if (other.parent != null || other.age < minAge || feeder.size + other.size > 3): continue
				if (feeder.size >= other.size):
					if (feeder.try_to_combine(other)): break
				else:
					if (other.try_to_combine(feeder)): break

	var n : int = 0
	var f : int = 1
	var now : int = Time.get_ticks_msec()
	metaballGroupData[0] = Color.WHITE
	for feeder in feeders:
		if (feeder.parent != null): continue
		var groupID : int = 0
		var throb : float = 0
		var throbTime : float = 0
		if (feeder.availableSeeds > 0):
			groupID = f
			var opacity : float = feeder.availableSeeds / Feeder.maxAvailableSeeds
			# opacity = 1 - pow(1 - opacity, 2)
			opacity = lerp(metaballGroupData[groupID].r, opacity, 0.1)
			metaballGroupData[groupID] = Color(opacity, 0, 0, 0)
			f += 1
			throb = 7
			throbTime = float(now - feeder.throbStartTime) / 1000
		var i : int = 0
		for element in feeder.elements:
			var elementPosition : Vector2 = element.art.global_position
			metaballData[n] = Color(
				elementPosition.x,
				elementPosition.y,
				15 + throb * (sin((i * PI * 2 / 3) + throbTime * 4) * 0.5 + 0.5),
				groupID
			)
			n += 1
			i += 1
	for j in range(f, 3):
		metaballGroupData[f] = Color.WHITE

	feederMetaballs.set_shader_parameter("metaballs", metaballData)
	feederMetaballs.set_shader_parameter("metaballGroups", metaballGroupData)
		
	for alga in algae:
		alga.scene.position = alga.scene.position.lerp(alga.goalPosition, 0.1)
			
		if (alga.ripe || alga.occupant != null): continue
			
		for feeder in seedingFeeders:
			if (feeder.size == 3 && feeder.try_to_seed(alga)): break

func spawn_algae():
	var grid : Array[Array] = []

	const numRows : int = 9
	const numColumns : int = 10
	var spacing : Vector2 = Vector2(110, 90)
	for i in numRows:
		var rowOffset : Vector2 = Vector2(1 - (numColumns - i % 2), 1 - numRows) / 2
		var row : Array[Alga] = []
		for j in numColumns:
			if (i % 2 == 1 && j == numColumns - 1):
				row.push_back(null)
				continue
			var alga : Alga = Alga.new(grid.size(), row.size(), (Vector2(j, i) + rowOffset) * spacing)
			row.push_back(alga)
			algae.push_back(alga)
			add_child(alga.scene)
			alga.reset()
		grid.push_back(row)

	for i in numRows:
		for j in numColumns:
			var alga : Alga = grid[i][j]
			if (alga == null): continue
			if (j > 0):
				Game.connect_neighbors(alga, grid[i][j - 1])
			if (i > 0):
				Game.connect_neighbors(alga, grid[i - 1][j])
				var j2 : int = j + (i % 2) * 2 - 1
				if (j2 >= 0):
					Game.connect_neighbors(alga, grid[i - 1][j2])

func spawn_foragers():
	const numForagers : int = 2
	for i in numForagers:
		foragers.push_back(Forager.new(foragers.size()))
	reset_foragers()

func spawn_feeders():
	const numFeeders : int = 7
	for i in numFeeders:
		feeders.push_back(Feeder.new(feeders.size()))
	reset_feeders()

func reset_foragers():
	for forager in foragers:
		var alga : Alga = algae[Globals.random.randi_range(0, algae.size() - 1)]
		while (alga.occupant != null):
			alga = algae[Globals.random.randi_range(0, algae.size() - 1)]
		forager.reset()
		forager.place(alga)

func reset_feeders():
	for feeder in feeders:
		feeder.reset()
		add_child(feeder.scene)
		feeder.scene.global_position = Vector2(
			Globals.random.randf() - 0.5,
			Globals.random.randf() - 0.5
		) * Globals.screenSize
		feeder.velocity = Vector2(
			Globals.random.randf() - 0.5,
			Globals.random.randf() - 0.5
		) * 200

func detect_endgame(alga : Alga):
	if (resetting): return

	if (alga.mucky):
		numMuckyAlgae += 1
	else:
		numMuckyAlgae -= 1

	if (gameCanEnd):
		if (numMuckyAlgae == 0):
			reset()
		elif (float(numMuckyAlgae) / algae.size() > 0.6):
			reset()
	elif (!resetting && numMuckyAlgae >= 3):
		gameCanEnd = true

func reset():
	resetting = true
	gameCanEnd = false
	fade.visible = true
	var tween : Tween = fade.create_tween() \
	.set_trans(Tween.TransitionType.TRANS_QUAD)
	tween.tween_property(fade, "modulate", Color.WHITE, 5) \
	.set_ease(Tween.EaseType.EASE_IN)
	tween.tween_callback(
		func():
			for alga in algae:
				alga.reset()
			reset_foragers()
			reset_feeders()
			numMuckyAlgae = 0
			resetting = false
	)
	tween.tween_property(fade, "modulate", Color(Color.WHITE, 0), 5) \
	.set_ease(Tween.EaseType.EASE_OUT)
	tween.tween_property(fade, "visible", false, 0)

static func connect_neighbors(l1 : Alga, l2 : Alga):
	if (l1 == null || l2 == null): return
	# if(!l1.neighbors.has(l2) || !l2.neighbors.has(l1)): return
	l1.neighbors.push_back(l2)
	l2.neighbors.push_back(l1)
