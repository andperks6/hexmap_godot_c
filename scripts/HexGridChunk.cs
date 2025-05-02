using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class HexGridChunk : Node3D
{
    private readonly List<HexCell> hexCells = new();
    private readonly HexMeshCollection meshes;
    private readonly HexTerrainTriangulator terrainTriangulator;
    private readonly HexWaterTriangulator waterTriangulator;
    private readonly HexRoadRiverTriangulator roadRiverTriangulator;
    private readonly HexFeatureManager features;
    private readonly HexMaterialManager materialManager;

    public bool UpdateNeeded { get; set; }

    public HexGridChunk()
    {
        meshes = new HexMeshCollection();
        terrainTriangulator = new HexTerrainTriangulator(meshes);
        waterTriangulator = new HexWaterTriangulator(meshes);
        roadRiverTriangulator = new HexRoadRiverTriangulator(meshes);
        features = new HexFeatureManager();
        materialManager = meshes.MaterialManager;
    }

    public override void _Ready()
    {
        meshes.InitializeMeshes(this);
        AddChild(features);
    }

    public void AddCell(int index, HexCell cell)
    {
        cell.HexChunk = this;
        hexCells.Add(cell);
        AddChild(cell);
    }

    // Material setter
    public void SetMaterial(HexMaterialType type, ShaderMaterial mat)
    {
        materialManager.SetMaterial(type, mat);
    }

    public void RequestRefresh()
    {
        UpdateNeeded = true;
    }

    public void Refresh()
    {
        TriangulateCells();
        UpdateNeeded = false;
    }

    private void TriangulateCells()
    {
        meshes.BeginMeshes();
        features.Clear();

        InitializeWallsMesh();
        
        foreach (var cell in hexCells)
        {
            TriangulateHex(cell);
        }

        meshes.EndMeshes();
        features.Apply();
        features.Walls.End(materialManager.GetMaterial(HexMaterialType.Walls));
    }

    private void InitializeWallsMesh()
    {
        features.Walls.Begin();
        features.Walls.UseCellData = true;
        features.Walls.UseCollider = false;
        features.Walls.UseUVCoordinates = false;
        features.Walls.UseUV2Coordinates = false;
        features.Walls.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        features.Walls.SortingOffset = HexMeshConstants.WallSortingOffset;
    }

    private void TriangulateHex(HexCell cell)
    {
        for (int i = 0; i < 6; i++)
        {
            TriangulateHexInDirection(cell, (HexDirection)i);
        }

        if (!cell.IsUnderwater)
        {
            if (!cell.HasRiver && !cell.HasRoads)
            {
                features.AddFeature(cell, cell.Position);
            }

            if (cell.IsSpecial)
            {
                features.AddSpecialFeature(cell, cell.Position);
            }
        }
    }

    private void TriangulateHexInDirection(HexCell cell, HexDirection direction)
    {
        Vector3 center = cell.Position;
        EdgeVertices e = new EdgeVertices(
            center + HexMetrics.GetFirstSolidCorner(direction),
            center + HexMetrics.GetSecondSolidCorner(direction)
        );

        if (cell.HasRiver)
        {
            if (cell.HasRiverThroughEdge(direction))
            {
                e.V3 = e.V3 with { Y = cell.StreamBedY };

                if (cell.HasRiverBeginningOrEnd)
                {
                    TriangulateWithRiverBeginOrEnd(direction, cell, center, e);
                }
                else
                {
                    TriangulateWithRiver(direction, cell, center, e);
                }
            }
            else
            {
                TriangulateAdjacentToRiver(direction, cell, center, e);
            }
        }
        else
        {
            TriangulateWithoutRiver(direction, cell, center, e);

            if (!cell.IsUnderwater && !cell.HasRoadThroughEdge(direction))
            {
                Vector3 featurePosition = (center + e.V1 + e.V5) * HexMeshConstants.FeatureThirdOffset;
                features.AddFeature(cell, featurePosition);
            }
        }

        if (direction <= HexDirection.SE)
        {
            TriangulateConnection(direction, cell, e);
        }

        if (cell.IsUnderwater)
        {
            waterTriangulator.TriangulateWater(direction, cell, center);
        }
    }

    private void TriangulateWithRiverBeginOrEnd(HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
    {
        EdgeVertices m = new(
            center.Lerp(e.V1, 0.5f),
            center.Lerp(e.V5, 0.5f)
        );

        m.V3 = m.V3 with { Y = e.V3.Y };

        terrainTriangulator.TriangulateEdgeStrip(m, cell.Index, e, cell.Index);
        terrainTriangulator.TriangulateEdgeFan(center, m, cell.Index);

        if (!cell.IsUnderwater)
        {
            bool reversed = cell.HasIncomingRiver;
            var indices = new Vector3(cell.Index, cell.Index, cell.Index);
            roadRiverTriangulator.TriangulateRiverQuad(
                m.V2, m.V4, e.V2, e.V4, 
                cell.RiverSurfaceY, 
                HexMeshConstants.RiverSurfaceElevationOffset, 
                reversed, indices
            );

            center = center with { Y = cell.RiverSurfaceY };
            m.V2 = m.V2 with { Y = cell.RiverSurfaceY };
            m.V4 = m.V4 with { Y = cell.RiverSurfaceY };

            roadRiverTriangulator.TriangulateRiverTriangle(center, m.V2, m.V4, reversed, indices);
        }
    }

    private void TriangulateWithRiver(HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
    {
        var (centerL, centerR) = CalculateRiverCenters(direction, cell, center, e);
        center = centerL.Lerp(centerR, 0.5f);

        EdgeVertices m = new EdgeVertices(
            centerL.Lerp(e.V1, 0.5f),
            centerR.Lerp(e.V5, 0.5f),
            1f / 6f
        );

        center = center with { Y = e.V3.Y };
        m.V3 = m.V3 with { Y = e.V3.Y };

        terrainTriangulator.TriangulateEdgeStrip(m, cell.Index, e, cell.Index);

        var indices = new Vector3(cell.Index, cell.Index, cell.Index);
        roadRiverTriangulator.TriangulateRiverCenter(centerL, center, centerR, m, indices);

        if (!cell.IsUnderwater)
        {
            bool reversed = cell.IncomingRiverDirection == direction;
            roadRiverTriangulator.TriangulateRiverQuad(
                centerL, centerR, m.V2, m.V4, 
                cell.RiverSurfaceY, 0.4f, reversed, indices
            );
            roadRiverTriangulator.TriangulateRiverQuad(
                m.V2, m.V4, e.V2, e.V4, 
                cell.RiverSurfaceY, 0.6f, reversed, indices
            );
        }
    }

    private (Vector3 centerL, Vector3 centerR) CalculateRiverCenters(
        HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
    {
        var oppositeDirection = direction.Opposite();
        var previousDirection = direction.Previous();
        var nextDirection = direction.Next();
        var next2Direction = direction.Next2();

        if (cell.HasRiverThroughEdge(oppositeDirection))
        {
            return (
                center + HexMetrics.GetFirstSolidCorner(previousDirection) * 0.25f,
                center + HexMetrics.GetSecondSolidCorner(nextDirection) * 0.25f
            );
        }
        if (cell.HasRiverThroughEdge(nextDirection))
        {
            return (center, center.Lerp(e.V5, 0.67f));
        }
        if (cell.HasRiverThroughEdge(previousDirection))
        {
            return (center.Lerp(e.V1, 0.67f), center);
        }
        if (cell.HasRiverThroughEdge(next2Direction))
        {
            return (
                center,
                center + HexMetrics.GetSolidEdgeMiddle(nextDirection) * (0.5f * HexMetrics.InnerToOuter)
            );
        }
        
        return (
            center + HexMetrics.GetSolidEdgeMiddle(previousDirection) * (0.5f * HexMetrics.InnerToOuter),
            center
        );
    }

    private void TriangulateConnection(HexDirection direction, HexCell cell, EdgeVertices e1)
    {
        HexCell neighbor = cell.GetNeighbor(direction);
        if (neighbor == null) return;

        Vector3 bridge = HexMetrics.GetBridge(direction);
        bridge = bridge with { Y = neighbor.Position.Y - cell.Position.Y };
        EdgeVertices e2 = new EdgeVertices(
            e1.V1 + bridge,
            e1.V5 + bridge
        );

        bool hasRiver = cell.HasRiverThroughEdge(direction);
        bool hasRoad = cell.HasRoadThroughEdge(direction);

        if (hasRiver)
        {
            TriangulateRiverConnection(direction, cell, neighbor, e1, e2);
        }

        if (cell.GetEdgeType(direction) == HexEdgeType.Slope)
        {
            TriangulateEdgeTerraces(e1, cell, e2, neighbor, hasRoad);
        }
        else
        {
            terrainTriangulator.TriangulateEdgeStrip(e1, cell.Index, e2, neighbor.Index, hasRoad);
        }

        features.AddWall(e1, cell, e2, neighbor, hasRiver, hasRoad);

        TriangulateConnectionCorner(direction, cell, e1, neighbor, e2);
    }

    private void TriangulateRiverConnection(
        HexDirection direction, HexCell cell, HexCell neighbor,
        EdgeVertices e1, EdgeVertices e2)
    {
        e2.V3 = e2.V3 with { Y = neighbor.StreamBedY };
        var indices = new Vector3(cell.Index, neighbor.Index, cell.Index);

        if (!cell.IsUnderwater)
        {
            if (!neighbor.IsUnderwater)
            {
                bool reversed = cell.HasIncomingRiver && cell.IncomingRiverDirection == direction;
                roadRiverTriangulator.TriangulateRiverQuad(
                    e1.V2, e1.V4, e2.V2, e2.V4,
                    cell.RiverSurfaceY, neighbor.RiverSurfaceY,
                    0.8f, reversed, indices
                );
            }
            else if (cell.Elevation > neighbor.WaterLevel)
            {
                waterTriangulator.TriangulateWaterfallInWater(
                    e1.V2, e1.V4, e2.V2, e2.V4,
                    cell.RiverSurfaceY, neighbor.RiverSurfaceY,
                    neighbor.WaterSurfaceY, indices
                );
            }
        }
        else if (!neighbor.IsUnderwater && neighbor.Elevation > cell.WaterLevel)
        {
            waterTriangulator.TriangulateWaterfallInWater(
                e2.V4, e2.V2, e1.V4, e1.V2,
                neighbor.RiverSurfaceY, cell.RiverSurfaceY,
                cell.WaterSurfaceY, indices
            );
        }
    }

    private void TriangulateConnectionCorner(
        HexDirection direction, HexCell cell,
        EdgeVertices e1, HexCell neighbor, EdgeVertices e2)
    {
        HexDirection nextDirection = direction.Next();
        HexCell nextNeighbor = cell.GetNeighbor(nextDirection);
        if (direction <= HexDirection.E && nextNeighbor != null)
        {
            Vector3 v5 = e1.V5 + HexMetrics.GetBridge(nextDirection);
            v5 = v5 with { Y = nextNeighbor.Position.Y };

            if (cell.Elevation <= neighbor.Elevation)
            {
                if (cell.Elevation <= nextNeighbor.Elevation)
                {
                    TriangulateCorner(e1.V5, cell, e2.V5, neighbor, v5, nextNeighbor);
                }
                else
                {
                    TriangulateCorner(v5, nextNeighbor, e1.V5, cell, e2.V5, neighbor);
                }
            }
            else if (neighbor.Elevation <= nextNeighbor.Elevation)
            {
                TriangulateCorner(e2.V5, neighbor, v5, nextNeighbor, e1.V5, cell);
            }
            else
            {
                TriangulateCorner(v5, nextNeighbor, e1.V5, cell, e2.V5, neighbor);
            }
        }
    }

    private void TriangulateEdgeTerraces(
        EdgeVertices begin, HexCell beginCell,
        EdgeVertices end, HexCell endCell,
        bool hasRoad = false)
    {
        EdgeVertices e2 = EdgeVertices.TerraceLerp(begin, end, 1);
        Color w2 = HexMetrics.TerraceColorLerp(
            HexMeshConstants.SplatWeights1,
            HexMeshConstants.SplatWeights2,
            1
        );
        float i1 = beginCell.Index;
        float i2 = endCell.Index;

        terrainTriangulator.TriangulateEdgeStrip(
            begin, HexMeshConstants.SplatWeights1,
            i1, e2, w2, i2, hasRoad
        );

        for (int i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            EdgeVertices e1 = e2;
            Color w1 = w2;
            e2 = EdgeVertices.TerraceLerp(begin, end, i);
            w2 = HexMetrics.TerraceColorLerp(
                HexMeshConstants.SplatWeights1,
                HexMeshConstants.SplatWeights2,
                i
            );
            terrainTriangulator.TriangulateEdgeStrip(e1, w1, i1, e2, w2, i2, hasRoad);
        }

        terrainTriangulator.TriangulateEdgeStrip(
            e2, w2, i1,
            end, HexMeshConstants.SplatWeights2,
            i2, hasRoad
        );
    }

    private void TriangulateCorner(
        Vector3 bottom, HexCell bottomCell,
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell)
    {
        terrainTriangulator.TriangulateCorner(bottom, bottomCell, left, leftCell, right, rightCell);
        features.AddWallThreeCells(bottom, bottomCell, left, leftCell, right, rightCell);
    }

    private void TriangulateAdjacentToRiver(
        HexDirection direction, HexCell cell,
        Vector3 center, EdgeVertices e)
    {
        if (cell.HasRoads)
        {
            TriangulateRoadAdjacentToRiver(direction, cell, center, e);
        }

        center = AdjustCenterForRiver(direction, cell, center);

        EdgeVertices m = new EdgeVertices(
            center.Lerp(e.V1, 0.5f),
            center.Lerp(e.V5, 0.5f)
        );

        terrainTriangulator.TriangulateEdgeStrip(m, cell.Index, e, cell.Index);
        terrainTriangulator.TriangulateEdgeFan(center, m, cell.Index);

        if (!cell.IsUnderwater && !cell.HasRoadThroughEdge(direction))
        {
            Vector3 featurePosition = (center + e.V1 + e.V5) * HexMeshConstants.FeatureThirdOffset;
            features.AddFeature(cell, featurePosition);
        }
    }

    private Vector3 AdjustCenterForRiver(HexDirection direction, HexCell cell, Vector3 center)
    {
        var nextDirection = direction.Next();
        var previousDirection = direction.Previous();
        var previous2Direction = previousDirection.Previous();
        var next2Direction = nextDirection.Next();

        if (cell.HasRiverThroughEdge(nextDirection))
        {
            if (cell.HasRiverThroughEdge(previousDirection))
            {
                return center + HexMetrics.GetSolidEdgeMiddle(direction) * 
                    (HexMetrics.InnerToOuter * 0.5f);
            }
            if (cell.HasRiverThroughEdge(previous2Direction))
            {
                return center + HexMetrics.GetFirstSolidCorner(direction) * 0.25f;
            }
        }
        else if (cell.HasRiverThroughEdge(previousDirection) && 
                 cell.HasRiverThroughEdge(next2Direction))
        {
            return center + HexMetrics.GetSecondSolidCorner(direction) * 0.25f;
        }

        return center;
    }

    private void TriangulateWithoutRiver(
        HexDirection direction, HexCell cell,
        Vector3 center, EdgeVertices e)
    {
        terrainTriangulator.TriangulateEdgeFan(center, e, cell.Index);

        if (cell.HasRoads)
        {
            var roadData = new HexRoadData(direction, cell);
            roadRiverTriangulator.TriangulateRoad(
                center,
                center.Lerp(e.V1, roadData.Interpolators.X),
                center.Lerp(e.V5, roadData.Interpolators.Y),
                e,
                roadData.HasRoadThroughEdge,
                cell.Index
            );
        }
    }

    private void TriangulateRoadAdjacentToRiver(
        HexDirection direction, HexCell cell,
        Vector3 center, EdgeVertices e)
    {
        var roadData = new HexRoadData(direction, cell);
        Vector3 adjustedCenter = center;
        Vector3 roadCenter = roadData.GetRoadCenter(center, out adjustedCenter);

        if (roadCenter == Vector3.Zero)
        {
            return;
        }

        roadRiverTriangulator.TriangulateRoad(
            roadCenter,
            adjustedCenter.Lerp(e.V1, roadData.Interpolators.X),
            adjustedCenter.Lerp(e.V5, roadData.Interpolators.Y),
            e,
            roadData.HasRoadThroughEdge,
            cell.Index
        );
    }
}
