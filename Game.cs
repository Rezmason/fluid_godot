using Godot;
using System;
using System.Collections.Generic;

public partial class Game : Node2D
{
	private class Lilypad {
		public Node2D scene;
		public Node2D alga;
		public Node2D muck;
		public Label label;
		public HashSet<Lilypad> neighbors = new HashSet<Lilypad>();

		static PackedScene algaArt = (PackedScene)ResourceLoader.Load("res://alga.tscn");
		static PackedScene muckArt = (PackedScene)ResourceLoader.Load("res://muck.tscn");

		public bool fed = false;
		public bool mucked = false;

		public Lilypad() {
			scene = new Node2D();
			muck = (Node2D)muckArt.Instantiate();
			scene.AddChild(muck);
			muck.Visible = false;
			alga = (Node2D)algaArt.Instantiate();
			scene.AddChild(alga);
			label = new Label();
			label.LabelSettings = new LabelSettings{FontColor = new Color("black")};
			scene.AddChild(label);
		}
	}

	List<List<Lilypad>> lilypads = new List<List<Lilypad>>();

	public override void _Ready()
	{
		const int numRows = 9;
		int[] numColumns = {10, 9};
		var spacing = new Vector2(110, 90);
		for (int i = 0; i < numRows; i++) {
			var rowOffset = new Vector2(1 - numColumns[i % 2], 1 - numRows) / 2;
			var row = new List<Lilypad>();
			lilypads.Add(row);
			for (int j = 0; j < numColumns[i % 2]; j++) {
				var lilypad = new Lilypad();
				lilypad.scene.Position = (new Vector2(j, i) + rowOffset) * spacing;
				row.Add(lilypad);
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
				ConnectNeighbors(lilypads[i][j], lilypads[i][j - 1]);
				if (i > 0) {
					ConnectNeighbors(lilypads[i][j], lilypads[i - 1][j - ((i + 1) % 2)]);
				}
				if (i < numRows - 1) {
					ConnectNeighbors(lilypads[i][j], lilypads[i + 1][j - ((i + 1) % 2)]);
				}
			}
		}
	}

	public override void _Process(double delta)
	{

	}
}
