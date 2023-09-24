using System;
using Godot;
using System.Collections.Generic;

public class Feeder
{
	private Color minSeedColor = Colors.Transparent;
	private Color maxSeedColor = Colors.White;
	
	public const int maxAvailableSeeds = 40;
	const float minSeedDist = 100;
	const float minDist = 80;
	const float margin = 50;
	public float age;
	public float availableSeeds;
	public Node2D scene;
	public Node2D art;
	public Node2D fill;
	private List<Feeder> children = new List<Feeder>();
	public List<Feeder> elements = new List<Feeder>();
	public Feeder parent;
	public Vector2 velocity = Vector2.Zero;
	public int Size => elements.Count;
	private Tween opacityTween;
	
	static PackedScene feederArt = ResourceLoader.Load<PackedScene>("res://feeder.tscn");
	
	public Feeder(int id)
	{
		scene = new Node2D();
		scene.Name = $"Feeder{id}";
		art = (Node2D)feederArt.Instantiate();
		scene.AddChild(art);
		fill = art.GetNode<Node2D>("Fill");
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

		if (opacityTween != null) {
			opacityTween.Stop();
			opacityTween = null;
		}
		fill.Set("modulate", maxSeedColor);
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
		opacityTween = art.CreateTween()
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
		
		var targetColor = minSeedColor.Lerp(maxSeedColor, amount);
		opacityTween.TweenProperty(fill, "modulate", targetColor, 0.1f);
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
		other.scene.Rotation = 0;

		for (int i = 0; i < Size; i++) {
			elements[i].art.GlobalPosition = artPositions[i];
		}

		return true;
	}
	
	public void Update(float delta)
	{
		if (parent != null) return;
		
		age += delta;

		var oldPosition = scene.Position;
		
		var pushForce = Vector2.Zero;
		if (Globals.isMousePressed) {
			var localPushPosition = Globals.mousePosition - scene.Position;
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
			var currentMargin = (Globals.screenSize - Vector2.One * (margin + currentRadius)) / 2;
			var goalPosition = scene.Position.Clamp(-currentMargin, currentMargin);
			scene.Position = scene.Position.Lerp(goalPosition, 0.08f);
		}

		scene.GlobalRotation += ((oldPosition.X + oldPosition.Y) - (scene.Position.X + scene.Position.Y)) / Size * 0.005f;
		
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
