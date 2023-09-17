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

		public bool ripe = false;
		public bool mucky = false;
		public Vector2 restingPosition;
		public Vector2 goalPosition;
		public Creature occupant = null;

		public Lilypad(Vector2 position)
		{
			scene = new Node2D();

			restingPosition = position;
			goalPosition = position;
			this.scene.Position = position;

			muck = (Node2D)muckArt.Instantiate();
			muck.Set("modulate", new Color(1, 1, 1, 0));
			muck.Set("scale", new Vector2(0, 0));
			muck.Visible = false;
			scene.AddChild(muck);

			alga = (Node2D)algaArt.Instantiate();
			algaAnimationTree = alga.GetNode<AnimationTree>("AnimationTree");
			scene.AddChild(alga);

			// TEMPORARY
			label = new Label();
			label.LabelSettings = new LabelSettings{FontColor = new Color("black")};
			scene.AddChild(label);

			// TEMPORARY
			algaClicker = new Clicker(alga.GetNode<Area2D>("Area2D"), () => {
				RipenAlga();
				if (mucky) SpreadMuck();
			});
		}

		public void Reset()
		{
			mucky = false;
			ripe = false;
			if (occupant != null) {
				scene.RemoveChild(occupant.scene);
				occupant = null;
			}

			muck.Visible = false;
			muck.Position = Vector2.Zero;
			muck.Modulate = new Color("white");

			algaAnimationTree.Set("parameters/AlgaBlend/blend_position", Vector2.Zero);
			alga.Position = Vector2.Zero;
		}

		private void AnimateMuck()
		{
			muck.Visible = true;
			var tween = muck.CreateTween().SetParallel(true)
				.SetTrans(Tween.TransitionType.Quad)
				.SetEase(Tween.EaseType.Out);
			var duration = 0.3f;
			float isHere = mucky ? 1 : 0;
			tween.TweenProperty(muck, "position", new Vector2(0, 0), duration);
			tween.TweenProperty(muck, "scale", new Vector2(isHere, isHere), duration);
			tween.TweenProperty(muck, "modulate", new Color(1, 1, 1, isHere), duration);
			tween.TweenProperty(muck, "visible", mucky, duration);
		}

		public bool Occupied => occupant != null;

		private void AnimateAlga()
		{
			var tween = alga.CreateTween();
			tween.TweenProperty(algaAnimationTree, "parameters/AlgaBlend/blend_position",
				new Vector2(
					mucky ? 1 : 0,
					ripe ? 1 : 0
				), 0.5f
			)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
		}

		public void RipenAlga()
		{
			if (!ripe && occupant == null) {
				ripe = true;
				AnimateAlga();
			}
		}

		public void EatAlga()
		{
			if (ripe) {
				ripe = false;
				mucky = false;
				AnimateMuck();
				AnimateAlga();
				Game.MuckChanged(this);
			}
		}

		private void WaitToSpreadMuck()
		{
			GetTimer(Game.random.NextDouble() * 3 + 1, () => {
				if (!mucky) return;
				if (Game.random.NextDouble() < 0.25) SpreadMuck();
				WaitToSpreadMuck();
			});
		}

		public void SpreadMuck()
		{
			var cleanNeighbor = Lilypad.GetRandomNeighbor(this, neighbor => !neighbor.mucky);
			if (cleanNeighbor != null) {
				cleanNeighbor.GetMuckFrom(scene.GlobalPosition);
			}
		}

		private void GetMuckFrom(Vector2 origin)
		{
			mucky = true;
			muck.GlobalPosition = origin;
			AnimateMuck();
			AnimateAlga();
			Game.MuckChanged(this);
			WaitToSpreadMuck();
		}

		public static Lilypad GetRandomNeighbor(Lilypad lilypad, Predicate<Lilypad> pred = null)
		{
			var candidates = new List<Lilypad>();
			foreach (var neighbor in lilypad.neighbors)
			{
				if (pred == null || pred(neighbor)) {
					candidates.Add(neighbor);
				}
			}
			if (candidates.Count == 0) return null;
			return candidates[Game.random.Next(candidates.Count)];
		}
	}

	private class Creature {
		public Node2D scene;
		private Clicker clicker;
		public Lilypad lilypad;

		static PackedScene creatureArt = (PackedScene)ResourceLoader.Load("res://creature.tscn");

		public Creature()
		{
			scene = (Node2D)creatureArt.Instantiate();
			clicker = new Clicker(scene.GetNode<Area2D>("Area2D"), () => lilypad.SpreadMuck());
		}

		public void Place(Lilypad lilypad)
		{
			lilypad.occupant = this;
			this.lilypad = lilypad;
			lilypad.scene.AddChild(scene);
			scene.LookAt(Lilypad.GetRandomNeighbor(lilypad).scene.GlobalPosition);
			WaitToJump();
		}

		private void WaitToJump()
		{
			GetTimer(Game.random.NextDouble() * 2 + 0.5, Jump);
		}

		private void Jump()
		{
			var tween = scene.CreateTween();
			var startAngle = scene.GlobalRotation;
			scene.Rotation = startAngle;

			var nextLilypad = Lilypad.GetRandomNeighbor(lilypad, neighbor => !neighbor.Occupied && neighbor.ripe && neighbor.mucky);

			if (nextLilypad == null) {
				nextLilypad = Lilypad.GetRandomNeighbor(lilypad, neighbor => !neighbor.Occupied && neighbor.ripe);
			}

			if (nextLilypad != null) {
				var oldLilypad = lilypad;
				lilypad = nextLilypad;
				oldLilypad.occupant = null;
				lilypad.occupant = this;

				var angleToLilypad = oldLilypad.scene.GetAngleTo(lilypad.scene.GlobalPosition);
				if (angleToLilypad - startAngle >  Math.PI) angleToLilypad -= (float)Math.PI * 2;
				if (angleToLilypad - startAngle < -Math.PI) angleToLilypad += (float)Math.PI * 2;

				var position = scene.GlobalPosition;
				oldLilypad.scene.RemoveChild(scene);
				lilypad.scene.AddChild(scene);
				scene.GlobalPosition = position;

				tween.SetParallel(true);

				tween.TweenProperty(scene, "rotation", angleToLilypad, 0.1f)
					.SetTrans(Tween.TransitionType.Quad)
					.SetEase(Tween.EaseType.Out);
				tween.TweenProperty(scene, "position", new Vector2(0, 0), 0.3f)
					.SetTrans(Tween.TransitionType.Quad)
					.SetEase(Tween.EaseType.Out);
				tween.TweenCallback(Callable.From(() => lilypad.EatAlga())).SetDelay(0.15f);

			} else {
				var someLilypadPosition = Lilypad.GetRandomNeighbor(lilypad).scene.GlobalPosition;
				var angleToRandomLilypad = lilypad.scene.GetAngleTo(someLilypadPosition);
				if (angleToRandomLilypad - startAngle >  Math.PI) angleToRandomLilypad -= (float)Math.PI * 2;
				if (angleToRandomLilypad - startAngle < -Math.PI) angleToRandomLilypad += (float)Math.PI * 2;
				tween.TweenProperty(scene, "rotation", angleToRandomLilypad, 0.3f)
					.SetTrans(Tween.TransitionType.Quad)
					.SetEase(Tween.EaseType.Out);
			}
			WaitToJump();
		}
	}

	List<Lilypad> lilypads = new List<Lilypad>();
	List<Creature> creatures = new List<Creature>();

	private static SceneTree _sceneTree;
	public static Random random = new Random();
	private static Action<Lilypad> MuckChanged;
	static HashSet<Lilypad> muckyLilypads = new HashSet<Lilypad>();
	static bool gameCanEnd = false;
	static bool resetting = false;
	static Node2D fade;

	public override void _Ready()
	{
		_sceneTree = GetTree();
		MuckChanged += DetectEndgame;
		fade = GetNode<Polygon2D>("FullscreenFade");

		SpawnLilypads();
		SpawnCreatures();

		var tween = fade.CreateTween()
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(fade, "modulate", new Color(1, 1, 1, 0), 5);
		tween.TweenProperty(fade, "visible", false, 0);
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouse mouseEvent) {
			bool isMousePressed = (mouseEvent.ButtonMask & MouseButtonMask.Left) == MouseButtonMask.Left;
			var mousePosition = GetLocalMousePosition();
			
			foreach (var lilypad in lilypads) {
				if (lilypad.mucky || !isMousePressed) {
					lilypad.goalPosition = lilypad.restingPosition;
				} else {
					var localMousePosition = mousePosition - lilypad.restingPosition;
					float offset = -localMousePosition.Length() / 50;
					offset *= Mathf.Pow(3, offset);
					lilypad.goalPosition = lilypad.restingPosition + localMousePosition * offset;
				}
			}
		}
	}

	public override void _Process(Double delta)
	{
		foreach (var lilypad in lilypads) {
			lilypad.scene.Position = lilypad.scene.Position.Lerp(lilypad.goalPosition, 0.1f);
		}
	}

	private void SpawnLilypads()
	{
		List<List<Lilypad>> grid = new List<List<Lilypad>>();

		const int numRows = 9, numColumns = 10;
		var spacing = new Vector2(110, 90);
		for (int i = 0; i < numRows; i++) {
			var rowOffset = new Vector2(1 - (numColumns - i % 2), 1 - numRows) / 2;
			var row = new List<Lilypad>();
			grid.Add(row);
			for (int j = 0; j < numColumns; j++) {
				if (i % 2 == 1 && j == numColumns - 1) {
					row.Add(null);
					continue;
				}
				var lilypad = new Lilypad((new Vector2(j, i) + rowOffset) * spacing);
				row.Add(lilypad);
				lilypads.Add(lilypad);
				AddChild(lilypad.scene);
				//lilypad.label.Text = $"{i},{j}";
				lilypad.Reset();
			}
		}

		void ConnectNeighbors(Lilypad l1, Lilypad l2) {
			if (l1 == null || l2 == null) return;
			l1.neighbors.Add(l2);
			l2.neighbors.Add(l1);
		}

		for (int i = 0; i < numRows; i++) {
			for (int j = 0; j < numColumns; j++) {
				var lilypad = grid[i][j];
				if (lilypad == null) continue;
				if (j > 0) {
					ConnectNeighbors(lilypad, grid[i][j - 1]);
				}
				if (i > 0) {
					ConnectNeighbors(lilypad, grid[i - 1][j]);
					int j2 = j + (i % 2) * 2 - 1;
					if (j2 >= 0) {
						ConnectNeighbors(lilypad, grid[i - 1][j2]);
					}
				}
			}
		}

		//foreach (var lilypad in lilypads) {
		//	lilypad.label.Text = lilypad.neighbors.Count.ToString();
		//}
	}

	private void SpawnCreatures()
	{
		const int numCreatures = 2;
		for (int i = 0; i < numCreatures; i++) {
			creatures.Add(new Creature());
		}
		ResetCreatures();
	}

	private void ResetCreatures()
	{
		foreach (var creature in creatures) {
			var lilypad = lilypads[Game.random.Next(lilypads.Count)];
			while (lilypad.occupant != null) {
				lilypad = lilypads[Game.random.Next(lilypads.Count)];
			}
			creature.Place(lilypad);
		}
	}

	private void DetectEndgame(Lilypad lilypad)
	{
		if (lilypad.mucky) {
			muckyLilypads.Add(lilypad);
		} else {
			muckyLilypads.Remove(lilypad);
		}
		int numMuckyLilypads = muckyLilypads.Count;

		if (gameCanEnd) {
			if (numMuckyLilypads == 0) {
				Reset();
			} else if ((float)numMuckyLilypads / lilypads.Count > 0.6) {
				Reset();
			}
		} else if (!resetting && numMuckyLilypads >= 3) {
			gameCanEnd = true;
		}
	}

	private void Reset()
	{
		resetting = true;
		gameCanEnd = false;
		fade.Visible = true;
		var tween = fade.CreateTween()
			.SetTrans(Tween.TransitionType.Quad);
		tween.TweenProperty(fade, "modulate", new Color(1, 1, 1, 1), 5)
			.SetEase(Tween.EaseType.In);
		tween.TweenCallback(Callable.From(() => {
			foreach (var lilypad in lilypads) {
				lilypad.Reset();
			}
			ResetCreatures();
			muckyLilypads.Clear();
			resetting = false;
		}));
		tween.TweenProperty(fade, "modulate", new Color(1, 1, 1, 0), 5)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(fade, "visible", false, 0);
	}

	public static SceneTreeTimer GetTimer(double timeSec, Action action)
	{
		var timer = _sceneTree.CreateTimer(timeSec);
		timer.Timeout += action;

		return timer;
	}
}
