using Godot;

public class HexTerrainTriangulator
{
    private static readonly Color w1 = new(1f, 0f, 0f);
    private static readonly Color w2 = new(0f, 1f, 0f);
    private static readonly Color SplatWeights3 = new(0f, 0f, 1f);

    private readonly HexMeshCollection meshes;

    public HexTerrainTriangulator(HexMeshCollection meshes)
    {
        this.meshes = meshes;
    }


    public void TriangulateCorner(
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
            t1.AddTriangleCellData(indices, w1, w2, SplatWeights3);
            meshes.Terrain.CommitPrimitive(t1);
        }
    }

    private void TriangulateCornerTerraces(
        Vector3 begin, HexCell beginCell,
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell)
    {
        Vector3 v3 = HexMetrics.TerraceLerp(begin, left, 1);
        Vector3 v4 = HexMetrics.TerraceLerp(begin, right, 1);
        Color w3 = HexMetrics.TerraceColorLerp(w1, w2, 1);
        Color w4 = HexMetrics.TerraceColorLerp(w1, SplatWeights3, 1);

        var indices = new Vector3(beginCell.Index, leftCell.Index, rightCell.Index);

        var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t1.AddTrianglePerturbedVertices(begin, v3, v4);
        t1.AddTriangleCellData(indices, w1, w3, w4);
        meshes.Terrain.CommitPrimitive(t1);

        for (int i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            Vector3 v1 = v3;
            Vector3 v2 = v4;
            Color w1 = w3;
            Color w2 = w4;

            v3 = HexMetrics.TerraceLerp(begin, left, i);
            v4 = HexMetrics.TerraceLerp(begin, right, i);
            w3 = HexMetrics.TerraceColorLerp(w1, w2, i);
            w4 = HexMetrics.TerraceColorLerp(w1, SplatWeights3, i);

            var q1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            q1.AddQuadPerturbedVertices(v1, v2, v3, v4);
            q1.AddQuadCellData(indices, w1, w2, w3, w4);
            meshes.Terrain.CommitPrimitive(q1);
        }

        var q2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        q2.AddQuadPerturbedVertices(v3, v4, left, right);
        q2.AddQuadCellData(indices, w3, w4, w2, SplatWeights3);
        meshes.Terrain.CommitPrimitive(q2);
    }

    private void TriangulateCornerTerracesCliff(
        Vector3 begin, HexCell beginCell,
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell)
    {
        float b = 1f / (rightCell.Elevation - beginCell.Elevation);
        if (b < 0) b = -b;

        Vector3 boundary = HexMetrics.Perturb(begin).Lerp(HexMetrics.Perturb(right), b);
        Color boundaryWeights = w1.Lerp(SplatWeights3, b);

        var indices = new Vector3(beginCell.Index, leftCell.Index, rightCell.Index);

        TriangulateBoundaryTriangle(
            begin, w1,
            left, w2,
            boundary, boundaryWeights,
            indices
        );

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateBoundaryTriangle(
                left, w2,
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
            t1.AddTriangleCellData(indices, w2, SplatWeights3, boundaryWeights);
            meshes.Terrain.CommitPrimitive(t1);
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
        Color boundaryWeights = w1.Lerp(w2, b);

        var indices = new Vector3(beginCell.Index, leftCell.Index, rightCell.Index);

        TriangulateBoundaryTriangle(
            right, SplatWeights3,
            begin, w1,
            boundary, boundaryWeights,
            indices
        );

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateBoundaryTriangle(
                left, w2,
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
            t1.AddTriangleCellData(indices, w2, SplatWeights3, boundaryWeights);
            meshes.Terrain.CommitPrimitive(t1);
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
        meshes.Terrain.CommitPrimitive(t1);

        for (int i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            Vector3 v1 = v2;
            Color w1 = w2;

            v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, i));
            w2 = HexMetrics.TerraceColorLerp(beginWeights, leftWeights, i);

            var t2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            t2.AddTriangleUnperturbedVertices(v1, v2, boundary);
            t2.AddTriangleCellData(indices, w1, w2, boundaryWeights);
            meshes.Terrain.CommitPrimitive(t2);
        }

        var t3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t3.AddTriangleUnperturbedVertices(v2, HexMetrics.Perturb(left), boundary);
        t3.AddTriangleCellData(indices, w2, leftWeights, boundaryWeights);
        meshes.Terrain.CommitPrimitive(t3);
    }

    public void TriangulateEdgeStrip(
        EdgeVertices e1, float index1,
        EdgeVertices e2, float index2,
        bool hasRoad = false)
    {
        var indices = new Vector3(index1, index2, index1);

        var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t1.AddQuadPerturbedVertices(e1.V1, e1.V2, e2.V1, e2.V2);
        t1.AddQuadCellDataDual(indices, w1, w2);
        meshes.Terrain.CommitPrimitive(t1);

        var t2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t2.AddQuadPerturbedVertices(e1.V2, e1.V3, e2.V2, e2.V3);
        t2.AddQuadCellDataDual(indices, w1, w2);
        meshes.Terrain.CommitPrimitive(t2);

        var t3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t3.AddQuadPerturbedVertices(e1.V3, e1.V4, e2.V3, e2.V4);
        t3.AddQuadCellDataDual(indices, w1, w2);
        meshes.Terrain.CommitPrimitive(t3);

        var t4 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t4.AddQuadPerturbedVertices(e1.V4, e1.V5, e2.V4, e2.V5);
        t4.AddQuadCellDataDual(indices, w1, w2);
        meshes.Terrain.CommitPrimitive(t4);

        if (hasRoad)
        {
            TriangulateRoadSegment(
                e1.V2, e1.V3, e1.V4,
                e2.V2, e2.V3, e2.V4,
                w1, w2, indices
            );
        }
    }
    public void TriangulateEdgeStrip(
      EdgeVertices e1, Color w1, float index1,
        EdgeVertices e2, Color w2, float index2,
        bool hasRoad = false)
    {
        var indices = new Vector3(index1, index2, index1);

        var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t1.AddQuadPerturbedVertices(e1.V1, e1.V2, e2.V1, e2.V2);
        t1.AddQuadCellDataDual(indices, w1, w2);
        meshes.Terrain.CommitPrimitive(t1);

        var t2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t2.AddQuadPerturbedVertices(e1.V2, e1.V3, e2.V2, e2.V3);
        t2.AddQuadCellDataDual(indices, w1, w2);
        meshes.Terrain.CommitPrimitive(t2);

        var t3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t3.AddQuadPerturbedVertices(e1.V3, e1.V4, e2.V3, e2.V4);
        t3.AddQuadCellDataDual(indices, w1, w2);
        meshes.Terrain.CommitPrimitive(t3);

        var t4 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t4.AddQuadPerturbedVertices(e1.V4, e1.V5, e2.V4, e2.V5);
        t4.AddQuadCellDataDual(indices, w1, w2);
        meshes.Terrain.CommitPrimitive(t4);

        if (hasRoad)
        {
            TriangulateRoadSegment(
                e1.V2, e1.V3, e1.V4,
                e2.V2, e2.V3, e2.V4,
                w1, w2, indices
            );
        }
    }

    public void TriangulateEdgeFan(Vector3 center, EdgeVertices edge, float index)
    {
        var indices = new Vector3(index, index, index);
        Color color = w1;

        var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t1.AddTrianglePerturbedVertices(center, edge.V1, edge.V2);
        t1.AddTriangleCellDataUniform(indices, color);
        meshes.Terrain.CommitPrimitive(t1);

        var t2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t2.AddTrianglePerturbedVertices(center, edge.V2, edge.V3);
        t2.AddTriangleCellDataUniform(indices, color);
        meshes.Terrain.CommitPrimitive(t2);

        var t3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t3.AddTrianglePerturbedVertices(center, edge.V3, edge.V4);
        t3.AddTriangleCellDataUniform(indices, color);
        meshes.Terrain.CommitPrimitive(t3);

        var t4 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t4.AddTrianglePerturbedVertices(center, edge.V4, edge.V5);
        t4.AddTriangleCellDataUniform(indices, color);
        meshes.Terrain.CommitPrimitive(t4);
    }

    public void TriangulateRoad(
        Vector3 center, Vector3 mL, Vector3 mR,
        EdgeVertices e, bool hasRoadThroughCellEdge, float index)
    {
        if (hasRoadThroughCellEdge)
        {
            var indices = new Vector3(index, index, index);
            var mC = mL.Lerp(mR, 0.5f);
            TriangulateRoadSegment(mL, mC, mR, e.V2, e.V3, e.V4, w1, w1, indices);

            center = center with { Y = center.Y + 0.1f };
            mC = mC with { Y = mC.Y + 0.1f };
            mL = mL with { Y = mL.Y + 0.1f };
            mR = mR with { Y = mR.Y + 0.1f };

            var road1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            road1.AddTrianglePerturbedVertices(center, mL, mC);
            road1.AddTriangleUv1(new Vector2(1, 0), new Vector2(0, 0), new Vector2(1, 0));
            road1.AddTriangleCellDataUniform(indices, w1);
            meshes.Roads.CommitPrimitive(road1);

            var road2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            road2.AddTrianglePerturbedVertices(center, mC, mR);
            road2.AddTriangleUv1(new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 0));
            road2.AddTriangleCellDataUniform(indices, w1);
            meshes.Roads.CommitPrimitive(road2);
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
        road.AddTriangleCellDataUniform(indices, w1);
        meshes.Roads.CommitPrimitive(road);
    }

    public void TriangulateRoadSegment(
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
        meshes.Roads.CommitPrimitive(road1);

        var road2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        road2.AddQuadPerturbedVertices(v2, v3, v5, v6);
        road2.AddQuadUv1Floats(1, 0, 0, 0);
        road2.AddQuadCellDataDual(indices, w1, w2);
        meshes.Roads.CommitPrimitive(road2);
    }

    public void TriangulateRiverQuad(
        Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4,
        float y1, float v, bool reversed, Vector3 indices)
    {
        TriangulateRiverQuad(v1, v2, v3, v4, y1, y1, v, reversed, indices);
    }

    public void TriangulateRiverQuad(
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

        river.AddQuadCellDataDual(indices, w1, w2);
        meshes.Rivers.CommitPrimitive(river);
    }

    public void TriangulateRiverTriangle(Vector3 center, Vector3 v2, Vector3 v4, bool reversed, Vector3 indices)
    {
        var primitive = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        primitive.AddTrianglePerturbedVertices(center, v2, v4);
        if (reversed)
        {
            primitive.AddTriangleUv1(
                new Vector2(0.5f, 0.4f),
                new Vector2(1f, 0.2f),
                new Vector2(0f, 0.2f)
            );
        }
        else
        {
            primitive.AddTriangleUv1(
                new Vector2(0.5f, 0.4f),
                new Vector2(0f, 0.6f),
                new Vector2(1f, 0.6f)
            );
        }
        primitive.AddTriangleCellDataUniform(indices, w1);
        meshes.Rivers.CommitPrimitive(primitive);
    }



    public void TriangulateRiverCenter(Vector3 centerL, Vector3 center, Vector3 centerR, EdgeVertices m, Vector3 indices)
    {
        var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t1.AddTrianglePerturbedVertices(centerL, m.V1, m.V2);
        t1.AddTriangleCellDataUniform(indices, w1);
        meshes.Terrain.CommitPrimitive(t1);

        var t2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t2.AddQuadPerturbedVertices(centerL, center, m.V2, m.V3);
        t2.AddQuadCellDataUnified(indices, w1);
        meshes.Terrain.CommitPrimitive(t2);

        var t3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        t3.AddQuadPerturbedVertices(center, centerR, m.V3, m.V4);
        t3.AddQuadCellDataUnified(indices, w1);
        meshes.Terrain.CommitPrimitive(t3);

        var t4 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t4.AddTrianglePerturbedVertices(centerR, m.V4, m.V5);
        t4.AddTriangleCellDataUniform(indices, w1);
        meshes.Terrain.CommitPrimitive(t4);
    }
}