using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class HexUnit : Node3D
{
    #region Constants

    private const int TravelSpeed = 4;
    private const float RotationSpeed = 5.0f;
    private const int VISION_RANGE = 3;

    #endregion

    #region Public Fields

    public HexGrid HexGrid { get; set; }

    #endregion

    #region Private Fields

    private HexCell _location;
    private HexCell _currentTravelLocation;
    private float _orientation;
    private List<HexCell> _pathToTravel = [];

    #endregion

    #region Public Static Fields

    public static PackedScene UnitPrefab;

    #endregion

    #region Properties

    public HexCell Location
    {
        get => _location;
        set
        {
            if (value != null)
            {
                if (_location != null)
                {
                    HexGrid.DecreaseVisibilityInGame(_location, VisionRange);
                    _location.Unit = null;
                }

                _location = value;
                value.Unit = this;
                HexGrid.IncreaseVisibilityInGame(value, VisionRange);
                Position = value.Position;
                HexGrid.MakeChildOfColumn(this, value.ColumnIndex);
            }
        }
    }

    public float Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            Quaternion = Quaternion.FromEuler(new Vector3(0, value, 0));
        }
    }

    public int Speed => 24;

    public int VisionRange => VISION_RANGE;

    #endregion

    #region Public Methods

    public async void Travel(List<HexCell> path)
    {
        _location.Unit = null;
        _location = path[^1];
        _location.Unit = this;
        _pathToTravel = path;
        await TravelPath();
    }

    public void Die()
    {
        if (Location != null)
        {
            HexGrid.DecreaseVisibilityInGame(Location, VisionRange);
        }

        Location.Unit = null;
        QueueFree();
    }

    public void ValidateLocation()
    {
        Position = Location.Position;
    }

    public void SaveToFile(FileAccess writer)
    {
        Location.HexCoordinates.SaveToFile(writer);
        writer.StoreFloat(Orientation);
    }

    public bool IsValidDestination(HexCell cell)
    {
        return cell.IsExplored && !cell.IsUnderwater && cell.Unit == null;
    }

    public static int GetMoveCost(HexCell fromCell, HexCell toCell, HexDirection direction)
    {
        var edgeType = fromCell.GetEdgeType(toCell);
        if (edgeType == HexEdgeType.Cliff)
        {
            return -1;
        }

        int moveCost;
        if (fromCell.HasRoadThroughEdge(direction))
        {
            moveCost = 1;
        }
        else if (fromCell.Walled != toCell.Walled)
        {
            return -1;
        }
        else
        {
            moveCost = edgeType == HexEdgeType.Flat ? 5 : 10;
            moveCost += toCell.UrbanLevel + toCell.FarmLevel + toCell.PlantLevel;
        }

        return moveCost;
    }

    #endregion

    #region Private Methods

    private async Task LookAt(Vector3 point)
    {
        if (HexMetrics.Wrapping)
        {
            float xDistance = point.X - Position.X;
            if (xDistance < -HexMetrics.InnerRadius * HexMetrics.WrapSize)
            {
                point = point with { X = point.X + HexMetrics.InnerDiameter * HexMetrics.WrapSize };
            }
            else if (xDistance > HexMetrics.InnerRadius * HexMetrics.WrapSize)
            {
                point = point with { X = point.X - HexMetrics.InnerDiameter * HexMetrics.WrapSize };
            }
        }

        point = point with { Y = Position.Y };

        var fromRotation = Quaternion;
        var toRotation = Transform3D.Identity.LookingAt(point - Position).Basis.GetRotationQuaternion();
        float angle = fromRotation.AngleTo(toRotation);
        float rotationSpeed = RotationSpeed / Mathf.Max(angle, 0.01f);

        if (angle > 0.0f)
        {
            float t = 0;
            while (t < 1.0f)
            {
                Quaternion = fromRotation.Slerp(toRotation, t);
                float deltaTime = await WaitForNextFrame();
                t += deltaTime * rotationSpeed;
            }
        }

        await LookAt(point);
        _orientation = RotationDegrees.Y;
    }

    private async Task<float> WaitForNextFrame()
    {
        ulong startUs = Time.GetTicksUsec();
        await ToSignal(GetTree(), "process_frame");
        ulong endUs = Time.GetTicksUsec();
        long elapsedUs = (long)(endUs - startUs);
        float elapsedMs = elapsedUs / 1000.0f;
        return elapsedMs / 1000.0f;
    }

    private async Task TravelPath()
    {
        var a = _pathToTravel[0].Position;
        var b = a;
        var c = a;

        Position = c;
        await LookAt(_pathToTravel[1].Position);

        if (_currentTravelLocation == null)
        {
            _currentTravelLocation = _pathToTravel[0];
        }
        HexGrid.DecreaseVisibilityInGame(_currentTravelLocation, VisionRange);
        int currentColumn = _currentTravelLocation.ColumnIndex;

        float t = 0.01f;
        for (int i = 1; i < _pathToTravel.Count; i++)
        {
            _currentTravelLocation = _pathToTravel[i];

            a = c;
            b = _pathToTravel[i - 1].Position;

            int nextColumn = _currentTravelLocation.ColumnIndex;
            if (currentColumn != nextColumn)
            {
                if (nextColumn < currentColumn - 1)
                {
                    a = a with { X = a.X - HexMetrics.InnerDiameter * HexMetrics.WrapSize };
                    b = b with { X = b.X - HexMetrics.InnerDiameter * HexMetrics.WrapSize };
                }
                else if (nextColumn > currentColumn + 1)
                {
                    a = a with { X = a.X + HexMetrics.InnerDiameter * HexMetrics.WrapSize };
                    b = b with { X = b.X + HexMetrics.InnerDiameter * HexMetrics.WrapSize };
                }

                HexGrid.MakeChildOfColumn(this, nextColumn);
                currentColumn = nextColumn;
            }

            c = (b + _currentTravelLocation.Position) * 0.5f;
            HexGrid.IncreaseVisibilityInGame(_pathToTravel[i], VisionRange);

            while (t < 1.0f)
            {
                var bezierPoint = Bezier.GetPoint(a, b, c, t);
                Position = bezierPoint;

                var d = Bezier.GetDerivative(a, b, c, t);
                d = d with { Y = 0 };
                Orientation = Transform3D.Identity.LookingAt(d, Vector3.Up).Basis.GetEuler().Y;

                float elapsedSec = await WaitForNextFrame();
                t += elapsedSec * TravelSpeed;
            }

            HexGrid.DecreaseVisibilityInGame(_pathToTravel[i], VisionRange);
            t -= 1.0f;
        }

        _currentTravelLocation = null;

        a = c;
        b = Location.Position;
        c = b;

        HexGrid.IncreaseVisibilityInGame(Location, VisionRange);

        while (t < 1.0f)
        {
            Position = Bezier.GetPoint(a, b, c, t);

            var d = Bezier.GetDerivative(a, b, c, t);
            d = d with { Y = 0 };
            Orientation = Transform3D.Identity.LookingAt(d, Vector3.Up).Basis.GetEuler().Y;

            float elapsedSec = await WaitForNextFrame();
            t += elapsedSec * TravelSpeed;
        }

        Position = Location.Position;
        _pathToTravel.Clear();
    }

    #endregion

    #region Static Methods

    public static void LoadFromFile(FileAccess reader, HexGrid grid)
    {
        var coordinates = HexCoordinates.LoadFromFile(reader);
        float hexOrientation = reader.GetFloat();

        var unit = (HexUnit)UnitPrefab.Instantiate();
        grid.AddUnit(unit, grid.GetCellFromCoordinates(coordinates), hexOrientation);
    }

    #endregion
}