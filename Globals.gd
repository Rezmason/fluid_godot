class_name Globals

static var random : RandomNumberGenerator = RandomNumberGenerator.new()
static var screenSize : Vector2
static var isMousePressed : bool
static var mousePosition : Vector2
static var muckChanged : Signaler = Signaler.new()

static var sceneTree

static func init(basis):
	screenSize = basis.get_viewport().size
	sceneTree = basis.get_tree()

static func get_timer(timeSec : float, callable : Callable):
	await sceneTree.create_timer(timeSec).timeout
	callable.call()

class Signaler:
	signal sig;
