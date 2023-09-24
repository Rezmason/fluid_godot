using Godot;
using System;

public class Forager
{
	public Node2D scene;
	public Node2D art;
	private Clicker clicker;
	public Alga alga;
	Tween jumpTween;

	static PackedScene foragerArt = ResourceLoader.Load<PackedScene>("res://forager.tscn");

	public Forager(int id)
	{
		scene = new Node2D();
		scene.Name = $"Forager{id}";
		art = (Node2D)foragerArt.Instantiate();
		scene.AddChild(art);
		clicker = new Clicker(art.GetNode<Area2D>("Area2D"), () => alga.SpreadMuck());
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
		Globals.GetTimer(Globals.random.NextDouble() * 1.5f + 0.5f, Jump);
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
