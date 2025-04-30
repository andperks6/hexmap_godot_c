using Godot;
using System;

[GlobalClass]
public partial class HexMapCamera : Node3D
{

    [Export]
    public float StickMinZoom { get; set; } = 6.0f;

    [Export]
    public float StickMaxZoom { get; set; } = 175.0f;

    [Export]
    public float SwivelMinZoom { get; set; } = 0f;

    [Export]
    public float SwivelMaxZoom { get; set; } = -90f;

    [Export]
    public float MovementSpeedMinZoom { get; set; } = 250f;

    [Export]
    public float MovementSpeedMaxZoom { get; set; } = 100f;

    [Export]
    public float RotationSpeed { get; set; } = 180f;

    [Export]
    public HexGrid HexGrid { get; set; }


    private Node3D _swivel;
    private Node3D _stick;
    private Camera3D _mainCamera;

    private float _zoom = 1.0f;
    private float _rotationAngle;

    public bool Locked { get; set; }

    public override void _Ready()
    {
        _swivel = GetNode<Node3D>("Swivel");
        _stick = GetNode<Node3D>("Swivel/Stick");
        _mainCamera = GetNode<Camera3D>("Swivel/Stick/MainCamera");
        HexGrid = GetNode<HexGrid>("../HexGrid");
        ValidatePosition();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_mainCamera.Current || Locked)
            return;

        var leftRightMovement = Input.GetAxis("ui_left", "ui_right");
        var forwardBackMovement = Input.GetAxis("ui_up", "ui_down");

        if (leftRightMovement != 0.0f || forwardBackMovement != 0.0f)
        {
            AdjustPosition(leftRightMovement, forwardBackMovement, (float)delta);
        }

        var rotateLeftRightMovement = Input.GetAxis("ui_rotate_right", "ui_rotate_left");
        if (rotateLeftRightMovement != 0.0f)
        {
            AdjustRotation(rotateLeftRightMovement, (float)delta);
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!_mainCamera.Current || Locked)
            return;

        if (@event is InputEventKey eventKey && eventKey.Pressed)
        {
            if (eventKey.Keycode == Key.Z)
            {
                AdjustZoom(-0.1f);
            }
            else if (eventKey.Keycode == Key.X)
            {
                AdjustZoom(0.1f);
            }
        }
        else if (@event is InputEventMouseButton eventMouseButton)
        {
            if (eventMouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                AdjustZoom(-0.05f);  // Zoom in
            }
            else if (eventMouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                AdjustZoom(0.05f);   // Zoom out
            }
        }
    }

    public void ValidatePosition()
    {
        AdjustPosition(0, 0, 0);
    }

    private void AdjustZoom(float zoomDelta)
    {
        _zoom = Mathf.Clamp(_zoom + zoomDelta, 0.0f, 1.0f);

        var distance = Mathf.Lerp(StickMinZoom, StickMaxZoom, _zoom);
        _stick.Position = new Vector3(0.0f, 0.0f, distance);

        var angle = Mathf.Lerp(SwivelMinZoom, SwivelMaxZoom, _zoom);
        _swivel.RotationDegrees = new Vector3(angle, 0.0f, 0.0f);
    }

    private void AdjustPosition(float xDelta, float zDelta, float timeDelta)
    {
        var direction = Quaternion * new Vector3(xDelta, 0.0f, zDelta).Normalized();
        var damping = Mathf.Max(Mathf.Abs(xDelta), Mathf.Abs(zDelta));
        var movementSpeed = Mathf.Lerp(MovementSpeedMinZoom, MovementSpeedMaxZoom, _zoom);
        var distance = movementSpeed * damping * timeDelta;
        Position += direction * distance;

        Position = HexGrid.Wrapping ? WrapPosition(Position) : ClampPosition(Position);
    }

    private Vector3 ClampPosition(Vector3 pos)
    {
        var xMax = (HexGrid.CellCountX - 0.5f) * HexMetrics.InnerDiameter;
        pos.X = Mathf.Clamp(pos.X, 0.0f, xMax);

        var zMax = (HexGrid.CellCountZ - 1.0f) * (1.5f * HexMetrics.OuterRadius);
        pos.Z = Mathf.Clamp(pos.Z, 0.0f, zMax);

        return pos;
    }

    private Vector3 WrapPosition(Vector3 pos)
    {
        var width = HexGrid.CellCountX * HexMetrics.InnerDiameter;
        while (pos.X < 0.0f)
        {
            pos.X += width;
        }

        while (pos.X > width)
        {
            pos.X -= width;
        }

        var zMax = (HexGrid.CellCountZ - 1.0f) * (1.5f * HexMetrics.OuterRadius);
        pos.Z = Mathf.Clamp(pos.Z, 0.0f, zMax);

        HexGrid.CenterMap(pos.X);
        return pos;
    }

    private void AdjustRotation(float rotationDelta, float timeDelta)
    {
        _rotationAngle += rotationDelta * RotationSpeed * timeDelta;
        if (_rotationAngle < 0.0f)
        {
            _rotationAngle += 360.0f;
        }
        else if (_rotationAngle >= 360.0f)
        {
            _rotationAngle -= 360.0f;
        }

        RotationDegrees = new Vector3(0.0f, _rotationAngle, 0.0f);
    }
}