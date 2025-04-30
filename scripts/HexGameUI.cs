using Godot;


[GlobalClass]
public partial class HexGameUI : Node3D
{
    [Signal]
    public delegate void EditModeEnabledEventHandler();

    [Export]
    public HexGrid Grid { get; set; }

    private bool _enabled;
    private HexCell _currentCell;
    private HexUnit _selectedUnit;

    public override void _Ready()
    {
        Grid = GetNode<HexGrid>("../../HexGrid");

    }

    public override void _Process(double delta)
    {
        if (_selectedUnit != null)
        {
            DoPathfinding();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_enabled) return;

        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                DoSelection();
            }
            else if (mouseEvent.ButtonIndex == MouseButton.Right)
            {
                DoMove();
            }
        }
    }

    public void Enable()
    {
        if (!_enabled)
        {
            _enabled = true;
            GetNode<CanvasLayer>("CanvasLayer").Visible = true;
        }
    }

    public void Disable()
    {
        if (_enabled)
        {
            _enabled = false;
            GetNode<CanvasLayer>("CanvasLayer").Visible = false;
        }
    }

    private void DoMove()
    {
        if (!Grid.HasPath) return;

        _selectedUnit.Travel(Grid.GetUnitPath());
        Grid.ClearPath();
    }

    private void DoPathfinding()
    {
        if (!UpdateCurrentCell()) return;

        if (_currentCell != null && _selectedUnit != null && _selectedUnit.IsValidDestination(_currentCell))
        {
            Grid.FindPath(_selectedUnit.Location, _currentCell, _selectedUnit);
        }
        else
        {
            Grid.ClearPath();
            _selectedUnit.Location.EnableHighlight(Colors.Blue);
        }
    }

    private void DoSelection()
    {
        UpdateCurrentCell();

        if (_currentCell == null) return;

        if (_selectedUnit != null)
        {
            _selectedUnit.Location.DisableHighlight();
        }

        _selectedUnit = _currentCell.Unit;

        if (_selectedUnit != null)
        {
            _selectedUnit.Location.EnableHighlight(Colors.Blue);
        }
    }

    private bool UpdateCurrentCell()
    {
        const float RayLength = 1000f;
        var mousePos = GetViewport().GetMousePosition();
        var camera = GetViewport().GetCamera3D();
        var origin = camera.ProjectRayOrigin(mousePos);
        var end = origin + camera.ProjectRayNormal(mousePos) * RayLength;

        var rayQuery = PhysicsRayQueryParameters3D.Create(origin, end);
        rayQuery.CollideWithAreas = true;

        var resultCell = Grid.GetCellFromRay(rayQuery);
        if (resultCell != _currentCell)
        {
            _currentCell = resultCell;
            return true;
        }

        return false;
    }

    private void OnEnableEditModeButtonPressed()
    {
        EmitSignal(SignalName.EditModeEnabled);
    }
}