using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class HexGrid : Node3D
{
    [Export] public PackedScene UnitPrefab { get; set; }
    [Export] public Color[] HexColors { get; set; } = {
        Colors.Yellow,
        Colors.Green,
        Colors.Blue,
        Colors.Orange,
        Colors.White
    };
    [Export] public Texture2D[] HexTextures { get; set; }
    [Export] public int CellCountX { get; set; } = 20;
    [Export] public int CellCountZ { get; set; } = 15;
    [Export] public bool Wrapping { get; set; } = false;
    [Export] public PackedScene HexCellPrefab { get; set; }
    [Export] public ShaderMaterial TexturedTerrainShaderMaterial { get; set; }
    [Export] public ShaderMaterial RiverShaderMaterial { get; set; }
    [Export] public ShaderMaterial RoadShaderMaterial { get; set; }
    [Export] public ShaderMaterial WaterShaderMaterial { get; set; }
    [Export] public ShaderMaterial WaterShoreShaderMaterial { get; set; }
    [Export] public ShaderMaterial EstuariesShaderMaterial { get; set; }
    [Export] public ShaderMaterial WallsMaterial { get; set; }

    private readonly List<HexGridChunk> _hexGridChunks = new();
    private readonly List<HexCell> _hexCells = new();
    private readonly List<HexUnit> _units = new();
    private readonly List<Node3D> _columns = new();

    public IReadOnlyList<HexUnit> Units => _units;
    
    private int _chunkCountX = 2;
    private int _chunkCountZ = 2;
    private bool _hexGridOverlayEnabled;
    private HexCellPriorityQueue _searchFrontier;
    private int _searchFrontierPhase;
    private HexCell _currentPathFrom;
    private HexCell _currentPathTo;
    private bool _currentPathExists;
    private HexCellShaderData _cellShaderData;
    private int _currentCenterColumnIndex = -1;

    public bool HexGridOverlayEnabled
    {
        get => _hexGridOverlayEnabled;
        set
        {
            if (_hexGridOverlayEnabled != value)
            {
                _hexGridOverlayEnabled = value;
                TexturedTerrainShaderMaterial?.SetShaderParameter("grid_on", _hexGridOverlayEnabled);
            }
        }
    }

    public bool HasPath => _currentPathExists;

    public override void _Ready()
    {
        // Set the colors on the HexMetrics class
        HexMetrics.Colors = HexColors;

        // Initialize the Perlin noise
        HexMetrics.InitializeNoiseGenerator();

        // Initialize the hash grid
        HexMetrics.InitializeHashGrid();

        // Set the unit prefab on the HexUnit class
        HexUnit.UnitPrefab = UnitPrefab;

        // Create the map
        CreateMap(CellCountX, CellCountZ, Wrapping);
    }

    public override void _Process(double delta)
    {
        foreach (var chunk in _hexGridChunks)
        {
            if (chunk.UpdateNeeded)
            {
                chunk.Refresh();
            }
        }

        _cellShaderData?.LateUpdate();
    }

    public bool CreateMap(int mapSizeX, int mapSizeZ, bool shouldWrap)
    {
        GD.Print($"Map size: {mapSizeX} x {mapSizeZ}");
        // Return immediately if this is an unsupported map size
        if (mapSizeX <= 0 || mapSizeX % HexMetrics.ChunkSizeX != 0 ||
            mapSizeZ <= 0 || mapSizeZ % HexMetrics.ChunkSizeZ != 0)
        {
            return false;
        }

        // Clear any existing path
        ClearPath();

        // Clear any existing units
        ClearUnits();

        // Destroy existing columns
        if (_columns.Count > 0)
        {
            foreach (var column in _columns)
            {
                RemoveChild(column);
            }
            _columns.Clear();
        }

        // Set the cell counts in each axis direction
        CellCountX = mapSizeX;
        CellCountZ = mapSizeZ;

        // Set the wrapping status
        Wrapping = shouldWrap;

        // Set the HexMetrics wrap size
        HexMetrics.WrapSize = Wrapping ? CellCountX : 0;

        // Set the center column index
        _currentCenterColumnIndex = -1;

        // Calculate the chunk count in the x and z directions
        _chunkCountX = CellCountX / HexMetrics.ChunkSizeX;
        _chunkCountZ = CellCountZ / HexMetrics.ChunkSizeZ;

        if (_cellShaderData == null)
        {
            _cellShaderData = new HexCellShaderData();
        }
        _cellShaderData.Initialize(CellCountX, CellCountZ);
        _cellShaderData.HexGrid = this;

        CreateChunks();
        CreateCells();

        foreach (var chunk in _hexGridChunks)
        {
            chunk.SetTerrainMeshMaterial(TexturedTerrainShaderMaterial);
            chunk.SetRiversMeshMaterial(RiverShaderMaterial);
            chunk.SetRoadMeshMaterial(RoadShaderMaterial);
            chunk.SetWaterMeshMaterial(WaterShaderMaterial);
            chunk.SetWaterShoreMeshMaterial(WaterShoreShaderMaterial);
            chunk.SetEstuariesMeshMaterial(EstuariesShaderMaterial);
            chunk.SetWallsMeshMaterial(WallsMaterial);
            chunk.Refresh();
            chunk.RequestRefresh(); // TODO: Investigate why double refresh is needed
        }

        return true;
    }

    public HexCell GetCellFromIndex(int index)
    {
        return _hexCells[index];
    }

    public HexCell GetCellFromRay(PhysicsRayQueryParameters3D rayQuery)
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        var result = spaceState.IntersectRay(rayQuery);
        if (result != null && result.Count > 0)
        {
            return GetCell((Vector3)result["position"]);
        }
        return null;
    }

    public HexCell GetCell(Vector3 position)
    {
        position = position * GlobalTransform;
        var coordinates = HexCoordinates.FromPosition(position);
        return GetCellFromCoordinates(coordinates);
    }

    public HexCell GetCellFromCoordinates(HexCoordinates coordinates)
    {
        var z = coordinates.Z;
        if (z < 0 || z >= CellCountZ)
        {
            return null;
        }

        var x = coordinates.X + z / 2;
        if (x < 0 || x >= CellCountX)
        {
            return null;
        }

        return _hexCells[x + z * CellCountX];
    }

    public HexCell GetCellFromOffset(int xOffset, int zOffset)
    {
        return _hexCells[xOffset + zOffset * CellCountX];
    }

    public void AddUnit(HexUnit unit, HexCell location, float orientation)
    {
        _units.Add(unit);
        unit.HexGrid = this;
        AddChild(unit);
        unit.Location = location;
        unit.Orientation = orientation;
    }

    public void RemoveUnit(HexUnit unit)
    {
        _units.Remove(unit);
        unit.Die();
    }

    public void ClearPath()
    {
        if (_currentPathExists)
        {
            var current = _currentPathTo;
            while (current != _currentPathFrom)
            {
                current.SetLabel("");
                current.DisableHighlight();
                current = current.PathFrom;
            }
            current.DisableHighlight();
            _currentPathExists = false;
        }
        else if (_currentPathFrom != null)
        {
            _currentPathFrom.DisableHighlight();
            _currentPathTo.DisableHighlight();
        }

        _currentPathFrom = null;
        _currentPathTo = null;
    }

    public List<HexCell> GetUnitPath()
    {
        if (!_currentPathExists)
        {
            return new List<HexCell>();
        }

        var path = new List<HexCell>();
        var current = _currentPathTo;
        while (current != _currentPathFrom)
        {
            path.Add(current);
            current = current.PathFrom;
        }
        path.Add(_currentPathFrom);
        path.Reverse();
        return path;
    }

    public void SetAllCellLabelModes(CellInformationLabelMode labelMode)
    {
        foreach (var cell in _hexCells)
        {
            cell.CellLabelMode = labelMode;
        }
    }

    public void DisableAllCellHighlights()
    {
        foreach (var cell in _hexCells)
        {
            cell.DisableHighlight();
        }
    }

    public void ResetAllCellDistances()
    {
        foreach (var cell in _hexCells)
        {
            cell.Distance = GodotConstants.MaxInt;
        }
    }

    public void ResetAllCellLabels()
    {
        foreach (var cell in _hexCells)
        {
            cell.SetLabel("");
        }
    }

    public void Save(FileAccess fileWriter)
    {
        fileWriter.Store32((uint)CellCountX);
        fileWriter.Store32((uint)CellCountZ);
        fileWriter.Store8(Wrapping ? (byte)1 : (byte)0);

        foreach (var cell in _hexCells)
        {
            cell.SaveHexCell(fileWriter);
        }

        fileWriter.Store32((uint)_units.Count);
        foreach (var unit in _units)
        {
            unit.SaveToFile(fileWriter);
        }
    }

    public void Load(FileAccess fileReader, int fileVersion)
    {
        ClearPath();
        ClearUnits();

        int newMapSizeX = 20;
        int newMapSizeZ = 15;

        if (fileVersion >= 1)
        {
            newMapSizeX = (int)fileReader.Get32();
            newMapSizeZ = (int)fileReader.Get32();
        }

        bool shouldWrap = false;
        if (fileVersion >= 5)
        {
            shouldWrap = fileReader.Get8() != 0;
        }

        if (newMapSizeX != CellCountX || newMapSizeZ != CellCountZ || Wrapping != shouldWrap)
        {
            if (!CreateMap(newMapSizeX, newMapSizeZ, shouldWrap))
            {
                return;
            }
        }

        foreach (var cell in _hexCells)
        {
            cell.LoadHexCell(fileReader, fileVersion);
        }

        foreach (var cell in _hexCells)
        {
            cell.Refresh();
        }

        if (fileVersion >= 2)
        {
            int unitCount = (int)fileReader.Get32();
            for (int i = 0; i < unitCount; i++)
            {
                HexUnit.LoadFromFile(fileReader, this);
            }
        }
    }

    public void IncreaseVisibilityInGame(HexCell fromCell, int visibilityRange)
    {
        var cells = DijkstraGetVisibleCells(fromCell, visibilityRange);
        foreach (var cell in cells)
        {
            cell.IncreaseVisibilityInGame();
        }
    }

    public void DecreaseVisibilityInGame(HexCell fromCell, int visibilityRange)
    {
        var cells = DijkstraGetVisibleCells(fromCell, visibilityRange);
        foreach (var cell in cells)
        {
            cell.DecreaseVisibilityInGame();
        }
    }

    public void ResetVisibility()
    {
        foreach (var cell in _hexCells)
        {
            cell.ResetVisibility();
        }

        foreach (var unit in _units)
        {
            IncreaseVisibilityInGame(unit.Location, unit.VisionRange);
        }
    }

    public void MakeChildOfColumn(Node3D child, int columnIndex)
    {
        child.Reparent(_columns[columnIndex]);
    }

    public void CenterMap(float xPosition)
    {
        int centerColumnIndex = (int)(xPosition / (HexMetrics.InnerDiameter * HexMetrics.ChunkSizeX));
        
        if (centerColumnIndex == _currentCenterColumnIndex)
        {
            return;
        }

        _currentCenterColumnIndex = centerColumnIndex;

        int minColumnIndex = centerColumnIndex - (_chunkCountX / 2);
        int maxColumnIndex = centerColumnIndex + (_chunkCountX / 2);

        Vector3 position = Vector3.Zero;
        for (int i = 0; i < _columns.Count; i++)
        {
            if (i < minColumnIndex)
            {
                position.X = _chunkCountX * (HexMetrics.InnerDiameter * HexMetrics.ChunkSizeX);
            }
            else if (i > maxColumnIndex)
            {
                position.X = _chunkCountX * -(HexMetrics.InnerDiameter * HexMetrics.ChunkSizeX);
            }
            else
            {
                position.X = 0f;
            }

            _columns[i].Position = position;
        }
    }

    private List<HexCell> DijkstraGetVisibleCells(HexCell fromCell, int visibilityRange)
    {
        var cells = new List<HexCell>();
        
        _searchFrontierPhase += 2;
        if (_searchFrontier == null)
        {
            _searchFrontier = new HexCellPriorityQueue();
        }
        else
        {
            _searchFrontier.Clear();
        }

        visibilityRange += fromCell.ViewElevation;
        fromCell.SearchPhase = _searchFrontierPhase;
        fromCell.Distance = 0;
        _searchFrontier.Enqueue(fromCell);

        var fromCoordinates = fromCell.HexCoordinates;

        while (_searchFrontier.Count > 0)
        {
            var current = _searchFrontier.Dequeue();
            current.SearchPhase += 1;

            cells.Add(current);

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                var neighbor = current.GetNeighbor(d);
                if (neighbor == null || neighbor.SearchPhase > _searchFrontierPhase || !neighbor.Explorable)
                {
                    continue;
                }

                int distance = current.Distance + 1;
                if ((distance + neighbor.ViewElevation) > visibilityRange ||
                    distance > fromCoordinates.DistanceTo(neighbor.HexCoordinates))
                {
                    continue;
                }

                if (neighbor.SearchPhase < _searchFrontierPhase)
                {
                    neighbor.SearchPhase = _searchFrontierPhase;
                    neighbor.Distance = distance;
                    neighbor.SearchHeuristic = 0;
                    _searchFrontier.Enqueue(neighbor);
                }
                else if (distance < neighbor.Distance)
                {
                    int oldPriority = neighbor.SearchPriority;
                    neighbor.Distance = distance;
                    _searchFrontier.Change(neighbor, oldPriority);
                }
            }
        }

        return cells;
    }

    public void FindPath(HexCell fromCell, HexCell toCell, HexUnit unit)
    {
        if (fromCell == null || toCell == null)
        {
            return;
        }

        ClearPath();
        _currentPathFrom = fromCell;
        _currentPathTo = toCell;
        _currentPathExists = DijkstraSearchFromTo(fromCell, toCell, unit);
        ShowPath(unit.Speed);
    }

    private void CreateChunks()
    {
        _columns.Clear();
        for (int x = 0; x < _chunkCountX; x++)
        {
            var column = new Node3D();
            _columns.Add(column);
            AddChild(column);
        }

        _hexGridChunks.Clear();
        for (int z = 0; z < _chunkCountZ; z++)
        {
            for (int x = 0; x < _chunkCountX; x++)
            {
                var chunk = new HexGridChunk();
                _hexGridChunks.Add(chunk);
                _columns[x].AddChild(chunk);
            }
        }
    }

    private void CreateCells()
    {
        _hexCells.Clear();
        for (int z = 0, i = 0; z < CellCountZ; z++)
        {
            for (int x = 0; x < CellCountX; x++)
            {
                CreateCell(z, x, i++);
            }
        }
    }

    private void CreateCell(int z, int x, int i)
    {
        var position = new Vector3(
            (x + z * 0.5f - z / 2) * HexMetrics.InnerDiameter,
            0f,
            z * (HexMetrics.OuterRadius * 1.5f)
        );

        var cell = HexCellPrefab.Instantiate<HexCell>();

        if (x > 0)
        {
            cell.SetNeighbor(HexDirection.W, _hexCells[i - 1]);
            if (Wrapping && x == CellCountX - 1)
            {
                cell.SetNeighbor(HexDirection.E, _hexCells[i - x]);
            }
        }

        if (z > 0)
        {
            if ((z & 1) == 0)
            {
                cell.SetNeighbor(HexDirection.SE, _hexCells[i - CellCountX]);
                if (x > 0)
                {
                    cell.SetNeighbor(HexDirection.SW, _hexCells[i - CellCountX - 1]);
                }
                else if (Wrapping)
                {
                    cell.SetNeighbor(HexDirection.SW, _hexCells[i - 1]);
                }
            }
            else
            {
                cell.SetNeighbor(HexDirection.SW, _hexCells[i - CellCountX]);
                if (x < CellCountX - 1)
                {
                    cell.SetNeighbor(HexDirection.SE, _hexCells[i - CellCountX + 1]);
                }
                else if (Wrapping)
                {
                    cell.SetNeighbor(HexDirection.SE, _hexCells[i - CellCountX * 2 + 1]);
                }
            }
        }

        cell.Position = position;
        cell.Elevation = 0;
        cell.HexCoordinates = HexCoordinates.FromOffsetCoordinates(x, z);
        cell.Index = i;
        cell.ColumnIndex = x / HexMetrics.ChunkSizeX;
        cell.ShaderData = _cellShaderData;

        if (Wrapping)
        {
            cell.Explorable = z > 0 && z < CellCountZ - 1;
        }
        else
        {
            cell.Explorable = x > 0 && z > 0 && x < CellCountX - 1 && z < CellCountZ - 1;
        }

        cell.Distance = GodotConstants.MaxInt;
        cell.CellLabelMode = CellInformationLabelMode.Position;

        _hexCells.Add(cell);
        AddCellToChunk(x, z, cell);
    }

    private void AddCellToChunk(int x, int z, HexCell cell)
    {
        int chunkX = x / HexMetrics.ChunkSizeX;
        int chunkZ = z / HexMetrics.ChunkSizeZ;

        var chunk = _hexGridChunks[chunkX + chunkZ * _chunkCountX];

        int localX = x - chunkX * HexMetrics.ChunkSizeX;
        int localZ = z - chunkZ * HexMetrics.ChunkSizeZ;

        chunk.AddCell(localX + localZ * HexMetrics.ChunkSizeX, cell);
    }

    private void ShowPath(int speed)
    {
        if (_currentPathExists)
        {
            var current = _currentPathTo;
            while (current != _currentPathFrom)
            {
                int turn = (current.Distance - 1) / speed;
                current.SetLabel(turn.ToString());
                current.EnableHighlight(Colors.White);
                current = current.PathFrom;
            }
        }

        _currentPathFrom.EnableHighlight(Colors.Blue);
        _currentPathTo.EnableHighlight(Colors.Red);
    }

    private bool DijkstraSearchFromTo(HexCell fromCell, HexCell toCell, HexUnit unit)
    {
        int speed = unit.Speed;
        _searchFrontierPhase += 2;

        if (_searchFrontier == null)
        {
            _searchFrontier = new HexCellPriorityQueue();
        }
        else
        {
            _searchFrontier.Clear();
        }

        fromCell.EnableHighlight(Colors.Blue);
        fromCell.SearchPhase = _searchFrontierPhase;
        fromCell.Distance = 0;
        _searchFrontier.Enqueue(fromCell);

        while (_searchFrontier.Count > 0)
        {
            var current = _searchFrontier.Dequeue();
            current.SearchPhase += 1;

            if (current == toCell)
            {
                return true;
            }

            int currentTurn = (current.Distance - 1) / speed;

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                var neighbor = current.GetNeighbor(d);
                if (neighbor == null || neighbor.SearchPhase > _searchFrontierPhase)
                {
                    continue;
                }

                if (!unit.IsValidDestination(neighbor))
                {
                    continue;
                }

                int moveCost = HexUnit.GetMoveCost(current, neighbor, d);
                if (moveCost < 0)
                {
                    continue;
                }

                int distance = current.Distance + moveCost;
                int turn = (distance - 1) / speed;
                if (turn > currentTurn)
                {
                    distance = turn * speed + moveCost;
                }

                if (neighbor.SearchPhase < _searchFrontierPhase)
                {
                    neighbor.SearchPhase = _searchFrontierPhase;
                    neighbor.Distance = distance;
                    neighbor.PathFrom = current;
                    neighbor.SearchHeuristic = neighbor.HexCoordinates.DistanceTo(toCell.HexCoordinates);
                    _searchFrontier.Enqueue(neighbor);
                }
                else if (distance < neighbor.Distance)
                {
                    int oldPriority = neighbor.SearchPriority;
                    neighbor.Distance = distance;
                    neighbor.PathFrom = current;
                    _searchFrontier.Change(neighbor, oldPriority);
                }
            }
        }

        return false;
    }

    private void ClearUnits()
    {
        foreach (var unit in _units)
        {
            unit.Die();
        }
        _units.Clear();
    }
}