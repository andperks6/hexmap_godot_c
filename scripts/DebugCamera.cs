using Godot;
using System;

public partial class DebugCamera : Camera3D
{
    // This camera code has largely been borrowed from Github user timjonaswechler. Thanks!

    [Export(PropertyHint.Range, "0,10,0.01")]
    public float Sensitivity { get; set; } = 3f;

    [Export(PropertyHint.Range, "0,1000,0.1")]
    public float DefaultVelocity { get; set; } = 50f;

    [Export(PropertyHint.Range, "0,10,0.01")]
    public float SpeedScale { get; set; } = 1.17f;

    [Export(PropertyHint.Range, "1,100,0.1")]
    public float BoostSpeedMultiplier { get; set; } = 3.0f;

    [Export]
    public float MaxSpeed { get; set; } = 10000f;

    [Export]
    public float MinSpeed { get; set; } = 2f;

    private float _velocity;
    public bool Locked { get; set; }

    public override void _Ready()
    {
        _velocity = DefaultVelocity;
    }

    public override void _Process(double delta)
    {
        if (!Current || Locked) return;

        // Get the direction of movement based on user interaction
        var direction = new Vector3(
            Input.IsPhysicalKeyPressed(Key.D) ? 1f : (Input.IsPhysicalKeyPressed(Key.A) ? -1f : 0f),
            Input.IsPhysicalKeyPressed(Key.E) ? 1f : (Input.IsPhysicalKeyPressed(Key.Q) ? -1f : 0f),
            Input.IsPhysicalKeyPressed(Key.S) ? 1f : (Input.IsPhysicalKeyPressed(Key.W) ? -1f : 0f)
        ).Normalized();

        // Apply a boost if the user is pressing shift
        var movement = direction * _velocity * (float)delta;
        if (Input.IsPhysicalKeyPressed(Key.Shift))
        {
            movement *= BoostSpeedMultiplier;
        }

        Translate(movement);
    }

    public override void _Input(InputEvent @event)
    {
        if (!Current || Locked) return;

        if (Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            if (@event is InputEventMouseMotion mouseMotion)
            {
                RotationDegrees = new Vector3(
                    Mathf.Clamp(RotationDegrees.X - mouseMotion.Relative.Y * Sensitivity / 1000f, -90f, 90f),
                    RotationDegrees.Y - mouseMotion.Relative.X * Sensitivity / 1000f,
                    RotationDegrees.Z
                );
            }
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            switch (mouseButton.ButtonIndex)
            {
                case MouseButton.Right:
                    Input.MouseMode = mouseButton.Pressed ? 
                        Input.MouseModeEnum.Captured : 
                        Input.MouseModeEnum.Visible;
                    break;

                case MouseButton.WheelUp: // increase fly velocity
                    _velocity = Mathf.Clamp(_velocity * SpeedScale, MinSpeed, MaxSpeed);
                    break;

                case MouseButton.WheelDown: // decrease fly velocity
                    _velocity = Mathf.Clamp(_velocity / SpeedScale, MinSpeed, MaxSpeed);
                    break;
            }
        }
    }
}