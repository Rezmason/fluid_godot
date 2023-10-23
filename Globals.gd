using Godot;
using System;

public static class Globals
{
	public static Random random = new Random();
	public static Vector2 screenSize;
	public static bool isMousePressed;
	public static Vector2 mousePosition;
	public static Action<Alga> MuckChanged;

	static SceneTree sceneTree;

	public static void Init(Node2D basis)
	{
		screenSize = ((Window)basis.GetViewport()).Size;
		sceneTree = basis.GetTree();
	}

	public static SceneTreeTimer GetTimer(double timeSec, Action action)
	{
		var timer = sceneTree.CreateTimer(timeSec);
		timer.Timeout += action;

		return timer;
	}
}
