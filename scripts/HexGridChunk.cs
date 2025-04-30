using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class HexGridChunk : Node3D
{
    private readonly List<HexCell> hexCells = new();
    private readonly HexMeshCollection meshes;
    private readonly HexTerrainTriangulator terrainTriangulator;
    private readonly HexWaterTriangulator waterTriangulator;
    private readonly HexFeatureManager features;
    private static readonly Color SplatWeights1 = new(1f, 0f, 0f);
    private static readonly Color SplatWeights2 = new(0f, 1f, 0f);
    private static readonly Color SplatWeights3 = new(0f, 0f, 1f);
    private ShaderMaterial wallsMaterial;

    public bool UpdateNeeded { get; set; }

    public HexGridChunk()
    {
        meshes = new HexMeshCollection();
        terrainTriangulator = new HexTerrainTriangulator(meshes);
        waterTriangulator = new HexWaterTriangulator(meshes);
        features = new HexFeatureManager();
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

    public void SetTerrainMeshMaterial(ShaderMaterial mat) => meshes.SetTerrainMaterial(mat);
    public void SetRiversMeshMaterial(ShaderMaterial mat) => meshes.SetRiversMaterial(mat);
    public void SetRoadMeshMaterial(ShaderMaterial mat) => meshes.SetRoadMaterial(mat);
    public void SetWaterMeshMaterial(ShaderMaterial mat) => meshes.SetWaterMaterial(mat);
    public void SetWaterShoreMeshMaterial(ShaderMaterial mat) => meshes.SetWaterShoreMaterial(mat);
    public void SetEstuariesMeshMaterial(ShaderMaterial mat) => meshes.SetEstuariesMaterial(mat);
    
    public void SetWallsMeshMaterial(ShaderMaterial mat)
    {
        wallsMaterial = mat;
        wallsMaterial.RenderPriority = 1;
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

        // Begin creation of the walls mesh
        features.Walls.Begin();
        features.Walls.UseCellData = true;
        features.Walls.UseCollider = false;
        features.Walls.UseUVCoordinates = false;
        features.Walls.UseUV2Coordinates = false;
        features.Walls.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
        features.Walls.SortingOffset = 10f;

        // Iterate over each hex cell and triangulate the mesh for that hex
        foreach (var cell in hexCells)
        {
            TriangulateHex(cell);
        }

        // Finalize all meshes
        meshes.EndMeshes();
        features.Apply();
        features.Walls.End(wallsMaterial);
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
                Vector3 featurePosition = (center + e.V1 + e.V5) * (1f / 3f);
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
        terrainTriangulator.TriangulateEdgeFan(center, m,  cell.Index);

        if (!cell.IsUnderwater)
        {
            bool reversed = cell.HasIncomingRiver;
            var indices = new Vector3(cell.Index, cell.Index, cell.Index);
            terrainTriangulator.TriangulateRiverQuad(m.V2, m.V4, e.V2, e.V4, cell.RiverSurfaceY, 0.6f, reversed, indices);

            center = center with { Y = cell.RiverSurfaceY };
            m.V2 = m.V2 with { Y = cell.RiverSurfaceY };
            m.V4 = m.V4 with { Y = cell.RiverSurfaceY };

            terrainTriangulator.TriangulateRiverTriangle(center, m.V2, m.V4, reversed, indices);
        }
    }

    private void TriangulateWithRiver(HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
    {
        Vector3 centerL = Vector3.Zero;
        Vector3 centerR = Vector3.Zero;
        var oppositeDirection = direction.Opposite();
        var previousDirection = direction.Previous();
        var nextDirection = direction.Next();
        var next2Direction = direction.Next2();

        if (cell.HasRiverThroughEdge(oppositeDirection))
        {
            centerL = center + HexMetrics.GetFirstSolidCorner(previousDirection) * 0.25f;
            centerR = center + HexMetrics.GetSecondSolidCorner(nextDirection) * 0.25f;
        }
        else if (cell.HasRiverThroughEdge(nextDirection))
        {
            centerL = center;
            centerR = center.Lerp(e.V5, 0.67f);
        }
        else if (cell.HasRiverThroughEdge(previousDirection))
        {
            centerL = center.Lerp(e.V1, 0.67f);
            centerR = center;
        }
        else if (cell.HasRiverThroughEdge(next2Direction))
        {
            centerL = center;
            centerR = center + HexMetrics.GetSolidEdgeMiddle(nextDirection) * (0.5f * HexMetrics.InnerToOuter);
        }
        else
        {
            centerL = center + HexMetrics.GetSolidEdgeMiddle(previousDirection) * (0.5f * HexMetrics.InnerToOuter);
            centerR = center;
        }

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
        terrainTriangulator.TriangulateRiverCenter(centerL, center, centerR, m, indices);

        if (!cell.IsUnderwater)
        {
            bool reversed = cell.IncomingRiverDirection == direction;
            terrainTriangulator.TriangulateRiverQuad(centerL, centerR, m.V2, m.V4, cell.RiverSurfaceY, 0.4f, reversed, indices);
            terrainTriangulator.TriangulateRiverQuad(m.V2, m.V4, e.V2, e.V4, cell.RiverSurfaceY, 0.6f, reversed, indices);
        }
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
            e2.V3 = e2.V3 with { Y = neighbor.StreamBedY };
            var indices = new Vector3(cell.Index, neighbor.Index, cell.Index);

            if (!cell.IsUnderwater)
            {
                if (!neighbor.IsUnderwater)
                {
                    bool reversed = cell.HasIncomingRiver && cell.IncomingRiverDirection == direction;
                    terrainTriangulator.TriangulateRiverQuad(
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

        if (cell.GetEdgeType(direction) == HexEdgeType.Slope)
        {
            TriangulateEdgeTerraces(e1, cell, e2, neighbor, hasRoad);
        }
        else
        {
            terrainTriangulator.TriangulateEdgeStrip(e1, cell.Index, e2, neighbor.Index, hasRoad);
        }

        features.AddWall(e1, cell, e2, neighbor, hasRiver, hasRoad);

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
        Color w2 = HexMetrics.TerraceColorLerp(SplatWeights1, SplatWeights2, 1);
        float i1 = beginCell.Index;
        float i2 = endCell.Index;

        terrainTriangulator.TriangulateEdgeStrip(begin, SplatWeights1, i1, e2, w2, i2, hasRoad);

        for (int i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            EdgeVertices e1 = e2;
            Color w1 = w2;
            e2 = EdgeVertices.TerraceLerp(begin, end, i);
            w2 = HexMetrics.TerraceColorLerp(SplatWeights1, SplatWeights2, i);
            terrainTriangulator.TriangulateEdgeStrip(e1, w1, i1, e2, w2, i2, hasRoad);
        }

        terrainTriangulator.TriangulateEdgeStrip(e2, w2, i1, end, SplatWeights2, i2, hasRoad);
    }

    private void TriangulateCorner(
        Vector3 bottom, HexCell bottomCell,
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell)
    {
        terrainTriangulator.TriangulateCorner(bottom, bottomCell, left, leftCell, right, rightCell);

        features.AddWallThreeCells(bottom, bottomCell, left, leftCell, right, rightCell);
    }

    private void TriangulateAdjacentToRiver(HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
    {
        if (cell.HasRoads)
        {
            TriangulateRoadAdjacentToRiver(direction, cell, center, e);
        }

        var nextDirection = direction.Next();
        var previousDirection = direction.Previous();
        var previous2Direction = previousDirection.Previous();
        var next2Direction = nextDirection.Next();

        if (cell.HasRiverThroughEdge(nextDirection))
        {
            if (cell.HasRiverThroughEdge(previousDirection))
            {
                center += HexMetrics.GetSolidEdgeMiddle(direction) * (HexMetrics.InnerToOuter * 0.5f);
            }
            else if (cell.HasRiverThroughEdge(previous2Direction))
            {
                center += HexMetrics.GetFirstSolidCorner(direction) * 0.25f;
            }
        }
        else if (cell.HasRiverThroughEdge(previousDirection) && cell.HasRiverThroughEdge(next2Direction))
        {
            center += HexMetrics.GetSecondSolidCorner(direction) * 0.25f;
        }

        EdgeVertices m = new EdgeVertices(
            center.Lerp(e.V1, 0.5f),
            center.Lerp(e.V5, 0.5f)
        );

        terrainTriangulator.TriangulateEdgeStrip(m, cell.Index, e, cell.Index);
        terrainTriangulator.TriangulateEdgeFan(center, m, cell.Index);

        if (!cell.IsUnderwater && !cell.HasRoadThroughEdge(direction))
        {
            Vector3 featurePosition = (center + e.V1 + e.V5) * (1f / 3f);
            features.AddFeature(cell, featurePosition);
        }
    }

    private void TriangulateWithoutRiver(HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
    {
        terrainTriangulator.TriangulateEdgeFan(center, e, cell.Index);

        if (cell.HasRoads)
        {
            var interpolators = GetRoadInterpolators(direction, cell);

            terrainTriangulator.TriangulateRoad(
                center,
                center.Lerp(e.V1, interpolators.X),
                center.Lerp(e.V5, interpolators.Y),
                e,
                cell.HasRoadThroughEdge(direction),
                cell.Index
            );
        }
    }

    private void TriangulateRoadAdjacentToRiver(HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
    {
        bool hasRoadThroughEdge = cell.HasRoadThroughEdge(direction);
        var interpolators = GetRoadInterpolators(direction, cell);
        bool previousHasRiver = cell.HasRiverThroughEdge(direction.Previous());
        bool nextHasRiver = cell.HasRiverThroughEdge(direction.Next());
        Vector3 roadCenter = center;

        if (cell.HasRiverBeginningOrEnd)
        {
            var dir = cell.RiverBeginOrEndDirection;
            var oppDir = dir.Opposite();
            roadCenter += HexMetrics.GetSolidEdgeMiddle(oppDir) * (1f / 3f);
        }
        else if (cell.IncomingRiverDirection == cell.OutgoingRiverDirection.Opposite())
        {
            Vector3 corner;
            if (previousHasRiver)
            {
                if (!hasRoadThroughEdge && !cell.HasRoadThroughEdge(direction.Next()))
                    return;

                corner = HexMetrics.GetSecondSolidCorner(direction);
            }
            else
            {
                if (!hasRoadThroughEdge && !cell.HasRoadThroughEdge(direction.Previous()))
                    return;

                corner = HexMetrics.GetFirstSolidCorner(direction);
            }

            roadCenter += corner * 0.5f;

            if (cell.IncomingRiverDirection == direction.Next() &&
                (cell.HasRoadThroughEdge(direction.Next2()) ||
                 cell.HasRoadThroughEdge(direction.Opposite())))
            {
                features.AddBridge(cell, roadCenter, center - (corner * 0.5f));
            }

            center += corner * 0.25f;
        }
        else if (cell.IncomingRiverDirection == cell.OutgoingRiverDirection.Previous())
        {
            roadCenter -= HexMetrics.GetSecondCorner(cell.IncomingRiverDirection) * 0.2f;
        }
        else if (cell.IncomingRiverDirection == cell.OutgoingRiverDirection.Next())
        {
            roadCenter -= HexMetrics.GetFirstCorner(cell.IncomingRiverDirection) * 0.2f;
        }
        else if (previousHasRiver && nextHasRiver)
        {
            if (!hasRoadThroughEdge)
                return;

            Vector3 offset = HexMetrics.GetSolidEdgeMiddle(direction) * HexMetrics.InnerToOuter;
            roadCenter += offset * 0.7f;
            center += offset * 0.5f;
        }
        else
        {
            HexDirection middle;
            if (previousHasRiver)
            {
                middle = direction.Next();
            }
            else if (nextHasRiver)
            {
                middle = direction.Previous();
            }
            else
            {
                middle = direction;
            }

            if (!cell.HasRoadThroughEdge(middle) &&
                !cell.HasRoadThroughEdge(middle.Previous()) &&
                !cell.HasRoadThroughEdge(middle.Next()))
            {
                return;
            }

            Vector3 offset = HexMetrics.GetSolidEdgeMiddle(middle);
            roadCenter += offset * 0.25f;

            if (direction == middle &&
                cell.HasRoadThroughEdge(direction.Opposite()))
            {
                features.AddBridge(cell, roadCenter, center - offset * (HexMetrics.InnerToOuter * 0.7f));
            }
        }

        terrainTriangulator.TriangulateRoad(
            roadCenter,
            center.Lerp(e.V1, interpolators.X),
            center.Lerp(e.V5, interpolators.Y),
            e,
            hasRoadThroughEdge,
            cell.Index
        );
    }

    private Vector2 GetRoadInterpolators(HexDirection direction, HexCell cell)
    {
        if (cell.HasRoadThroughEdge(direction))
        {
            return new Vector2(0.5f, 0.5f);
        }

        var previousDirection = direction.Previous();
        var nextDirection = direction.Next();

        float x = cell.HasRoadThroughEdge(previousDirection) ? 0.5f : 0.25f;
        float y = cell.HasRoadThroughEdge(nextDirection) ? 0.5f : 0.25f;

        return new Vector2(x, y);
    }

}
