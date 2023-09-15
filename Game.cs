using Godot;
using System;
using System.Collections.Generic;

public partial class Game : Node2D
{
	private class Clicker
	{
		private bool mouseOver = false;
		private bool mousePressed = false;
		private CollisionObject2D target;
		private Action callback;

		public Clicker(CollisionObject2D target, Action callback)
		{
			this.target = target;
			this.callback = callback;

			target.InputEvent += (_, inputEvent, _) => {
				var mouseButtonEvent = inputEvent as InputEventMouseButton;
				if (mouseButtonEvent == null || mouseButtonEvent.ButtonIndex != MouseButton.Left) return;
				bool mouseWasPressed = mousePressed;
				mousePressed = mouseButtonEvent.Pressed;
				if (mouseWasPressed && mouseOver && !mousePressed) {
					this.callback();
				}
			};
			target.MouseEntered += () => mouseOver = true;
			target.MouseExited += () => {
				mouseOver = false;
				mousePressed = false; // Not perfect, but fine for most cases
			};
		}
	}

	private class Lilypad {
		public Node2D scene;
		public Node2D alga;
		public Node2D muck;
		public Label label;
		public HashSet<Lilypad> neighbors = new HashSet<Lilypad>();

		private Clicker algaClicker;
		private AnimationTree algaAnimationTree;

		static PackedScene algaArt = (PackedScene)ResourceLoader.Load("res://alga.tscn");
		static PackedScene muckArt = (PackedScene)ResourceLoader.Load("res://muck.tscn");

		public bool fed = false;
		public bool mucked = false;
		public Creature occupant = null;

		public Lilypad()
		{
			scene = new Node2D();
			muck = (Node2D)muckArt.Instantiate();
			scene.AddChild(muck);
			muck.Visible = false;
			alga = (Node2D)algaArt.Instantiate();
			scene.AddChild(alga);
			label = new Label();
			label.LabelSettings = new LabelSettings{FontColor = new Color("black")};
			scene.AddChild(label);

			algaClicker = new Clicker(alga.GetNode<Area2D>("Area2D"), FeedAlga); // TEMPORARY
			algaAnimationTree = alga.GetNode<AnimationTree>("AnimationTree");
		}

		private void AnimateAlga()
		{
			var tween = alga.GetTree().CreateTween();
			tween.TweenProperty(algaAnimationTree, "parameters/AlgaBlend/blend_position",
				new Vector2(
					mucked ? 1 : 0,
					fed ? 1 : 0
				), 0.5f
			)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
		}

		private void FeedAlga()
		{
			if (!fed && occupant == null) {
				fed = true;
				AnimateAlga();
			}
		}
	}

	private class Creature {
		public Node2D scene;
		private Clicker clicker;

		static PackedScene creatureArt = (PackedScene)ResourceLoader.Load("res://creature.tscn");

		public Creature()
		{
			scene = (Node2D)creatureArt.Instantiate();
			clicker = new Clicker(scene.GetNode<Area2D>("Area2D"), () => GD.Print("*fart*"));
		}
	}

	List<Lilypad> lilypads = new List<Lilypad>();
	List<Creature> creatures = new List<Creature>();

	public override void _Ready()
	{
		SpawnLilypads();
		SpawnCreatures();
	}

	private void SpawnLilypads()
	{
		List<List<Lilypad>> grid = new List<List<Lilypad>>();

		const int numRows = 9;
		int[] numColumns = {10, 9};
		var spacing = new Vector2(110, 90);
		for (int i = 0; i < numRows; i++) {
			var rowOffset = new Vector2(1 - numColumns[i % 2], 1 - numRows) / 2;
			var row = new List<Lilypad>();
			grid.Add(row);
			for (int j = 0; j < numColumns[i % 2]; j++) {
				var lilypad = new Lilypad();
				lilypad.scene.Position = (new Vector2(j, i) + rowOffset) * spacing;
				row.Add(lilypad);
				lilypads.Add(lilypad);
				AddChild(lilypad.scene);
				// lilypad.label.Text = $"{i},{j}";
			}
		}

		void ConnectNeighbors(Lilypad l1, Lilypad l2) {
			l1.neighbors.Add(l2);
			l2.neighbors.Add(l1);
		}

		for (int i = 0; i < numRows; i++) {
			for (int j = 1; j < numColumns[i % 2]; j++) {
				ConnectNeighbors(grid[i][j], grid[i][j - 1]);
				if (i > 0) {
					ConnectNeighbors(grid[i][j], grid[i - 1][j - ((i + 1) % 2)]);
				}
				if (i < numRows - 1) {
					ConnectNeighbors(grid[i][j], grid[i + 1][j - ((i + 1) % 2)]);
				}
			}
		}
	}

	private void SpawnCreatures()
	{
		Random rnd = new Random();
		const int numCreatures = 2;
		for (int i = 0; i < numCreatures; i++) {
			var creature = new Creature();
			creatures.Add(creature);
			var lilypad = lilypads[rnd.Next(lilypads.Count)];
			while (lilypad.occupant != null) {
				lilypad = lilypads[rnd.Next(lilypads.Count)];
			}
			lilypad.occupant = creature;
			lilypad.scene.AddChild(creature.scene);
		}
	}

	public override void _Process(double delta)
	{

	}
}

/*



let numNodes = 0;
let offsetX = -1;
let ike = 0;
while (ike * 90 + 30 < height) {
	numNodes++;
	offsetX = (offsetX == 0) ? 55 : 0;
	let jen = 0;
	while (jen * 110 + 20 < width)
	{
			const depth = nodeHolder.getNextHighestDepth();
			const node = nodeHolder.attachMovie("node", "node_" + ike + "_" + jen, depth);
			node._x = node.restX = jen * 110 + 20 + offsetX;
			node._y = node.restY = ike * 90 + 30;
			node.dirty = false;
			node.inhabitant = undefined;
			node.fertilized = false;
			node.neighbors = [];
			nodes.push(node);

			const muck = muckHolder.attachMovie("muck", "muck_" + ike + "_" + jen, depth);
			node.muck = muck;
			muck._x = muck.restX = node._x;
			muck._y = muck.restY = node._y;
			mucks.push(muck);
			++jen;
	}
	++ike;
}
*/
