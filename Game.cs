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

	private class Alga {
		public Node2D scene;
		public Node2D art;
		public Node2D muck;
		public HashSet<Alga> neighbors = new HashSet<Alga>();

		private AnimationTree fruitAnimation;

		static PackedScene algaArt = ResourceLoader.Load<PackedScene>("res://alga.tscn");

		public bool ripe = false;
		public bool mucky = false;
		public Vector2 restingPosition;
		public Vector2 goalPosition;
		public Forager occupant = null;

		private Tween muckTween;
		private Tween fruitTween;

		public Alga(Vector2 position)
		{
			scene = new Node2D();

			restingPosition = position;
			goalPosition = position;
			this.scene.Position = position;

			art = (Node2D)algaArt.Instantiate();
			fruitAnimation = art.GetNode<AnimationTree>("AnimationTree");
			scene.AddChild(art);

			muck = art.GetNode<Node2D>("Muck");
			muck.Set("modulate", new Color(1, 1, 1, 0));
			muck.Set("scale", new Vector2(0, 0));
			muck.Visible = false;
		}

		public void Reset()
		{
			mucky = false;
			ripe = false;
			if (occupant != null) {
				scene.RemoveChild(occupant.scene);
				occupant = null;
			}

			if (muckTween != null) {
				muckTween.Stop();
				muckTween = null;
			}

			if (fruitTween != null) {
				fruitTween.Stop();
				fruitTween = null;
			}

			muck.Visible = false;
			muck.Position = Vector2.Zero;
			muck.Modulate = new Color("white");

			fruitAnimation.Set("parameters/FruitBlend/blend_position", Vector2.Zero);
			art.Position = Vector2.Zero;
		}

		private void AnimateMuck()
		{
			muck.Visible = true;
			muckTween = muck.CreateTween().SetParallel(true)
				.SetTrans(Tween.TransitionType.Quad)
				.SetEase(Tween.EaseType.Out);
			var duration = 0.3f;
			float isHere = mucky ? 1 : 0;
			muckTween.TweenProperty(muck, "position", new Vector2(0, 0), duration);
			muckTween.TweenProperty(muck, "scale", new Vector2(isHere, isHere), duration);
			muckTween.TweenProperty(muck, "modulate", new Color(1, 1, 1, isHere), duration);
			muckTween.TweenProperty(muck, "visible", mucky, duration);
		}

		public bool Occupied => occupant != null;

		private void AnimateFruit()
		{
			fruitTween = art.CreateTween();
			fruitTween.TweenProperty(fruitAnimation, "parameters/FruitBlend/blend_position",
				new Vector2(
					mucky ? 1 : 0,
					ripe ? 1 : 0
				), 0.5f
			)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
		}

		public void Ripen()
		{
			if (!ripe && occupant == null) {
				ripe = true;
				AnimateFruit();
			}
		}

		public void Eat()
		{
			if (ripe) {
				ripe = false;
				mucky = false;
				AnimateMuck();
				AnimateFruit();
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
			var cleanNeighbor = Alga.GetRandomNeighbor(this, neighbor => !neighbor.mucky);
			if (cleanNeighbor != null) {
				cleanNeighbor.ReceiveMuckFrom(scene.GlobalPosition);
			}
		}

		private void ReceiveMuckFrom(Vector2 origin)
		{
			mucky = true;
			muck.GlobalPosition = origin;
			AnimateMuck();
			AnimateFruit();
			Game.MuckChanged(this);
			WaitToSpreadMuck();
		}

		public static Alga GetRandomNeighbor(Alga alga, Predicate<Alga> pred = null)
		{
			var candidates = new List<Alga>();
			foreach (var neighbor in alga.neighbors)
			{
				if (pred == null || pred(neighbor)) {
					candidates.Add(neighbor);
				}
			}
			if (candidates.Count == 0) return null;
			return candidates[Game.random.Next(candidates.Count)];
		}
	}

	private class Forager {
		public Node2D scene;
		private Clicker clicker;
		public Alga alga;
		Tween jumpTween;

		static PackedScene foragerArt = ResourceLoader.Load<PackedScene>("res://forager.tscn");

		public Forager()
		{
			scene = (Node2D)foragerArt.Instantiate();
			clicker = new Clicker(scene.GetNode<Area2D>("Area2D"), () => alga.SpreadMuck());
		}

		public void Reset()
		{
			if (jumpTween != null) {
				jumpTween.Stop();
				jumpTween = null;
			}
		}

		public void Place(Alga alga)
		{
			alga.occupant = this;
			this.alga = alga;
			alga.scene.AddChild(scene);
			scene.LookAt(Alga.GetRandomNeighbor(alga).scene.GlobalPosition);
			WaitToJump();
		}

		private void WaitToJump()
		{
			GetTimer(Game.random.NextDouble() * 1.5f + 0.5f, Jump);
		}

		private void Jump()
		{
			jumpTween = scene.CreateTween();
			var startAngle = scene.GlobalRotation;
			scene.Rotation = startAngle;

			var nextAlga = Alga.GetRandomNeighbor(alga, neighbor => !neighbor.Occupied && neighbor.ripe && neighbor.mucky);

			if (nextAlga == null) {
				nextAlga = Alga.GetRandomNeighbor(alga, neighbor => !neighbor.Occupied && neighbor.ripe);
			}

			if (nextAlga != null) {
				var oldAlga = alga;
				alga = nextAlga;
				oldAlga.occupant = null;
				alga.occupant = this;

				var angleToAlga = oldAlga.scene.GetAngleTo(alga.scene.GlobalPosition);
				if (angleToAlga - startAngle >  Math.PI) angleToAlga -= (float)Math.PI * 2;
				if (angleToAlga - startAngle < -Math.PI) angleToAlga += (float)Math.PI * 2;

				var position = scene.GlobalPosition;
				oldAlga.scene.RemoveChild(scene);
				alga.scene.AddChild(scene);
				scene.GlobalPosition = position;

				jumpTween.SetParallel(true);

				jumpTween.TweenProperty(scene, "rotation", angleToAlga, 0.1f)
					.SetTrans(Tween.TransitionType.Quad)
					.SetEase(Tween.EaseType.Out);
				jumpTween.TweenProperty(scene, "position", new Vector2(0, 0), 0.3f)
					.SetTrans(Tween.TransitionType.Quad)
					.SetEase(Tween.EaseType.Out);
				jumpTween.TweenCallback(Callable.From(() => {
					if (alga.ripe && alga.occupant == this) alga.Eat();
				})).SetDelay(0.15f);

			} else {
				var someAlgaPosition = Alga.GetRandomNeighbor(alga).scene.GlobalPosition;
				var angleToRandomAlga = alga.scene.GetAngleTo(someAlgaPosition);
				if (angleToRandomAlga - startAngle >  Math.PI) angleToRandomAlga -= (float)Math.PI * 2;
				if (angleToRandomAlga - startAngle < -Math.PI) angleToRandomAlga += (float)Math.PI * 2;
				jumpTween.TweenProperty(scene, "rotation", angleToRandomAlga, 0.3f)
					.SetTrans(Tween.TransitionType.Quad)
					.SetEase(Tween.EaseType.Out);
			}
			WaitToJump();
		}
	}
	
	private class Feeder
	{
		const int maxAvailableSeeds = 40;
		const float minSeedDist = 100;
		const float minDist = 80;
		const float margin = 50;
		public float age;
		public float availableSeeds;
		public Node2D scene;
		public Node2D art;
		private List<Feeder> children = new List<Feeder>();
		private List<Feeder> elements = new List<Feeder>();
		public Feeder parent;
		public Vector2 velocity = Vector2.Zero;
		public int Size => elements.Count;
		
		static PackedScene feederArt = ResourceLoader.Load<PackedScene>("res://feeder.tscn");
		
		public Feeder()
		{
			scene = new Node2D();
			art = (Node2D)feederArt.Instantiate();
			scene.AddChild(art);
		}
		
		public void Reset()
		{
			foreach (var child in children) {
				scene.RemoveChild(child.scene);
				child.parent = null;
			}
			children.Clear();
			elements.Clear();
			elements.Add(this);
			art.Position = Vector2.Zero;
			parent = null;
			velocity = Vector2.Zero;
			age = 0;
			availableSeeds = 0;

			// TODO: reset the modulation of the feeder art and stop the tween
		}
		
		public bool TryToSeed(Alga alga)
		{
			if (Size < 3 || availableSeeds <= 0) return false;
			var minSeedDistSquared = minSeedDist * minSeedDist;
			if (scene.GlobalPosition.DistanceSquaredTo(alga.scene.GlobalPosition) > minSeedDistSquared) {
				return false;
			}

			alga.Ripen();
			availableSeeds--;
			if (availableSeeds <= 0) {
				Burst();
			} else {
				foreach (var feeder in elements) {
					feeder.AnimateOpacity(availableSeeds / maxAvailableSeeds);
				}
			}
			return true;
		}
		
		private void Burst()
		{
			var oldPosition = scene.GlobalPosition;
			var artPositions = new List<Vector2>();
			foreach (var feeder in elements) {
				artPositions.Add(feeder.art.GlobalPosition);
			}
			
			var parentNode = scene.GetParent();
			foreach (var child in children) {
				scene.RemoveChild(child.scene);
				child.parent = null;
				parentNode.AddChild(child.scene);
			}
			children.Clear();
			
			for (int i = 0; i < 3; i++) {
				var feeder = elements[i];
				feeder.age = 0;
				feeder.scene.GlobalPosition = artPositions[i];
				feeder.velocity = (artPositions[i] - oldPosition) * 6;
				feeder.art.Position = Vector2.Zero;
			}
			
			foreach (var feeder in elements) {
				feeder.AnimateOpacity(1);
			}
			
			elements.Clear();
			elements.Add(this);
			
			availableSeeds = 0;
		}
		
		public void AnimateOpacity(float amount) {
			// TODO: tween the modulation of the feeder art
		}
		
		public bool TryToCombine(Feeder other)
		{
			if (Size >= 3) return false;
			
			const float minDistSquared = minDist * minDist;
			var otherGlobalPosition = other.art.GlobalPosition;

			foreach (var feeder in elements) {
				if (feeder.art.GlobalPosition.DistanceSquaredTo(otherGlobalPosition) > minDistSquared) {
					return false;
				}
			}

			children.Add(other);
			elements.Add(other);
			other.parent = this;

			velocity = (velocity * (Size - 1) + other.velocity) / Size;
			other.velocity = Vector2.Zero;
			other.age = 0;

			if (Size == 3) availableSeeds = maxAvailableSeeds;

			var averageGlobalPosition = Vector2.Zero;
			var artPositions = new List<Vector2>();
			foreach (var feeder in elements) {
				averageGlobalPosition += feeder.art.GlobalPosition;
				artPositions.Add(feeder.art.GlobalPosition);
			}
			averageGlobalPosition /= Size;

			scene.GlobalPosition = averageGlobalPosition;
			other.scene.GetParent().RemoveChild(other.scene);
			scene.AddChild(other.scene);
			other.scene.Position = Vector2.Zero;

			for (int i = 0; i < Size; i++) {
				elements[i].art.GlobalPosition = artPositions[i];
			}

			return true;
		}
		
		public void Update(float delta)
		{
			if (parent != null) return;
			
			age += delta;
			
			var pushForce = Vector2.Zero;
			if (Game.isMousePressed) {
				var localPushPosition = Game.mousePosition - scene.Position;
				var force = 2000f / localPushPosition.LengthSquared();
				if (force > 0.05) {
					pushForce = -localPushPosition * force;
				}
			}
			
			float mag = 10;
			velocity += pushForce * mag * delta;
			scene.Position += velocity * mag * delta;
			velocity = velocity.Lerp(Vector2.Zero, 0.02f);
			
			// Avoid the edges
			{
				var currentRadius = 50;
				var currentMargin = (Game.screenSize - Vector2.One * (margin + currentRadius)) / 2;
				var goalPosition = scene.Position.Clamp(-currentMargin, currentMargin);
				scene.Position = scene.Position.Lerp(goalPosition, 0.08f);
			}
			
			if (Size == 2) {
				foreach (var feeder in elements) {
					var art = feeder.art;
					var goalPosition = art.Position * (minDist / 2) / art.Position.Length();
					art.Position = art.Position.Lerp(goalPosition, 0.2f);
				}
			} else if (Size == 3) {
				var averagePosition = (
					elements[0].art.Position +
					elements[1].art.Position +
					elements[2].art.Position
				) / 3;
				foreach (var feeder in elements) {
					var art = feeder.art;
					var goalPosition = art.Position - averagePosition;
					goalPosition *= (minDist / 2) / goalPosition.Length();
					art.Position = art.Position.Lerp(goalPosition, 0.2f);
				}
			}
		}
	}

	List<Alga> algae = new List<Alga>();
	List<Forager> foragers = new List<Forager>();
	List<Feeder> feeders = new List<Feeder>();

	private static SceneTree _sceneTree;
	public static Random random = new Random();
	public static Vector2 screenSize;
	public static bool isMousePressed;
	public static Vector2 mousePosition;
	private static Action<Alga> MuckChanged;
	static HashSet<Alga> muckyAlgae = new HashSet<Alga>();
	static bool gameCanEnd = false;
	static bool resetting = false;
	static Node2D fade;

	public override void _Ready()
	{
		screenSize = ((Window)GetViewport()).Size;
		_sceneTree = GetTree();
		MuckChanged += DetectEndgame;
		fade = GetNode<Polygon2D>("FullscreenFade");

		SpawnAlgae();
		SpawnForagers();
		SpawnFeeders();

		var tween = fade.CreateTween()
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(fade, "modulate", new Color(1, 1, 1, 0), 5);
		tween.TweenProperty(fade, "visible", false, 0);
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouse mouseEvent) {
			isMousePressed = (mouseEvent.ButtonMask & MouseButtonMask.Left) == MouseButtonMask.Left;
			mousePosition = GetLocalMousePosition();
			
			foreach (var alga in algae) {
				if (alga.mucky || !isMousePressed) {
					alga.goalPosition = alga.restingPosition;
				} else {
					var localPushPosition = mousePosition - alga.restingPosition;
					float offset = -localPushPosition.Length() / 50;
					offset *= Mathf.Pow(3, offset);
					alga.goalPosition = alga.restingPosition + localPushPosition * offset;
				}
			}
		}
	}

	public override void _Process(Double delta)
	{	
		float fDelta = (float)delta;
		foreach (var feeder in feeders) {
			feeder.Update(fDelta);
		}
		
		var seedingFeeders = new List<Feeder>();

		const float minAge = 3;
		
		for (int i = 0; i < feeders.Count; i++) {
			var feeder = feeders[i];
			if (feeder.parent != null || feeder.age < minAge) continue;
			if (feeder.Size >= 3) {
				seedingFeeders.Add(feeder);
			} else {
				for (int j = i + 1; j < feeders.Count; j++) {
					var other = feeders[j];
					if (other.parent != null || other.age < minAge || feeder.Size + other.Size > 3) continue;
					if (feeder.Size >= other.Size) {
						if (feeder.TryToCombine(other)) break;
					} else {
						if (other.TryToCombine(feeder)) break;
					}
				}
			}
		}
		
		foreach (var alga in algae) {
			alga.scene.Position = alga.scene.Position.Lerp(alga.goalPosition, 0.1f);
			
			if (alga.ripe || alga.occupant != null) continue;
			
			foreach (var feeder in seedingFeeders) {
				if (feeder.Size == 3 && feeder.TryToSeed(alga)) break;
			}
		}
	}

	private void SpawnAlgae()
	{
		List<List<Alga>> grid = new List<List<Alga>>();

		const int numRows = 9, numColumns = 10;
		var spacing = new Vector2(110, 90);
		for (int i = 0; i < numRows; i++) {
			var rowOffset = new Vector2(1 - (numColumns - i % 2), 1 - numRows) / 2;
			var row = new List<Alga>();
			grid.Add(row);
			for (int j = 0; j < numColumns; j++) {
				if (i % 2 == 1 && j == numColumns - 1) {
					row.Add(null);
					continue;
				}
				var alga = new Alga((new Vector2(j, i) + rowOffset) * spacing);
				row.Add(alga);
				algae.Add(alga);
				AddChild(alga.scene);
				alga.Reset();
			}
		}

		void ConnectNeighbors(Alga l1, Alga l2) {
			if (l1 == null || l2 == null) return;
			l1.neighbors.Add(l2);
			l2.neighbors.Add(l1);
		}

		for (int i = 0; i < numRows; i++) {
			for (int j = 0; j < numColumns; j++) {
				var alga = grid[i][j];
				if (alga == null) continue;
				if (j > 0) {
					ConnectNeighbors(alga, grid[i][j - 1]);
				}
				if (i > 0) {
					ConnectNeighbors(alga, grid[i - 1][j]);
					int j2 = j + (i % 2) * 2 - 1;
					if (j2 >= 0) {
						ConnectNeighbors(alga, grid[i - 1][j2]);
					}
				}
			}
		}
	}

	private void SpawnForagers()
	{
		const int numForagers = 2;
		for (int i = 0; i < numForagers; i++) {
			foragers.Add(new Forager());
		}
		ResetForagers();
	}
	
	private void SpawnFeeders()
	{
		const int numFeeders = 7;
		for (int i = 0; i < numFeeders; i++) {
			var feeder = new Feeder();
			feeders.Add(feeder);
		}
		ResetFeeders();
	}

	private void ResetForagers()
	{
		foreach (var forager in foragers) {
			var alga = algae[random.Next(algae.Count)];
			while (alga.occupant != null) {
				alga = algae[random.Next(algae.Count)];
			}
			forager.Reset();
			forager.Place(alga);
		}
	}
	
	private void ResetFeeders()
	{
		foreach (var feeder in feeders)
		{
			feeder.Reset();
			AddChild(feeder.scene);
			feeder.scene.GlobalPosition = new Vector2(
				(float)random.NextDouble() - 0.5f,
				(float)random.NextDouble() - 0.5f
			) * screenSize;
			feeder.velocity = new Vector2(
				(float)random.NextDouble() - 0.5f,
				(float)random.NextDouble() - 0.5f
			) * 200;
		}
	}

	private void DetectEndgame(Alga alga)
	{
		if (resetting) return;

		if (alga.mucky) {
			muckyAlgae.Add(alga);
		} else {
			muckyAlgae.Remove(alga);
		}
		int numMuckyAlgae = muckyAlgae.Count;

		if (gameCanEnd) {
			if (numMuckyAlgae == 0) {
				Reset();
			} else if ((float)numMuckyAlgae / algae.Count > 0.6) {
				Reset();
			}
		} else if (!resetting && numMuckyAlgae >= 3) {
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
			foreach (var alga in algae) {
				alga.Reset();
			}
			ResetForagers();
			ResetFeeders();
			muckyAlgae.Clear();
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
