using Godot;
using System;
using System.Collections.Generic;

public partial class Game : Node2D
{
	HashSet<Alga> muckyAlgae = new HashSet<Alga>();
	bool gameCanEnd = false;
	bool resetting = false;
	Node2D fade;
	ShaderMaterial feederMetaballs;

	List<Alga> algae = new List<Alga>();
	List<Forager> foragers = new List<Forager>();
	List<Feeder> feeders = new List<Feeder>();

	Color[] metaballData = new Color[10];
	Color[] metaballGroupData = new Color[3];

	public override void _Ready()
	{
		Globals.Init(this);
		Globals.MuckChanged += DetectEndgame;
		fade = GetNode<Polygon2D>("FullscreenFade");
		feederMetaballs = (ShaderMaterial)GetNode<CanvasItem>("FeederMetaballs").Material;

		SpawnAlgae();
		SpawnForagers();
		SpawnFeeders();

		var emptyColor = new Color(Colors.Black, 0);
		for (int i = 0; i < 10; i++) {
			metaballData[i] = emptyColor;
		}
		for (int i = 0; i < 3; i++) {
			metaballGroupData[i] = emptyColor;
		}

		var tween = fade.CreateTween()
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(fade, "modulate", new Color(1, 1, 1, 0), 5);
		tween.TweenProperty(fade, "visible", false, 0);
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouse mouseEvent) {
			Globals.isMousePressed = (mouseEvent.ButtonMask & MouseButtonMask.Left) == MouseButtonMask.Left;
			Globals.mousePosition = GetLocalMousePosition();
			
			foreach (var alga in algae) {
				if (alga.mucky || !Globals.isMousePressed) {
					alga.goalPosition = alga.restingPosition;
				} else {
					var localPushPosition = Globals.mousePosition - alga.restingPosition;
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

		int n = 0;
		int f = 1;
		ulong now = Time.GetTicksMsec();
		metaballGroupData[0] = new Color(1, 0, 0, 0);
		foreach (var feeder in feeders) {
			if (feeder.parent != null) continue;
			int groupID = 0;
			float throb = 0;
			float throbTime = 0;
			if (feeder.availableSeeds > 0) {
				groupID = f;
				float opacity = feeder.availableSeeds / Feeder.maxAvailableSeeds;
				opacity = 1 - Mathf.Pow(1 - opacity, 2);
				metaballGroupData[groupID] = new Color(opacity, 0, 0, 0);
				f++;
				throb = 7;
				throbTime = (float)(now - feeder.throbStartTime) / 1000;
			}
			int i = 0;
			foreach (var element in feeder.elements) {
				var position = element.art.GlobalPosition;
				metaballData[n] = new Color(position.X, position.Y, 15 + throb * (Mathf.Sin((i * (float)Math.PI * 2 / 3) + throbTime * 4) * 0.5f + 0.5f), groupID);
				n++;
				i++;
			}
		}

		feederMetaballs.SetShaderParameter("metaballs", metaballData);
		feederMetaballs.SetShaderParameter("metaballGroups", metaballGroupData);
		
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
			for (int j = 0; j < numColumns; j++) {
				if (i % 2 == 1 && j == numColumns - 1) {
					row.Add(null);
					continue;
				}
				var alga = new Alga(grid.Count, row.Count, (new Vector2(j, i) + rowOffset) * spacing);
				row.Add(alga);
				algae.Add(alga);
				AddChild(alga.scene);
				alga.Reset();
			}
			grid.Add(row);
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
			foragers.Add(new Forager(foragers.Count));
		}
		ResetForagers();
	}
	
	private void SpawnFeeders()
	{
		const int numFeeders = 7;
		for (int i = 0; i < numFeeders; i++) {
			var feeder = new Feeder(feeders.Count);
			feeders.Add(feeder);
		}
		ResetFeeders();
	}

	private void ResetForagers()
	{
		foreach (var forager in foragers) {
			var alga = algae[Globals.random.Next(algae.Count)];
			while (alga.occupant != null) {
				alga = algae[Globals.random.Next(algae.Count)];
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
				(float)Globals.random.NextDouble() - 0.5f,
				(float)Globals.random.NextDouble() - 0.5f
			) * Globals.screenSize;
			feeder.velocity = new Vector2(
				(float)Globals.random.NextDouble() - 0.5f,
				(float)Globals.random.NextDouble() - 0.5f
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
}
