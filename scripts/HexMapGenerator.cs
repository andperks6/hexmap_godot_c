using Godot;
using System;
using System.Collections.Generic;

public partial class HexMapGenerator : Node
{
    #region Nested Types

    public enum HemisphereMode
    {
        Both,
        North,
        South
    }

    private class MapRegion
    {
        public int XMin { get; set; }
        public int XMax { get; set; }
        public int ZMin { get; set; }
        public int ZMax { get; set; }
    }

    private class ClimateData
    {
        public float Clouds { get; set; }
        public float Moisture { get; set; }
    }

    private class Biome
    {
        public int Terrain { get; set; }
        public int Plant { get; set; }

        public Biome(int terrainType, int plantType)
        {
            Terrain = terrainType;
            Plant = plantType;
        }
    }

    #endregion

    #region Static Data

    private static readonly float[] TemperatureBands = { 0.1f, 0.3f, 0.6f };
    private static readonly float[] MoistureBands = { 0.12f, 0.28f, 0.85f };
    private static readonly Biome[] Biomes =
    {
        new(0, 0), new(4, 0), new(4, 0), new(4, 0),
        new(0, 0), new(2, 0), new(2, 1), new(2, 2),
        new(0, 0), new(1, 0), new(1, 1), new(1, 2),
        new(0, 0), new(1, 1), new(1, 2), new(1, 3)
    };

    #endregion

    #region Private Fields

    private readonly RandomNumberGenerator _rng = new();
    private int _cellCount;
    private int _landCells;
    private readonly HexCellPriorityQueue _searchFrontier = new();
    private int _searchFrontierPhase;
    private readonly List<MapRegion> _regions = new();
    private List<ClimateData> _climate = new();
    private List<ClimateData> _nextClimate = new();
    private readonly List<HexDirection> _flowDirections = new();
    private int _temperatureJitterChannel;

    #endregion

    #region Exported Properties

    [Export] public float JitterProbability { get; set; } = 0.25f;
    [Export] public int ChunkSizeMin { get; set; } = 30;
    [Export] public int ChunkSizeMax { get; set; } = 100;
    [Export] public int LandPercentage { get; set; } = 50;
    [Export] public int WaterLevel { get; set; } = 3;
    [Export] public float HighRiseProbability { get; set; } = 0.25f;
    [Export] public float SinkProbability { get; set; } = 0.2f;
    [Export] public int ElevationMinimum { get; set; } = -2;
    [Export] public int ElevationMaximum { get; set; } = 8;
    [Export] public int MapBorderX { get; set; } = 5;
    [Export] public int MapBorderZ { get; set; } = 5;
    [Export] public int RegionBorder { get; set; } = 5;
    [Export] public int RegionCount { get; set; } = 1;
    [Export] public int ErosionPercentage { get; set; } = 50;
    [Export] public float EvaporationFactor { get; set; } = 0.5f;
    [Export] public float PrecipitationFactor { get; set; } = 0.25f;
    [Export] public float RunoffFactor { get; set; } = 0.25f;
    [Export] public float SeepageFactor { get; set; } = 0.125f;
    [Export] public float WindStrength { get; set; } = 4.0f;
    [Export] public float StartingMoisture { get; set; } = 0.1f;
    [Export] public int RiverPercentage { get; set; } = 10;
    [Export] public float ExtraLakeProbability { get; set; } = 0.25f;
    [Export] public float LowTemperature { get; set; }
    [Export] public float HighTemperature { get; set; } = 1.0f;
    [Export] public float TemperatureJitter { get; set; } = 0.1f;
    [Export] public HemisphereMode hemisphereMode { get; set; } = HemisphereMode.Both;

    #endregion

    #region Public Properties

    public HexDirection WindDirection { get; set; } = HexDirection.NW;
    public HexGrid HexGrid { get; set; }

    #endregion

    #region Public Methods

    public void GenerateMap(int x, int z, bool wrapping)
    {
        _rng.Seed = 0;
        _cellCount = x * z;

        HexGrid.CreateMap(x, z, wrapping);

        for (int i = 0; i < _cellCount; i++)
        {
            HexGrid.GetCellFromIndex(i).WaterLevel = WaterLevel;
        }

        CreateRegions();
        CreateLand();
        ErodeLand();
        CreateClimate();
        CreateRivers();
        SetTerrainType();

        for (int i = 0; i < _cellCount; i++)
        {
            HexGrid.GetCellFromIndex(i).SearchPhase = 0;
        }
    }

    #endregion

    #region Private Methods

    private HexCell GetRandomCell(MapRegion region)
    {
        int randomCellX = _rng.RandiRange(region.XMin, region.XMax - 1);
        int randomCellZ = _rng.RandiRange(region.ZMin, region.ZMax - 1);
        return HexGrid.GetCellFromOffset(randomCellX, randomCellZ);
    }

    private void CreateRegions()
    {
        _regions.Clear();
        int borderX = HexGrid.Wrapping ? RegionBorder : MapBorderX;

        switch (RegionCount)
        {
            case 2:
                if (_rng.Randf() < 0.5f)
                {
                    CreateTwoRegionsVertical(borderX);
                }
                else
                {
                    CreateTwoRegionsHorizontal(borderX);
                }
                break;
            case 3:
                CreateThreeRegions(borderX);
                break;
            case 4:
                CreateFourRegions(borderX);
                break;
            default:
                CreateSingleRegion(borderX);
                break;
        }
    }

    private void CreateSingleRegion(int borderX)
    {
        if (HexGrid.Wrapping)
        {
            borderX = 0;
        }

        _regions.Add(new MapRegion
        {
            XMin = borderX,
            XMax = HexGrid.CellCountX - borderX,
            ZMin = MapBorderZ,
            ZMax = HexGrid.CellCountZ - MapBorderZ
        });
    }

    private void CreateTwoRegionsVertical(int borderX)
    {
        _regions.Add(new MapRegion
        {
            XMin = borderX,
            XMax = HexGrid.CellCountX / 2 - RegionBorder,
            ZMin = MapBorderZ,
            ZMax = HexGrid.CellCountZ - MapBorderZ
        });

        _regions.Add(new MapRegion
        {
            XMin = HexGrid.CellCountX / 2 + RegionBorder,
            XMax = HexGrid.CellCountX - borderX,
            ZMin = MapBorderZ,
            ZMax = HexGrid.CellCountZ - MapBorderZ
        });
    }

    private void CreateTwoRegionsHorizontal(int borderX)
    {
        if (HexGrid.Wrapping)
        {
            borderX = 0;
        }

        _regions.Add(new MapRegion
        {
            XMin = borderX,
            XMax = HexGrid.CellCountX - borderX,
            ZMin = MapBorderZ,
            ZMax = HexGrid.CellCountZ / 2 - RegionBorder
        });

        _regions.Add(new MapRegion
        {
            XMin = borderX,
            XMax = HexGrid.CellCountX - borderX,
            ZMin = HexGrid.CellCountZ / 2 + RegionBorder,
            ZMax = HexGrid.CellCountZ - MapBorderZ
        });
    }
    private void CreateThreeRegions(int borderX)
    {
        _regions.Add(new MapRegion
        {
            XMin = borderX,
            XMax = HexGrid.CellCountX / 3 - RegionBorder,
            ZMin = MapBorderZ,
            ZMax = HexGrid.CellCountZ - MapBorderZ
        });

        _regions.Add(new MapRegion
        {
            XMin = HexGrid.CellCountX / 3 + RegionBorder,
            XMax = 2 * HexGrid.CellCountX / 3 - RegionBorder,
            ZMin = MapBorderZ,
            ZMax = HexGrid.CellCountZ - MapBorderZ
        });

        _regions.Add(new MapRegion
        {
            XMin = 2 * HexGrid.CellCountX / 3 + RegionBorder,
            XMax = HexGrid.CellCountX - borderX,
            ZMin = MapBorderZ,
            ZMax = HexGrid.CellCountZ - MapBorderZ
        });
    }

    private void CreateFourRegions(int borderX)
    {
        _regions.Add(new MapRegion
        {
            XMin = borderX,
            XMax = HexGrid.CellCountX / 2 - RegionBorder,
            ZMin = MapBorderZ,
            ZMax = HexGrid.CellCountZ / 2 - RegionBorder
        });

        _regions.Add(new MapRegion
        {
            XMin = HexGrid.CellCountX / 2 + RegionBorder,
            XMax = HexGrid.CellCountX - borderX,
            ZMin = MapBorderZ,
            ZMax = HexGrid.CellCountZ / 2 - RegionBorder
        });

        _regions.Add(new MapRegion
        {
            XMin = HexGrid.CellCountX / 2 + RegionBorder,
            XMax = HexGrid.CellCountX - borderX,
            ZMin = HexGrid.CellCountZ / 2 + RegionBorder,
            ZMax = HexGrid.CellCountZ - MapBorderZ
        });

        _regions.Add(new MapRegion
        {
            XMin = borderX,
            XMax = HexGrid.CellCountX / 2 - RegionBorder,
            ZMin = HexGrid.CellCountZ / 2 + RegionBorder,
            ZMax = HexGrid.CellCountZ - MapBorderZ
        });
    }

    private void CreateLand()
    {
        int landBudget = Mathf.RoundToInt(_cellCount * LandPercentage * 0.01f);
        _landCells = landBudget;

        for (int guard = 0; guard < 10000; guard++)
        {
            bool shouldSink = _rng.Randf() < SinkProbability;

            foreach (var region in _regions)
            {
                int chunkSize = _rng.RandiRange(ChunkSizeMin, ChunkSizeMax);
                if (shouldSink)
                {
                    landBudget = SinkTerrain(chunkSize, landBudget, region);
                }
                else
                {
                    landBudget = RaiseTerrain(chunkSize, landBudget, region);
                    if (landBudget == 0)
                    {
                        return;
                    }
                }
            }
        }

        if (landBudget > 0)
        {
            GD.PrintErr($"Failed to use up {landBudget} land budget.");
            _landCells -= landBudget;
        }
    }

    private int RaiseTerrain(int chunkSize, int budget, MapRegion region)
    {
        _searchFrontierPhase++;
        HexCell firstCell = GetRandomCell(region);
        firstCell.SearchPhase = _searchFrontierPhase;
        firstCell.Distance = 0;
        firstCell.SearchHeuristic = 0;
        _searchFrontier.Enqueue(firstCell);

        HexCoordinates centerCoordinates = firstCell.HexCoordinates;

        int rise = _rng.Randf() < HighRiseProbability ? 2 : 1;
        int size = 0;

        while (size < chunkSize && _searchFrontier.Count > 0)
        {
            HexCell current = _searchFrontier.Dequeue();
            int originalElevation = current.Elevation;
            int newElevation = originalElevation + rise;

            if (newElevation > ElevationMaximum)
                continue;

            current.Elevation = newElevation;
            if (originalElevation < WaterLevel && newElevation >= WaterLevel)
            {
                budget--;
                if (budget == 0)
                    break;
            }

            size++;

            for (HexDirection d = 0; d <= HexDirection.NW; d++)
            {
                HexCell neighbor = current.GetNeighbor(d);
                if (neighbor == null || neighbor.SearchPhase >= _searchFrontierPhase)
                    continue;

                neighbor.SearchPhase = _searchFrontierPhase;
                neighbor.Distance = neighbor.HexCoordinates.DistanceTo(centerCoordinates);
                neighbor.SearchHeuristic = _rng.Randf() < JitterProbability ? 1 : 0;
                _searchFrontier.Enqueue(neighbor);
            }
        }

        _searchFrontier.Clear();
        return budget;
    }

    private int SinkTerrain(int chunkSize, int budget, MapRegion region)
    {
        _searchFrontierPhase++;
        HexCell firstCell = GetRandomCell(region);
        firstCell.SearchPhase = _searchFrontierPhase;
        firstCell.Distance = 0;
        firstCell.SearchHeuristic = 0;
        _searchFrontier.Enqueue(firstCell);

        HexCoordinates centerCoordinates = firstCell.HexCoordinates;

        int sink = _rng.Randf() < HighRiseProbability ? 2 : 1;
        int size = 0;

        while (size < chunkSize && _searchFrontier.Count > 0)
        {
            HexCell current = _searchFrontier.Dequeue();
            int originalElevation = current.Elevation;
            int newElevation = current.Elevation - sink;

            if (newElevation < ElevationMinimum)
                continue;

            current.Elevation = newElevation;
            if (originalElevation >= WaterLevel && newElevation < WaterLevel)
            {
                budget++;
            }

            size++;

            for (HexDirection d = 0; d <= HexDirection.NW; d++)
            {
                HexCell neighbor = current.GetNeighbor(d);
                if (neighbor == null || neighbor.SearchPhase >= _searchFrontierPhase)
                    continue;

                neighbor.SearchPhase = _searchFrontierPhase;
                neighbor.Distance = neighbor.HexCoordinates.DistanceTo(centerCoordinates);
                neighbor.SearchHeuristic = _rng.Randf() < JitterProbability ? 1 : 0;
                _searchFrontier.Enqueue(neighbor);
            }
        }

        _searchFrontier.Clear();
        return budget;
    }
    private void ErodeLand()
    {
        List<HexCell> erodibleCells = new();

        for (int i = 0; i < _cellCount; i++)
        {
            HexCell cell = HexGrid.GetCellFromIndex(i);
            if (IsErodible(cell))
            {
                erodibleCells.Add(cell);
            }
        }

        int targetErodibleCount = (int)(erodibleCells.Count * (100 - ErosionPercentage) * 0.01f);

        while (erodibleCells.Count > targetErodibleCount)
        {
            int index = _rng.RandiRange(0, erodibleCells.Count - 1);
            HexCell cell = erodibleCells[index];
            HexCell targetCell = GetErosionTarget(cell);

            cell.Elevation--;
            targetCell.Elevation++;

            if (!IsErodible(cell))
            {
                erodibleCells.RemoveAt(index);
            }

            for (HexDirection d = 0; d <= HexDirection.NW; d++)
            {
                HexCell neighbor = cell.GetNeighbor(d);
                if (neighbor != null &&
                    neighbor.Elevation == cell.Elevation + 2 &&
                    !erodibleCells.Contains(neighbor))
                {
                    erodibleCells.Add(neighbor);
                }
            }

            if (IsErodible(targetCell) && !erodibleCells.Contains(targetCell))
            {
                erodibleCells.Add(targetCell);
            }

            for (HexDirection d = 0; d <= HexDirection.NW; d++)
            {
                HexCell neighbor = targetCell.GetNeighbor(d);
                if (neighbor != null &&
                    neighbor != cell &&
                    neighbor.Elevation == targetCell.Elevation + 1 &&
                    !IsErodible(neighbor))
                {
                    erodibleCells.Remove(neighbor);
                }
            }
        }
    }

    private bool IsErodible(HexCell cell)
    {
        int erodibleElevation = cell.Elevation - 2;
        for (HexDirection d = 0; d <= HexDirection.NW; d++)
        {
            HexCell neighbor = cell.GetNeighbor(d);
            if (neighbor != null && neighbor.Elevation <= erodibleElevation)
            {
                return true;
            }
        }
        return false;
    }

    private HexCell GetErosionTarget(HexCell cell)
    {
        List<HexCell> candidates = new();
        int erodibleElevation = cell.Elevation - 2;

        for (HexDirection d = 0; d <= HexDirection.NW; d++)
        {
            HexCell neighbor = cell.GetNeighbor(d);
            if (neighbor != null && neighbor.Elevation <= erodibleElevation)
            {
                candidates.Add(neighbor);
            }
        }

        return candidates[_rng.RandiRange(0, candidates.Count - 1)];
    }

    private void CreateClimate()
    {
        _climate.Clear();
        _nextClimate.Clear();

        for (int i = 0; i < _cellCount; i++)
        {
            _climate.Add(new ClimateData { Moisture = StartingMoisture });
            _nextClimate.Add(new ClimateData());
        }

        for (int cycle = 0; cycle < 40; cycle++)
        {
            for (int i = 0; i < _cellCount; i++)
            {
                EvolveClimate(i);
            }

            (_climate, _nextClimate) = (_nextClimate, _climate);
        }
    }

    private void EvolveClimate(int cellIndex)
    {
        HexCell cell = HexGrid.GetCellFromIndex(cellIndex);
        ClimateData cellClimate = _climate[cellIndex];

        if (cell.IsUnderwater)
        {
            cellClimate.Clouds += EvaporationFactor;
        }
        else
        {
            float evaporation = cellClimate.Moisture * EvaporationFactor;
            cellClimate.Moisture -= evaporation;
            cellClimate.Clouds += evaporation;
        }

        float precipitation = cellClimate.Clouds * PrecipitationFactor;
        cellClimate.Clouds -= precipitation;
        cellClimate.Moisture += precipitation;

        float cloudMaximum = 1f - (float)cell.ViewElevation / (ElevationMaximum + 1);
        if (cellClimate.Clouds > cloudMaximum)
        {
            cellClimate.Moisture += cellClimate.Clouds - cloudMaximum;
            cellClimate.Clouds = cloudMaximum;
        }

        HexDirection mainDispersalDirection = WindDirection.Opposite();
        float cloudDispersal = cellClimate.Clouds * (1f / (5f + WindStrength));
        float runoff = cellClimate.Moisture * RunoffFactor * (1f / 6f);
        float seepage = cellClimate.Moisture * SeepageFactor * (1f / 6f);

        for (HexDirection d = 0; d <= HexDirection.NW; d++)
        {
            HexCell neighbor = cell.GetNeighbor(d);
            if (neighbor == null)
                continue;

            ClimateData neighborClimate = _nextClimate[neighbor.Index];
            if (d == mainDispersalDirection)
            {
                neighborClimate.Clouds += cloudDispersal * WindStrength;
            }
            else
            {
                neighborClimate.Clouds += cloudDispersal;
            }

            int elevationDelta = neighbor.ViewElevation - cell.ViewElevation;
            if (elevationDelta < 0)
            {
                cellClimate.Moisture -= runoff;
                neighborClimate.Moisture += runoff;
            }
            else if (elevationDelta == 0)
            {
                cellClimate.Moisture -= seepage;
                neighborClimate.Moisture += seepage;
            }

            _nextClimate[neighbor.Index] = neighborClimate;
        }

        cellClimate.Clouds = 0;

        ClimateData nextCellClimate = _nextClimate[cellIndex];
        nextCellClimate.Moisture += Mathf.Min(cellClimate.Moisture, 1f);
        _nextClimate[cellIndex] = nextCellClimate;
        _climate[cellIndex] = new ClimateData();
    }
    private void CreateRivers()
    {
        List<HexCell> riverOrigins = new();

        for (int i = 0; i < _cellCount; i++)
        {
            HexCell cell = HexGrid.GetCellFromIndex(i);
            if (cell.IsUnderwater)
                continue;

            ClimateData data = _climate[i];
            float weight = data.Moisture * (cell.Elevation - WaterLevel) / (ElevationMaximum - WaterLevel);

            if (weight > 0.75f)
            {
                riverOrigins.Add(cell);
                riverOrigins.Add(cell);
            }
            if (weight > 0.5f)
            {
                riverOrigins.Add(cell);
            }
            if (weight > 0.25f)
            {
                riverOrigins.Add(cell);
            }
        }

        int riverBudget = Mathf.RoundToInt(_landCells * RiverPercentage * 0.01f);
        while (riverBudget > 0 && riverOrigins.Count > 0)
        {
            int index = _rng.RandiRange(0, riverOrigins.Count - 1);
            int lastIndex = riverOrigins.Count - 1;
            HexCell origin = riverOrigins[index];
            riverOrigins[index] = riverOrigins[lastIndex];
            riverOrigins.RemoveAt(lastIndex);

            if (!origin.HasRiver)
            {
                bool isValidOrigin = true;
                for (HexDirection d = 0; d <= HexDirection.NW; d++)
                {
                    HexCell neighbor = origin.GetNeighbor(d);
                    if (neighbor != null && (neighbor.HasRiver || neighbor.IsUnderwater))
                    {
                        isValidOrigin = false;
                        break;
                    }
                }

                if (isValidOrigin)
                {
                    riverBudget -= CreateRiver(origin);
                }
            }
        }
    }

    private int CreateRiver(HexCell origin)
    {
        int riverLength = 0;
        HexCell cell = origin;
        HexDirection direction = HexDirection.NE;

        while (!cell.IsUnderwater)
        {
            int minNeighborElevation = int.MaxValue;
            _flowDirections.Clear();

            for (HexDirection d = 0; d <= HexDirection.NW; d++)
            {
                HexCell neighbor = cell.GetNeighbor(d);
                if (neighbor == null)
                    continue;

                if (neighbor.Elevation <= minNeighborElevation)
                {
                    minNeighborElevation = neighbor.Elevation;
                }

                if (neighbor == origin || neighbor.HasIncomingRiver)
                    continue;

                int delta = neighbor.Elevation - cell.Elevation;
                if (delta > 0)
                    continue;

                if (neighbor.HasOutgoingRiver)
                {
                    cell.SetOutgoingRiver(d);
                    return riverLength;
                }

                if (delta < 0)
                {
                    _flowDirections.Add(d);
                    _flowDirections.Add(d);
                    _flowDirections.Add(d);
                }

                if (riverLength == 1 ||
                    (d != direction.Next2() && d != direction.Previous2()))
                {
                    _flowDirections.Add(d);
                }

                _flowDirections.Add(d);
            }

            if (_flowDirections.Count == 0)
            {
                if (riverLength == 1)
                    return 0;

                if (minNeighborElevation >= cell.Elevation)
                {
                    cell.WaterLevel = minNeighborElevation;
                    if (minNeighborElevation == cell.Elevation)
                    {
                        cell.Elevation = minNeighborElevation - 1;
                    }
                }
                break;
            }

            direction = _flowDirections[_rng.RandiRange(0, _flowDirections.Count - 1)];
            cell.SetOutgoingRiver(direction);
            riverLength++;

            if (minNeighborElevation >= cell.Elevation && _rng.Randf() < ExtraLakeProbability)
            {
                cell.WaterLevel = cell.Elevation;
                cell.Elevation--;
            }

            cell = cell.GetNeighbor(direction);
        }

        return riverLength;
    }

    private float DetermineTemperature(HexCell cell)
    {
        float latitude = (float)cell.HexCoordinates.Z / HexGrid.CellCountZ;
        if (hemisphereMode == HemisphereMode.Both)
        {
            latitude *= 2f;
            if (latitude > 1f)
            {
                latitude = 2f - latitude;
            }
        }
        else if (hemisphereMode == HemisphereMode.North)
        {
            latitude = 1f - latitude;
        }

        float temperature = Mathf.Lerp(LowTemperature, HighTemperature, latitude);

        temperature *= 1f - (cell.ViewElevation - WaterLevel) / (ElevationMaximum - WaterLevel + 1f);

        Vector4 jitterVector = HexMetrics.SampleNoise(cell.Position * 0.1f);
        float jitter = jitterVector[_temperatureJitterChannel];
        temperature += (jitter * 2f - 1f) * TemperatureJitter;

        return temperature;
    }

    private void SetTerrainType()
    {
        _temperatureJitterChannel = _rng.RandiRange(0, 3);
        int rockDesertElevation = ElevationMaximum - ((ElevationMaximum - WaterLevel) / 2);

        for (int i = 0; i < _cellCount; i++)
        {
            HexCell cell = HexGrid.GetCellFromIndex(i);
            float temperature = DetermineTemperature(cell);
            float moisture = _climate[i].Moisture;

            if (!cell.IsUnderwater)
            {
                int t = 0;
                while (t < TemperatureBands.Length)
                {
                    if (temperature < TemperatureBands[t])
                        break;
                    t++;
                }

                int m = 0;
                while (m < MoistureBands.Length)
                {
                    if (moisture < MoistureBands[m])
                        break;
                    m++;
                }

                Biome cellBiome = Biomes[t * 4 + m];
                int terrainTypeToUse = cellBiome.Terrain;
                if (terrainTypeToUse == 0)
                {
                    if (cell.Elevation >= rockDesertElevation)
                    {
                        terrainTypeToUse = 3;
                    }
                }
                else if (cell.Elevation == ElevationMaximum)
                {
                    terrainTypeToUse = 4;
                }

                int plantTypeToUse = cellBiome.Plant;
                if (terrainTypeToUse == 4)
                {
                    plantTypeToUse = 0;
                }
                else if (cellBiome.Plant < 3 && cell.HasRiver)
                {
                    plantTypeToUse++;
                }

                cell.TerrainTypeIndex = terrainTypeToUse;
                cell.PlantLevel = plantTypeToUse;
            }
            else
            {
                int terrain = 0;
                if (cell.Elevation == WaterLevel - 1)
                {
                    int cliffs = 0;
                    int slopes = 0;
                    for (HexDirection d = 0; d <= HexDirection.NW; d++)
                    {
                        HexCell neighbor = cell.GetNeighbor(d);
                        if (neighbor == null)
                            continue;

                        int delta = neighbor.Elevation - cell.WaterLevel;
                        if (delta == 0)
                        {
                            slopes++;
                        }
                        else if (delta > 0)
                        {
                            cliffs++;
                        }
                    }

                    if (cliffs + slopes > 3)
                    {
                        terrain = 1;
                    }
                    else if (cliffs > 0)
                    {
                        terrain = 3;
                    }
                    else if (slopes > 0)
                    {
                        terrain = 0;
                    }
                    else
                    {
                        terrain = 1;
                    }
                }
                else if (cell.Elevation >= WaterLevel)
                {
                    terrain = 1;
                }
                else if (cell.Elevation < 0)
                {
                    terrain = 3;
                }
                else
                {
                    terrain = 2;
                }

                if (terrain == 1 && temperature < TemperatureBands[0])
                {
                    terrain = 2;
                }

                cell.TerrainTypeIndex = terrain;
            }
        }
    }
    #endregion

}