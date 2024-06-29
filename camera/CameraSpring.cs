using Godot;
using System;

public partial class CameraSpring : SpringArm3D
{
	[Export] private Camera3D camera;
	float xx = 0;
	float yy = 0;
	float zoom = 32;

    public override void _Input(InputEvent @event)
    {
        if(@event is InputEventMouseMotion eventMouseMotion  && Input.IsActionPressed("drag"))
		{
			float x = eventMouseMotion.Relative.Y * 0.5f * (float)GetProcessDeltaTime();
			float y = eventMouseMotion.Relative.X * 0.5f * (float)GetProcessDeltaTime();
			xx -= x;
			xx = Mathf.Clamp(xx,-Mathf.Pi/2,Mathf.Pi/2);
			yy -= y;
		}
    }
    public override void _PhysicsProcess(double delta)
    {
        Quaternion = new Quaternion(Vector3.Up, yy) * new Quaternion(Vector3.Right, xx);

		SpringLength = Mathf.Lerp(SpringLength,zoom,(float)delta*10);
		if(Input.IsActionPressed("scroll_up"))
		{
			zoom -= 1;
			zoom = Mathf.Clamp(zoom, 8,512);
		}
		if(Input.IsActionPressed("scroll_down"))
		{
			zoom += 1;
			zoom = Mathf.Clamp(zoom, 8,256);
		}
    }
}
