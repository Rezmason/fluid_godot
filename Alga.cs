using Godot;
using System;
using System.Collections.Generic;

public class Alga 
{
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
			Globals.MuckChanged(this);
		}
	}

	private void WaitToSpreadMuck()
	{
		Globals.GetTimer(Globals.random.NextDouble() * 3 + 1, () => {
			if (!mucky) return;
			if (Globals.random.NextDouble() < 0.25) SpreadMuck();
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
		Globals.MuckChanged(this);
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
		return candidates[Globals.random.Next(candidates.Count)];
	}
}
