using Godot;
using System;

public class Clicker
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
