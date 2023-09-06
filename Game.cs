using Godot;
using System;
using System.Collections.Generic;

public partial class Game : Node2D
{
	private class Alga {
		public Node2D scene;
		public bool fed = false;
		public bool mucked = false;
		public List<Alga> neighbors = new List<Alga>();

		public Alga(Node2D scene) {
			this.scene = scene;
		}
	}

	List<Alga> algae = new List<Alga>();

	public override void _Ready()
	{
		var algaScene = ResourceLoader.Load("res://alga.tscn") as PackedScene;
		
		for (int i = 0; i < 10; i++) {
			for (int j = 0; j < 10; j++) {
				var alga = new Alga(algaScene.Instantiate() as Node2D);
				algae.Add(alga);
				alga.scene.Position = new Vector2(i * 20, j * 20);
				this.AddChild(alga.scene);
			}
		}
	}

	public override void _Process(double delta)
	{

	}
}
