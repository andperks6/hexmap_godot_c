using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class HexGridChunk : Node3D
{
    private static readonly Color SplatWeights1 = new(1f, 0f, 0f);
    private static readonly Color SplatWeights2 = new(0f, 1f, 0f);
    private static readonly Color SplatWeights3 = new(0f, 0f, 1f);

    private readonly List<HexCell> hexCells = new();
    private readonly HexMesh terrain;
    private readonly HexMesh rivers;
    private readonly HexMesh roads;
    private readonly HexMesh water;
    private readonly HexMesh waterShore;
    private readonly HexMesh estuaries;
    private readonly HexFeatureManager features;

    private ShaderMaterial terrainShaderMaterial;
    private ShaderMaterial riversShaderMaterial;
    private ShaderMaterial roadShaderMaterial;
    private ShaderMaterial waterShaderMaterial;
    private ShaderMaterial waterShoreMaterial;
    private ShaderMaterial estuariesMaterial;
    private ShaderMaterial wallsMaterial;

    public bool UpdateNeeded { get; set; }

    public HexGridChunk()
    {
        terrain = new HexMesh();
        rivers = new HexMesh();
        roads = new HexMesh();
        water = new HexMesh();
        waterShore = new HexMesh();
        estuaries = new HexMesh();
        features = new HexFeatureManager();
    }

    public override void _Ready()
    {
        AddChild(terrain);
        AddChild(rivers);
        AddChild(roads);
        AddChild(water);
        AddChild(waterShore);
        AddChild(estuaries);
        AddChild(features);
    }

    public void AddCell(int index, HexCell cell)
    {
        cell.HexChunk = this;
        hexCells.Add(cell);
        AddChild(cell);
    }

    public void SetTerrainMeshMaterial(ShaderMaterial mat)
    {
        terrainShaderMaterial = mat;
        terrainShaderMaterial.RenderPriority = 0;
    }

    public void SetRiversMeshMaterial(ShaderMaterial mat)
    {
        riversShaderMaterial = mat;
        riversShaderMaterial.RenderPriority = 1;
    }

    public void SetRoadMeshMaterial(ShaderMaterial mat)
    {
        roadShaderMaterial = mat;
        roadShaderMaterial.RenderPriority = 1;
    }

    public void SetWaterMeshMaterial(ShaderMaterial mat)
    {
        waterShaderMaterial = mat;
        waterShaderMaterial.RenderPriority = 1;
    }

    public void SetWaterShoreMeshMaterial(ShaderMaterial mat)
    {
        waterShoreMaterial = mat;
        waterShoreMaterial.RenderPriority = 1;
    }

    public void SetEstuariesMeshMaterial(ShaderMaterial mat)
    {
        estuariesMaterial = mat;
        estuariesMaterial.RenderPriority = 1;
    }

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
        // Begin creation of the terrain mesh
        terrain.Begin();
        terrain.UseCellData = true;

        // Begin creation of the rivers mesh
        rivers.Begin();
        rivers.UseCollider = false;
        rivers.UseUVCoordinates = true;
        rivers.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        rivers.UseCellData = true;

        // Begin creation of the roads mesh
        roads.Begin();
        roads.UseCollider = false;
        roads.UseUVCoordinates = true;
        roads.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        roads.SortingOffset = 10f;
        roads.UseCellData = true;

        // Begin creation of the water mesh
        water.Begin();
        water.UseCollider = false;
        water.UseUVCoordinates = true;
        water.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        water.SortingOffset = 10f;
        water.UseCellData = true;

        // Begin creation of the shore water mesh
        waterShore.Begin();
        waterShore.UseCollider = false;
        waterShore.UseUVCoordinates = true;
        waterShore.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        waterShore.SortingOffset = 10f;
        waterShore.UseCellData = true;

        // Begin creation of the estuaries water mesh
        estuaries.Begin();
        estuaries.UseCollider = false;
        estuaries.UseUVCoordinates = true;
        estuaries.UseUV2Coordinates = true;
        estuaries.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        estuaries.SortingOffset = 10f;
        estuaries.UseCellData = true;

        // Clear the features for the hex grid chunk
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
        terrain.End(terrainShaderMaterial);
        rivers.End(riversShaderMaterial);
        roads.End(roadShaderMaterial);
        water.End(waterShaderMaterial);
        waterShore.End(waterShoreMaterial);
        estuaries.End(estuariesMaterial);
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
            TriangulateWater(direction, cell, center);
        }
    }

    private void TriangulateWithRiverBeginOrEnd(HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
    {
        EdgeVertices m = new EdgeVertices(
            center.Lerp(e.V1, 0.5f),
            center.Lerp(e.V5, 0.5f)
        );

        m.V3 = m.V3 with { Y = e.V3.Y };

        TriangulateEdgeStrip(m, SplatWeights1, cell.Index, e, SplatWeights1, cell.Index);
        TriangulateEdgeFan(center, m, SplatWeights1, cell.Index);

        if (!cell.IsUnderwater)
        {
            bool reversed = cell.HasIncomingRiver;
            var indices = new Vector3(cell.Index, cell.Index, cell.Index);
            TriangulateRiverQuad(m.V2, m.V4, e.V2, e.V4, cell.RiverSurfaceY, 0.6f, reversed, indices);

            center = center with { Y = cell.RiverSurfaceY };
            m.V2 = m.V2 with { Y = cell.RiverSurfaceY };
            m.V4 = m.V4 with { Y = cell.RiverSurfaceY };

            var primitive = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            if (reversed)
            {
                primitive.AddTrianglePerturbedVertices(center, m.V2, m.V4);
                primitive.AddTriangleUv1(
                    new Vector2(0.5f, 0.4f),
                    new Vector2(1f, 0.2f),
                    new Vector2(0f, 0.2f)
                );
            }
            else
            {
                primitive.AddTrianglePerturbedVertices(center, m.V2, m.V4);
                primitive.AddTriangleUv1(
                    new Vector2(0.5f, 0.4f),
                    new Vector2(0f, 0.6f),
                    new Vector2(1f, 0.6f)
                );
            }
            primitive.AddTriangleCellDataUniform(indices, SplatWeights1);
            rivers.CommitPrimitive(primitive);
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

        TriangulateEdgeStrip(m, SplatWeights1, cell.Index, e, SplatWeights1, cell.Index);

        var indices = new Vector3(cell.Index, cell.Index, cell.Index);

        var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t1.AddTrianglePerturbedVertices(centerL, m.V1, m.V2);
        t1.AddTriangleCellDataUniform(indices, SplatWeights1);
        terrain.CommitPrimitive(t1);

        var t2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t2.AddQuadPerturbedVertices(centerL, center, m.V2, m.V3);
        t2.AddQuadCellDataUnified(indices, SplatWeights1);
        terrain.CommitPrimitive(t2);

        var t3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t3.AddQuadPerturbedVertices(center, centerR, m.V3, m.V4);
        t3.AddQuadCellDataUnified(indices, SplatWeights1);
        terrain.CommitPrimitive(t3);

        var t4 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t4.AddTrianglePerturbedVertices(centerR, m.V4, m.V5);
        t4.AddTriangleCellDataUniform(indices, SplatWeights1);
        terrain.CommitPrimitive(t4);

        if (!cell.IsUnderwater)
        {
            bool reversed = cell.IncomingRiverDirection == direction;
            TriangulateRiverQuad(centerL, centerR, m.V2, m.V4, cell.RiverSurfaceY, 0.4f, reversed, indices);
            TriangulateRiverQuad(m.V2, m.V4, e.V2, e.V4, cell.RiverSurfaceY, 0.6f, reversed, indices);
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
                    TriangulateRiverQuad(
                        e1.V2, e1.V4, e2.V2, e2.V4,
                        cell.RiverSurfaceY, neighbor.RiverSurfaceY,
                        0.8f, reversed, indices
                    );
                }
                else if (cell.Elevation > neighbor.WaterLevel)
                {
                    TriangulateWaterfallInWater(
                        e1.V2, e1.V4, e2.V2, e2.V4,
                        cell.RiverSurfaceY, neighbor.RiverSurfaceY,
                        neighbor.WaterSurfaceY, indices
                    );
                }
            }
            else if (!neighbor.IsUnderwater && neighbor.Elevation > cell.WaterLevel)
            {
                TriangulateWaterfallInWater(
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
            TriangulateEdgeStrip(e1, SplatWeights1, cell.Index, e2, SplatWeights2, neighbor.Index, hasRoad);
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

        TriangulateEdgeStrip(begin, SplatWeights1, i1, e2, w2, i2, hasRoad);

        for (int i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            EdgeVertices e1 = e2;
            Color w1 = w2;
            e2 = EdgeVertices.TerraceLerp(begin, end, i);
            w2 = HexMetrics.TerraceColorLerp(SplatWeights1, SplatWeights2, i);
            TriangulateEdgeStrip(e1, w1, i1, e2, w2, i2, hasRoad);
        }

        TriangulateEdgeStrip(e2, w2, i1, end, SplatWeights2, i2, hasRoad);
    }

    private void TriangulateEdgeStrip(
        EdgeVertices e1, Color w1, float index1,
        EdgeVertices e2, Color w2, float index2,
        bool hasRoad = false)
    {
        var indices = new Vector3(index1, index2, index1);

        var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t1.AddQuadPerturbedVertices(e1.V1, e1.V2, e2.V1, e2.V2);
        t1.AddQuadCellDataDual(indices, w1, w2);
        terrain.CommitPrimitive(t1);

        var t2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t2.AddQuadPerturbedVertices(e1.V2, e1.V3, e2.V2, e2.V3);
        t2.AddQuadCellDataDual(indices, w1, w2);
        terrain.CommitPrimitive(t2);

        var t3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t3.AddQuadPerturbedVertices(e1.V3, e1.V4, e2.V3, e2.V4);
        t3.AddQuadCellDataDual(indices, w1, w2);
        terrain.CommitPrimitive(t3);

        var t4 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t4.AddQuadPerturbedVertices(e1.V4, e1.V5, e2.V4, e2.V5);
        t4.AddQuadCellDataDual(indices, w1, w2);
        terrain.CommitPrimitive(t4);

        if (hasRoad)
        {
            TriangulateRoadSegment(
                e1.V2, e1.V3, e1.V4,
                e2.V2, e2.V3, e2.V4,
                w1, w2, indices
            );
        }
    }

    private void TriangulateEdgeFan(Vector3 center, EdgeVertices edge, Color color, float index)
    {
        var indices = new Vector3(index, index, index);

        var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t1.AddTrianglePerturbedVertices(center, edge.V1, edge.V2);
        t1.AddTriangleCellDataUniform(indices, color);
        terrain.CommitPrimitive(t1);

        var t2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t2.AddTrianglePerturbedVertices(center, edge.V2, edge.V3);
        t2.AddTriangleCellDataUniform(indices, color);
        terrain.CommitPrimitive(t2);

        var t3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t3.AddTrianglePerturbedVertices(center, edge.V3, edge.V4);
        t3.AddTriangleCellDataUniform(indices, color);
        terrain.CommitPrimitive(t3);

        var t4 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t4.AddTrianglePerturbedVertices(center, edge.V4, edge.V5);
        t4.AddTriangleCellDataUniform(indices, color);
        terrain.CommitPrimitive(t4);
    }

    private void TriangulateCorner(
        Vector3 bottom, HexCell bottomCell,
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell)
    {
        HexEdgeType leftEdgeType = bottomCell.GetEdgeType(leftCell);
        HexEdgeType rightEdgeType = bottomCell.GetEdgeType(rightCell);

        if (leftEdgeType == HexEdgeType.Slope)
        {
            if (rightEdgeType == HexEdgeType.Slope)
            {
                TriangulateCornerTerraces(bottom, bottomCell, left, leftCell, right, rightCell);
            }
            else if (rightEdgeType == HexEdgeType.Flat)
            {
                TriangulateCornerTerraces(left, leftCell, right, rightCell, bottom, bottomCell);
            }
            else
            {
                TriangulateCornerTerracesCliff(bottom, bottomCell, left, leftCell, right, rightCell);
            }

        }
        else if (rightEdgeType == HexEdgeType.Slope)
        {
            if (leftEdgeType == HexEdgeType.Flat)
            {
                TriangulateCornerTerraces(right, rightCell, bottom, bottomCell, left, leftCell);
            }
            else
            {
                TriangulateCornerCliffTerraces(bottom, bottomCell, left, leftCell, right, rightCell);
            }
        }
        else if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            if (leftCell.Elevation < rightCell.Elevation)
            {
                TriangulateCornerCliffTerraces(right, rightCell, bottom, bottomCell, left, leftCell);
            }
            else
            {
                TriangulateCornerTerracesCliff(left, leftCell, right, rightCell, bottom, bottomCell);
            }
        }
        else
        {
            var indices = new Vector3(bottomCell.Index, leftCell.Index, rightCell.Index);
            var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            t1.AddTrianglePerturbedVertices(bottom, left, right);
            t1.AddTriangleCellData(indices, SplatWeights1, SplatWeights2, SplatWeights3);
            terrain.CommitPrimitive(t1);
        }

        features.AddWallThreeCells(bottom, bottomCell, left, leftCell, right, rightCell);
    }

    private void TriangulateCornerTerraces(
        Vector3 begin, HexCell beginCell,
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell)
    {
        Vector3 v3 = HexMetrics.TerraceLerp(begin, left, 1);
        Vector3 v4 = HexMetrics.TerraceLerp(begin, right, 1);
        Color w3 = HexMetrics.TerraceColorLerp(SplatWeights1, SplatWeights2, 1);
        Color w4 = HexMetrics.TerraceColorLerp(SplatWeights1, SplatWeights3, 1);

        var indices = new Vector3(beginCell.Index, leftCell.Index, rightCell.Index);

        var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t1.AddTrianglePerturbedVertices(begin, v3, v4);
        t1.AddTriangleCellData(indices, SplatWeights1, w3, w4);
        terrain.CommitPrimitive(t1);

        for (int i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            Vector3 v1 = v3;
            Vector3 v2 = v4;
            Color w1 = w3;
            Color w2 = w4;

            v3 = HexMetrics.TerraceLerp(begin, left, i);
            v4 = HexMetrics.TerraceLerp(begin, right, i);
            w3 = HexMetrics.TerraceColorLerp(SplatWeights1, SplatWeights2, i);
            w4 = HexMetrics.TerraceColorLerp(SplatWeights1, SplatWeights3, i);

            var q1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            q1.AddQuadPerturbedVertices(v1, v2, v3, v4);
            q1.AddQuadCellData(indices, w1, w2, w3, w4);
            terrain.CommitPrimitive(q1);
        }

        var q2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        q2.AddQuadPerturbedVertices(v3, v4, left, right);
        q2.AddQuadCellData(indices, w3, w4, SplatWeights2, SplatWeights3);
        terrain.CommitPrimitive(q2);
    }

    private void TriangulateCornerTerracesCliff(
        Vector3 begin, HexCell beginCell,
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell)
    {
        float b = 1f / (rightCell.Elevation - beginCell.Elevation);
        if (b < 0) b = -b;

        Vector3 boundary = HexMetrics.Perturb(begin).Lerp(HexMetrics.Perturb(right), b);
        Color boundaryWeights = SplatWeights1.Lerp(SplatWeights3, b);

        var indices = new Vector3(beginCell.Index, leftCell.Index, rightCell.Index);

        TriangulateBoundaryTriangle(
            begin, SplatWeights1,
            left, SplatWeights2,
            boundary, boundaryWeights,
            indices
        );

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateBoundaryTriangle(
                left, SplatWeights2,
                right, SplatWeights3,
                boundary, boundaryWeights,
                indices
            );
        }
        else
        {
            var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            t1.AddTriangleUnperturbedVertices(
                HexMetrics.Perturb(left),
                HexMetrics.Perturb(right),
                boundary
            );
            t1.AddTriangleCellData(indices, SplatWeights2, SplatWeights3, boundaryWeights);
            terrain.CommitPrimitive(t1);
        }
    }

    private void TriangulateCornerCliffTerraces(
        Vector3 begin, HexCell beginCell,
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell)
    {
        float b = 1f / (leftCell.Elevation - beginCell.Elevation);
        if (b < 0) b = -b;

        Vector3 boundary = HexMetrics.Perturb(begin).Lerp(HexMetrics.Perturb(left), b);
        Color boundaryWeights = SplatWeights1.Lerp(SplatWeights2, b);

        var indices = new Vector3(beginCell.Index, leftCell.Index, rightCell.Index);

        TriangulateBoundaryTriangle(
            right, SplatWeights3,
            begin, SplatWeights1,
            boundary, boundaryWeights,
            indices
        );

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateBoundaryTriangle(
                left, SplatWeights2,
                right, SplatWeights3,
                boundary, boundaryWeights,
                indices
            );
        }
        else
        {
            var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            t1.AddTriangleUnperturbedVertices(
                HexMetrics.Perturb(left),
                HexMetrics.Perturb(right),
                boundary
            );
            t1.AddTriangleCellData(indices, SplatWeights2, SplatWeights3, boundaryWeights);
            terrain.CommitPrimitive(t1);
        }
    }

    private void TriangulateBoundaryTriangle(
        Vector3 begin, Color beginWeights,
        Vector3 left, Color leftWeights,
        Vector3 boundary, Color boundaryWeights,
        Vector3 indices)
    {
        Vector3 v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, 1));
        Color w2 = HexMetrics.TerraceColorLerp(beginWeights, leftWeights, 1);

        var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t1.AddTriangleUnperturbedVertices(HexMetrics.Perturb(begin), v2, boundary);
        t1.AddTriangleCellData(indices, beginWeights, w2, boundaryWeights);
        terrain.CommitPrimitive(t1);

        for (int i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            Vector3 v1 = v2;
            Color w1 = w2;

            v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, i));
            w2 = HexMetrics.TerraceColorLerp(beginWeights, leftWeights, i);

            var t2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            t2.AddTriangleUnperturbedVertices(v1, v2, boundary);
            t2.AddTriangleCellData(indices, w1, w2, boundaryWeights);
            terrain.CommitPrimitive(t2);
        }

        var t3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t3.AddTriangleUnperturbedVertices(v2, HexMetrics.Perturb(left), boundary);
        t3.AddTriangleCellData(indices, w2, leftWeights, boundaryWeights);
        terrain.CommitPrimitive(t3);
    }

    private void TriangulateRiverQuad(
        Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4,
        float y1, float v, bool reversed, Vector3 indices)
    {
        TriangulateRiverQuad(v1, v2, v3, v4, y1, y1, v, reversed, indices);
    }

    private void TriangulateRiverQuad(
        Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4,
        float y1, float y2, float v, bool reversed, Vector3 indices)
    {
        v1 = v1 with { Y = y1 };
        v2 = v2 with { Y = y1 };
        v3 = v3 with { Y = y2 };
        v4 = v4 with { Y = y2 };

        var river = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        river.AddQuadPerturbedVertices(v1, v2, v3, v4);

        if (reversed)
        {
            river.AddQuadUv1Floats(1, 0, 0.8f - v, 0.6f - v);
        }
        else
        {
            river.AddQuadUv1Floats(0, 1, v, v + 0.2f);
        }

        river.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
        rivers.CommitPrimitive(river);
    }

    private void TriangulateRoadSegment(
        Vector3 v1, Vector3 v2, Vector3 v3,
        Vector3 v4, Vector3 v5, Vector3 v6,
        Color w1, Color w2, Vector3 indices)
    {
        v1 = v1 with { Y = v1.Y + 0.01f };
        v2 = v2 with { Y = v2.Y + 0.01f };
        v3 = v3 with { Y = v3.Y + 0.01f };
        v4 = v4 with { Y = v4.Y + 0.01f };
        v5 = v5 with { Y = v5.Y + 0.01f };
        v6 = v6 with { Y = v6.Y + 0.01f };

        var road1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        road1.AddQuadPerturbedVertices(v1, v2, v4, v5);
        road1.AddQuadUv1Floats(0, 1, 0, 0);
        road1.AddQuadCellDataDual(indices, w1, w2);
        roads.CommitPrimitive(road1);

        var road2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        road2.AddQuadPerturbedVertices(v2, v3, v5, v6);
        road2.AddQuadUv1Floats(1, 0, 0, 0);
        road2.AddQuadCellDataDual(indices, w1, w2);
        roads.CommitPrimitive(road2);
    }

    private void TriangulateRoad(
        Vector3 center, Vector3 mL, Vector3 mR,
        EdgeVertices e, bool hasRoadThroughCellEdge, float index)
    {
        if (hasRoadThroughCellEdge)
        {
            var indices = new Vector3(index, index, index);
            var mC = mL.Lerp(mR, 0.5f);
            TriangulateRoadSegment(mL, mC, mR, e.V2, e.V3, e.V4, SplatWeights1, SplatWeights1, indices);

            center = center with { Y = center.Y + 0.1f };
            mC = mC with { Y = mC.Y + 0.1f };
            mL = mL with { Y = mL.Y + 0.1f };
            mR = mR with { Y = mR.Y + 0.1f };

            var road1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            road1.AddTrianglePerturbedVertices(center, mL, mC);
            road1.AddTriangleUv1(new Vector2(1, 0), new Vector2(0, 0), new Vector2(1, 0));
            road1.AddTriangleCellDataUniform(indices, SplatWeights1);
            roads.CommitPrimitive(road1);

            var road2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            road2.AddTrianglePerturbedVertices(center, mC, mR);
            road2.AddTriangleUv1(new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 0));
            road2.AddTriangleCellDataUniform(indices, SplatWeights1);
            roads.CommitPrimitive(road2);
        }
        else
        {
            TriangulateRoadEdge(center, mL, mR, index);
        }
    }

    private void TriangulateRoadEdge(Vector3 center, Vector3 mL, Vector3 mR, float index)
    {
        center = center with { Y = center.Y + 0.1f };
        mR = mR with { Y = mR.Y + 0.1f };
        mL = mL with { Y = mL.Y + 0.1f };

        var indices = new Vector3(index, index, index);

        var road = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        road.AddTrianglePerturbedVertices(center, mL, mR);
        road.AddTriangleUv1(new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 0));
        road.AddTriangleCellDataUniform(indices, SplatWeights1);
        roads.CommitPrimitive(road);
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

        TriangulateEdgeStrip(m, SplatWeights1, cell.Index, e, SplatWeights1, cell.Index);
        TriangulateEdgeFan(center, m, SplatWeights1, cell.Index);

        if (!cell.IsUnderwater && !cell.HasRoadThroughEdge(direction))
        {
            Vector3 featurePosition = (center + e.V1 + e.V5) * (1f / 3f);
            features.AddFeature(cell, featurePosition);
        }
    }

    private void TriangulateWithoutRiver(HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e)
    {
        TriangulateEdgeFan(center, e, SplatWeights1, cell.Index);

        if (cell.HasRoads)
        {
            var interpolators = GetRoadInterpolators(direction, cell);

            TriangulateRoad(
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

        TriangulateRoad(
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

    private void TriangulateWater(HexDirection direction, HexCell cell, Vector3 center)
    {
        center = center with { Y = cell.WaterSurfaceY };

        var neighbor = cell.GetNeighbor(direction);
        if (neighbor != null && !neighbor.IsUnderwater)
        {
            TriangulateShoreWater(direction, cell, neighbor, center);
        }
        else
        {
            TriangulateOpenWater(direction, cell, neighbor, center);
        }
    }

    private void TriangulateOpenWater(
        HexDirection direction, HexCell cell, HexCell neighbor, Vector3 center)
    {
        var c1 = center + HexMetrics.GetFirstWaterCorner(direction);
        var c2 = center + HexMetrics.GetSecondWaterCorner(direction);

        var indices = new Vector3(cell.Index, cell.Index, cell.Index);

        var w1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        w1.AddTrianglePerturbedVertices(center, c1, c2);
        w1.AddTriangleCellDataUniform(indices, SplatWeights1);
        water.CommitPrimitive(w1);

        if (direction <= HexDirection.SE && neighbor != null)
        {
            var bridge = HexMetrics.GetWaterBridge(direction);
            var e1 = c1 + bridge;
            var e2 = c2 + bridge;

            indices = indices with { Y = neighbor.Index };

            var wquad = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            wquad.AddQuadPerturbedVertices(c1, c2, e1, e2);
            wquad.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
            water.CommitPrimitive(wquad);

            if (direction <= HexDirection.E)
            {
                var nextNeighbor = cell.GetNeighbor(direction.Next());
                if (nextNeighbor == null || !nextNeighbor.IsUnderwater)
                    return;

                indices = indices with { Z = nextNeighbor.Index };

                var w2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
                w2.AddTrianglePerturbedVertices(
                    c2,
                    e2,
                    c2 + HexMetrics.GetWaterBridge(direction.Next())
                );
                w2.AddTriangleCellData(indices, SplatWeights1, SplatWeights2, SplatWeights3);
                water.CommitPrimitive(w2);
            }
        }
    }

    private void TriangulateShoreWater(
        HexDirection direction, HexCell cell, HexCell neighbor, Vector3 center)
    {
        var e1 = new EdgeVertices(
            center + HexMetrics.GetFirstWaterCorner(direction),
            center + HexMetrics.GetSecondWaterCorner(direction)
        );

        var indices = new Vector3(cell.Index, neighbor.Index, cell.Index);

        var w1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        w1.AddTrianglePerturbedVertices(center, e1.V1, e1.V2);
        w1.AddTriangleCellDataUniform(indices, SplatWeights1);
        water.CommitPrimitive(w1);

        var w2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        w2.AddTrianglePerturbedVertices(center, e1.V2, e1.V3);
        w2.AddTriangleCellDataUniform(indices, SplatWeights1);
        water.CommitPrimitive(w2);

        var w3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        w3.AddTrianglePerturbedVertices(center, e1.V3, e1.V4);
        w3.AddTriangleCellDataUniform(indices, SplatWeights1);
        water.CommitPrimitive(w3);

        var w4 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        w4.AddTrianglePerturbedVertices(center, e1.V4, e1.V5);
        w4.AddTriangleCellDataUniform(indices, SplatWeights1);
        water.CommitPrimitive(w4);

        var center2 = neighbor.Position;
        center2 = center2 with { Y = center.Y };
        if (neighbor.ColumnIndex < cell.ColumnIndex - 1)
        {
            center2 = center2 with { X = center2.X + HexMetrics.WrapSize * HexMetrics.InnerDiameter };
        }
        else if (neighbor.ColumnIndex > cell.ColumnIndex + 1)
        {
            center2 = center2 with { X = center2.X - HexMetrics.WrapSize * HexMetrics.InnerDiameter };
        }

        var e2 = new EdgeVertices(
            center2 + HexMetrics.GetSecondSolidCorner(direction.Opposite()),
            center2 + HexMetrics.GetFirstSolidCorner(direction.Opposite())
        );

        if (cell.HasRiverThroughEdge(direction))
        {
            TriangulateEstuary(e1, e2, cell.HasIncomingRiver && cell.IncomingRiverDirection == direction, indices);
        }
        else
        {
            var ws1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            ws1.AddQuadPerturbedVertices(e1.V1, e1.V2, e2.V1, e2.V2);
            ws1.AddQuadUv1Floats(0, 0, 0, 1);
            ws1.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
            waterShore.CommitPrimitive(ws1);

            var ws2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            ws2.AddQuadPerturbedVertices(e1.V2, e1.V3, e2.V2, e2.V3);
            ws2.AddQuadUv1Floats(0, 0, 0, 1);
            ws2.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
            waterShore.CommitPrimitive(ws2);

            var ws3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            ws3.AddQuadPerturbedVertices(e1.V3, e1.V4, e2.V3, e2.V4);
            ws3.AddQuadUv1Floats(0, 0, 0, 1);
            ws3.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
            waterShore.CommitPrimitive(ws3);

            var ws4 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            ws4.AddQuadPerturbedVertices(e1.V4, e1.V5, e2.V4, e2.V5);
            ws4.AddQuadUv1Floats(0, 0, 0, 1);
            ws4.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
            waterShore.CommitPrimitive(ws4);
        }

        var nextNeighbor = cell.GetNeighbor(direction.Next());
        if (nextNeighbor != null)
        {
            var center3 = nextNeighbor.Position;
            if (nextNeighbor.ColumnIndex < cell.ColumnIndex - 1)
            {
                center3 = center3 with { X = center3.X + HexMetrics.WrapSize * HexMetrics.InnerDiameter };
            }
            else if (nextNeighbor.ColumnIndex > cell.ColumnIndex + 1)
            {
                center3 = center3 with { X = center3.X - HexMetrics.WrapSize * HexMetrics.InnerDiameter };
            }

            var v3 = nextNeighbor.IsUnderwater ?
                center3 + HexMetrics.GetFirstWaterCorner(direction.Previous()) :
                center3 + HexMetrics.GetFirstSolidCorner(direction.Previous());
            v3 = v3 with { Y = center.Y };

            var v_val = nextNeighbor.IsUnderwater ? 0f : 1f;

            indices = indices with { Z = nextNeighbor.Index };

            var wsTri = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            wsTri.AddTrianglePerturbedVertices(e1.V5, e2.V5, v3);
            wsTri.AddTriangleUv1(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, v_val));
            wsTri.AddTriangleCellData(indices, SplatWeights1, SplatWeights2, SplatWeights3);
            waterShore.CommitPrimitive(wsTri);
        }
    }

    private void TriangulateWaterfallInWater(
        Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4,
        float y1, float y2, float waterY, Vector3 indices)
    {
        v1 = v1 with { Y = y1 };
        v2 = v2 with { Y = y1 };
        v3 = v3 with { Y = y2 };
        v4 = v4 with { Y = y2 };

        v1 = HexMetrics.Perturb(v1);
        v2 = HexMetrics.Perturb(v2);
        v3 = HexMetrics.Perturb(v3);
        v4 = HexMetrics.Perturb(v4);

        float t = (waterY - y2) / (y1 - y2);
        v3 = v3.Lerp(v1, t);
        v4 = v4.Lerp(v2, t);

        var r1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        r1.AddQuadUnperturbedVertices(v1, v2, v3, v4);
        r1.AddQuadUv1Floats(0, 1, 0.8f, 1);
        r1.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
        rivers.CommitPrimitive(r1);
    }

    private void TriangulateEstuary(EdgeVertices e1, EdgeVertices e2, bool incomingRiver, Vector3 indices)
    {
        var ws1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        ws1.AddTrianglePerturbedVertices(e2.V1, e1.V2, e1.V1);
        ws1.AddTriangleUv1(new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 0));
        ws1.AddTriangleCellData(indices, SplatWeights2, SplatWeights1, SplatWeights1);
        waterShore.CommitPrimitive(ws1);

        var ws2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        ws2.AddTrianglePerturbedVertices(e2.V5, e1.V5, e1.V4);
        ws2.AddTriangleUv1(new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 0));
        ws2.AddTriangleCellData(indices, SplatWeights2, SplatWeights1, SplatWeights1);
        waterShore.CommitPrimitive(ws2);

        if (incomingRiver)
        {
            var est1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            est1.AddQuadPerturbedVertices(e2.V1, e1.V2, e2.V2, e1.V3);
            est1.AddQuadUv1Vectors(
                new Vector2(0, 1), new Vector2(0, 0),
                new Vector2(1, 1), new Vector2(0, 0)
            );
            est1.AddQuadUv2Vectors(
                new Vector2(1.5f, 1), new Vector2(0.7f, 1.15f),
                new Vector2(1, 0.8f), new Vector2(0.5f, 1.1f)
            );
            est1.AddQuadCellData(indices, SplatWeights2, SplatWeights1, SplatWeights2, SplatWeights1);
            estuaries.CommitPrimitive(est1);

            var est2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            est2.AddTrianglePerturbedVertices(e1.V3, e2.V2, e2.V4);
            est2.AddTriangleUv1(
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 1)
            );
            est2.AddTriangleUv2(
                new Vector2(0.5f, 1.1f), new Vector2(1, 0.8f), new Vector2(0, 0.8f)
            );
            est2.AddTriangleCellData(indices, SplatWeights1, SplatWeights2, SplatWeights2);
            estuaries.CommitPrimitive(est2);

            var est3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            est3.AddQuadPerturbedVertices(e1.V3, e1.V4, e2.V4, e2.V5);
            est3.AddQuadUv1Vectors(
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(1, 1), new Vector2(0, 1)
            );
            est3.AddQuadUv2Vectors(
                new Vector2(0.5f, 1.1f), new Vector2(0.3f, 1.15f),
                new Vector2(0, 0.8f), new Vector2(-0.5f, 1)
            );
            est3.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
            estuaries.CommitPrimitive(est3);
        }
        else
        {
            var est1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            est1.AddQuadPerturbedVertices(e2.V1, e1.V2, e2.V2, e1.V3);
            est1.AddQuadUv1Vectors(
                new Vector2(0, 1), new Vector2(0, 0),
                new Vector2(1, 1), new Vector2(0, 0)
            );
            est1.AddQuadUv2Vectors(
                new Vector2(-0.5f, -0.2f), new Vector2(0.3f, -0.35f),
                new Vector2(0, 0), new Vector2(0.5f, -0.3f)
            );
            est1.AddQuadCellData(indices, SplatWeights2, SplatWeights1, SplatWeights2, SplatWeights1);
            estuaries.CommitPrimitive(est1);

            var est2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            est2.AddTrianglePerturbedVertices(e1.V3, e2.V2, e2.V4);
            est2.AddTriangleUv1(
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 1)
            );
            est2.AddTriangleUv2(
                new Vector2(0.5f, -0.3f), new Vector2(0, 0), new Vector2(1, 0)
            );
            est2.AddTriangleCellData(indices, SplatWeights1, SplatWeights2, SplatWeights2);
            estuaries.CommitPrimitive(est2);

            var est3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            est3.AddQuadPerturbedVertices(e1.V3, e1.V4, e2.V4, e2.V5);
            est3.AddQuadUv1Vectors(
                new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(1, 1), new Vector2(0, 1)
            );
            est3.AddQuadUv2Vectors(
                new Vector2(0.5f, -0.3f), new Vector2(0.7f, -0.35f),
                new Vector2(1, 0), new Vector2(1.5f, -0.2f)
            );
            est3.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
            estuaries.CommitPrimitive(est3);
        }
    }
}
