class_name Clicker

var mouseOver : bool = false
var mousePressed : bool = false

func _init(target : CollisionObject2D, callable : Callable):
	
	target.input_event.connect(
		func(viewport : Viewport, event : InputEvent, shapeIndex : int):
			if (!(event is InputEventMouseButton)): return
			if (event.button_index != MouseButton.MOUSE_BUTTON_LEFT): return
			var mouseWasPressed : bool = mousePressed
			mousePressed = event.pressed
			if (mouseWasPressed && mouseOver && !mousePressed):
				callable.call()
	)

	target.mouse_entered.connect(func(): mouseOver = true)
	target.mouse_exited.connect(
		func():
			mouseOver = false
			mousePressed = false # Not perfect, but fine for most cases
	)
