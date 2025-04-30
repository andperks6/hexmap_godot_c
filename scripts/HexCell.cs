using Godot;

[GlobalClass]
public partial class HexCell : Node3D
{
    [Signal]
    public delegate void CellChangedEventHandler();

    [Export] public Node3D CellContent { get; set; }
    [Export] public Label3D CellInformationLabel { get; set; }
    [Export] public Sprite3D CellSelectionOutline { get; set; }

    private CellInformationLabelMode _cellLabelMode = CellInformationLabelMode.Off;
    private int _terrainTypeIndex;
    private int _elevation = -32767;
    private bool _hasIncomingRiver;
    private bool _hasOutgoingRiver;
    private HexDirection _incomingRiverDirection = HexDirection.NE;
    private HexDirection _outgoingRiverDirection = HexDirection.NE;
    private readonly bool[] _roads = new bool[6];
    private int _waterLevel;
    private int _urbanLevel;
    private int _farmLevel;
    private int _plantLevel;
    private bool _walled;
    private int _specialIndex;
    private int _distance;
    private int _visibilityInGame;
    private bool _isExplored;

    public HexGridChunk HexChunk { get; set; }
    public HexCoordinates HexCoordinates { get; set; }
    public HexCell[] HexNeighbors { get; } = new HexCell[6];
    public HexCell PathFrom { get; set; }
    public int SearchHeuristic { get; set; }
    public HexCell NextWithSamePriority { get; set; }
    public int SearchPhase { get; set; }
    public HexUnit Unit { get; set; }
    public HexCellShaderData ShaderData { get; set; }
    public int Index { get; set; }
    public bool Explorable { get; set; } = true;
    public int ColumnIndex { get; set; }

    public CellInformationLabelMode CellLabelMode
    {
        get => _cellLabelMode;
        set
        {
            _cellLabelMode = value;
            switch (_cellLabelMode)
            {
                case CellInformationLabelMode.Off:
                    CellInformationLabel.Visible = false;
                    break;
                case CellInformationLabelMode.Position:
                    CellInformationLabel.Visible = true;
                    CellInformationLabel.FontSize = 32;
                    CellInformationLabel.Text = HexCoordinates.ToString();
                    break;
                case CellInformationLabelMode.Information:
                    CellInformationLabel.Visible = true;
                    CellInformationLabel.FontSize = 128;
                    break;
            }
        }
    }

    public int Elevation
    {
        get => _elevation;
        set
        {
            if (_elevation == value) return;

            int originalViewElevation = ViewElevation;
            _elevation = value;
            // Execute the function within the shader data that handles when the view
            // elevation has changed
            if (ViewElevation != originalViewElevation)
                ShaderData.ViewElevationChanged();

            RefreshPosition();
            ValidateRivers();

            for (int i = 0; i < _roads.Length; i++)
            {
                //Check to see if there is a large elevation difference, clear road if there is

                if (_roads[i] && GetElevationDifference((HexDirection)i) > 1)
                    SetRoad(i, false);
            }

            Refresh();
        }
    }

    public int ViewElevation => Elevation >= WaterLevel ? Elevation : WaterLevel;

    public Color HexColor => HexMetrics.GetColor(_terrainTypeIndex);

    public int TerrainTypeIndex
    {
        get => _terrainTypeIndex;
        set
        {
            if (_terrainTypeIndex != value)
            {
                _terrainTypeIndex = value;
                ShaderData.RefreshTerrain(this);
            }
        }
    }

    public bool HasIncomingRiver => _hasIncomingRiver;
    public bool HasOutgoingRiver => _hasOutgoingRiver;
    public HexDirection IncomingRiverDirection => _incomingRiverDirection;
    public HexDirection OutgoingRiverDirection => _outgoingRiverDirection;
    public bool HasRiver => _hasIncomingRiver || _hasOutgoingRiver;
    public bool HasRiverBeginningOrEnd => _hasIncomingRiver != _hasOutgoingRiver;
    public float StreamBedY => (_elevation + HexMetrics.StreamBedElevationOffset) * HexMetrics.ElevationStep;
    public float RiverSurfaceY => (_elevation + HexMetrics.RiverSurfaceElevationOffset) * HexMetrics.ElevationStep;
    public bool HasRoads => System.Array.Exists(_roads, road => road);
    public HexDirection RiverBeginOrEndDirection => HasIncomingRiver ? IncomingRiverDirection : OutgoingRiverDirection;

    public int WaterLevel
    {
        get => _waterLevel;
        set
        {
            if (_waterLevel == value) return;

            int originalViewElevation = ViewElevation;
            _waterLevel = value;

            if (ViewElevation != originalViewElevation)
                ShaderData.ViewElevationChanged();

            ValidateRivers();
            Refresh();
        }
    }

    public bool IsUnderwater => _waterLevel > _elevation;
    public float WaterSurfaceY => (_waterLevel + HexMetrics.WaterElevationOffset) * HexMetrics.ElevationStep;

    public int UrbanLevel
    {
        get => _urbanLevel;
        set
        {
            if (_urbanLevel != value)
            {
                _urbanLevel = value;
                RefreshSelfOnly();
            }
        }
    }

    public int FarmLevel
    {
        get => _farmLevel;
        set
        {
            if (_farmLevel != value)
            {
                _farmLevel = value;
                RefreshSelfOnly();
            }
        }
    }

    public int PlantLevel
    {
        get => _plantLevel;
        set
        {
            if (_plantLevel != value)
            {
                _plantLevel = value;
                RefreshSelfOnly();
            }
        }
    }

    public bool Walled
    {
        get => _walled;
        set
        {
            if (_walled != value)
            {
                _walled = value;
                Refresh();
            }
        }
    }

    public int SpecialIndex
    {
        get => _specialIndex;
        set
        {
            if (_specialIndex != value && !HasRiver)
            {
                _specialIndex = value;
                RemoveRoads();
                RefreshSelfOnly();
            }
        }
    }

    public bool IsSpecial => _specialIndex > 0;

    public int Distance
    {
        get => _distance;
        set => _distance = value;
    }

    public int SearchPriority => Distance + SearchHeuristic;

    public bool IsVisibleInGame => _visibilityInGame > 0 && Explorable;

    public bool IsExplored
    {
        get => _isExplored && Explorable;
        set => _isExplored = value;
    }

    public HexCell GetNeighbor(HexDirection direction) => HexNeighbors[(int)direction];

    public void SetNeighbor(HexDirection direction, HexCell cell)
    {
        HexNeighbors[(int)direction] = cell;
        cell.HexNeighbors[(int)direction.Opposite()] = this;
    }

    public HexEdgeType GetEdgeType(HexDirection direction) =>
        HexMetrics.GetEdgeType(Elevation, HexNeighbors[(int)direction].Elevation);

    public HexEdgeType GetEdgeType(HexCell otherCell) =>
        HexMetrics.GetEdgeType(Elevation, otherCell.Elevation);

    public bool HasRiverThroughEdge(HexDirection direction) =>
        (_hasIncomingRiver && _incomingRiverDirection == direction) ||
        (_hasOutgoingRiver && _outgoingRiverDirection == direction);

    public void RemoveOutgoingRiver()
    {
        if (!_hasOutgoingRiver) return;

        _hasOutgoingRiver = false;
        RefreshSelfOnly();

        HexCell neighbor = GetNeighbor(_outgoingRiverDirection);
        neighbor._hasIncomingRiver = false;
        neighbor.RefreshSelfOnly();
    }

    public void RemoveIncomingRiver()
    {
        if (!_hasIncomingRiver) return;

        _hasIncomingRiver = false;
        RefreshSelfOnly();

        HexCell neighbor = GetNeighbor(_incomingRiverDirection);
        neighbor._hasOutgoingRiver = false;
        neighbor.RefreshSelfOnly();
    }

    public void RemoveRiver()
    {
        RemoveOutgoingRiver();
        RemoveIncomingRiver();
    }

    public void SetOutgoingRiver(HexDirection direction)
    {
        if (_hasOutgoingRiver && _outgoingRiverDirection == direction) return;

        HexCell neighbor = GetNeighbor(direction);
        if (!IsValidRiverDestination(neighbor)) return;

        RemoveOutgoingRiver();
        if (_hasIncomingRiver && _incomingRiverDirection == direction)
            RemoveIncomingRiver();

        _hasOutgoingRiver = true;
        _outgoingRiverDirection = direction;
        //  no special feature exists
        _specialIndex = 0;

        neighbor.RemoveIncomingRiver();
        neighbor._hasIncomingRiver = true;
        neighbor._incomingRiverDirection = direction.Opposite();
        neighbor._specialIndex = 0;

        SetRoad((int)direction, false);
    }

    public bool HasRoadThroughEdge(HexDirection direction) => _roads[(int)direction];

    public void AddRoad(HexDirection direction)
    {
        if (!_roads[(int)direction] && !HasRiverThroughEdge(direction) &&
            !IsSpecial && !GetNeighbor(direction).IsSpecial &&
            GetElevationDifference(direction) <= 1)
        {
            SetRoad((int)direction, true);
        }
    }

    public void RemoveRoads()
    {
        for (int i = 0; i < _roads.Length; i++)
        {
            if (_roads[i])
                SetRoad(i, false);
        }
    }

    public void SetRoad(int index, bool state)
    {
        _roads[index] = state;
        HexDirection oppositeDirection = ((HexDirection)index).Opposite();
        HexNeighbors[index]._roads[(int)oppositeDirection] = state;
        HexNeighbors[index].RefreshSelfOnly();
        RefreshSelfOnly();
    }

    public int GetElevationDifference(HexDirection direction)
    {
        int difference = _elevation - GetNeighbor(direction).Elevation;
        return difference >= 0 ? difference : -difference;
    }

    public void SaveHexCell(FileAccess writer)
    {
        // Cast to byte only at storage boundary, keeping fields as int for compatibility
        writer.Store8((byte)_terrainTypeIndex);
        writer.Store8((byte)(_elevation + 127));
        writer.Store8((byte)_waterLevel);
        writer.Store8((byte)_urbanLevel);
        writer.Store8((byte)_farmLevel);
        writer.Store8((byte)_plantLevel);
        writer.Store8((byte)_specialIndex);
        writer.Store8((byte)(_walled ? 1 : 0));

        writer.Store8((byte)(_hasIncomingRiver ? (int)_incomingRiverDirection + 128 : 0));
        writer.Store8((byte)(_hasOutgoingRiver ? (int)_outgoingRiverDirection + 128 : 0));

        int roadFlags = 0;
        for (int i = 0; i < _roads.Length; i++)
        {
            if (_roads[i])
                roadFlags |= 1 << i;
        }
        writer.Store8((byte)roadFlags);

        writer.Store8((byte)(IsExplored ? 1 : 0));
    }

    public void LoadHexCell(FileAccess reader, int fileVersion)
    {
        _terrainTypeIndex = reader.Get8();
        ShaderData.RefreshTerrain(this);

        _elevation = fileVersion >= 4 ? reader.Get8() - 127 : reader.Get8();
        RefreshPosition();

        _waterLevel = reader.Get8();
        _urbanLevel = reader.Get8();
        _farmLevel = reader.Get8();
        _plantLevel = reader.Get8();
        _specialIndex = reader.Get8();
        _walled = reader.Get8() != 0;

        int incomingRiverInfo = reader.Get8();
        if (incomingRiverInfo >= 128)
        {
            _hasIncomingRiver = true;
            _incomingRiverDirection = (HexDirection)(incomingRiverInfo - 128);
        }
        else
            _hasIncomingRiver = false;

        int outgoingRiverInfo = reader.Get8();
        if (outgoingRiverInfo >= 128)
        {
            _hasOutgoingRiver = true;
            _outgoingRiverDirection = (HexDirection)(outgoingRiverInfo - 128);
        }
        else
            _hasOutgoingRiver = false;

        int roadFlags = reader.Get8();
        for (int i = 0; i < _roads.Length; i++)
        {
            _roads[i] = (roadFlags & (1 << i)) != 0;
        }

        IsExplored = fileVersion >= 3 && reader.Get8() != 0;
        ShaderData.RefreshVisibility(this);
    }

    public void EnableHighlight(Color highlightColor)
    {
        CellSelectionOutline.Visible = true;
        CellSelectionOutline.Modulate = highlightColor;
    }

    public void DisableHighlight()
    {
        CellSelectionOutline.Visible = false;
    }

    public void SetLabel(string text)
    {
        CellInformationLabel.Text = text;
    }

    public void IncreaseVisibilityInGame()
    {
        _visibilityInGame += 1;
        if (_visibilityInGame == 1)
        {
            IsExplored = true;
            ShaderData.RefreshVisibility(this);
        }
    }

    public void DecreaseVisibilityInGame()
    {
        _visibilityInGame -= 1;
        if (_visibilityInGame <= 0)
            ShaderData.RefreshVisibility(this);
    }

    public void ResetVisibility()
    {
        if (_visibilityInGame > 0)
        {
            _visibilityInGame = 0;
            ShaderData.RefreshVisibility(this);
        }
    }

    public void SetMapData(float data)
    {
        ShaderData.SetMapData(this, data);
    }

    private void RefreshSelfOnly()
    {
        if (HexChunk != null)
            HexChunk.RequestRefresh();

        if (Unit != null)
            Unit.ValidateLocation();
    }

    public void Refresh()
    {
        if (HexChunk != null)
        {
            HexChunk.RequestRefresh();

            for (int i = 0; i < HexNeighbors.Length; i++)
            {
                HexCell neighbor = HexNeighbors[i];
                if (neighbor != null && neighbor.HexChunk != HexChunk)
                    neighbor.HexChunk.RequestRefresh();
            }

            if (Unit != null)
                Unit.ValidateLocation();
        }
    }

    private void RefreshPosition()
    {
        Position = Position with { Y = _elevation * HexMetrics.ElevationStep };

        Vector4 noise = HexMetrics.SampleNoise(Position);
        float perturbation = (noise.Y * 2f - 1f) * HexMetrics.ElevationPerturbStrength;
        Position = Position with { Y = Position.Y + perturbation };
        // Set the y-axis position of the "position label" for the cell
        CellContent.Position = new Vector3(0f, 0.1f + Mathf.Abs(perturbation), 0f);
    }

    private bool IsValidRiverDestination(HexCell neighbor)
    {
        return neighbor != null && (Elevation >= neighbor.Elevation || WaterLevel == neighbor.Elevation);
    }

    private void ValidateRivers()
    {
        if (HasOutgoingRiver && !IsValidRiverDestination(GetNeighbor(OutgoingRiverDirection)))
            RemoveOutgoingRiver();

        if (HasIncomingRiver && !GetNeighbor(IncomingRiverDirection).IsValidRiverDestination(this))
            RemoveIncomingRiver();
    }
}