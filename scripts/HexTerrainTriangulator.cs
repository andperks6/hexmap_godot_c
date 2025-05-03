using Godot;

public class HexTerrainTriangulator
{
    private static readonly Color w1 = HexMeshConstants.SplatWeights1;
    private static readonly Color w2 = HexMeshConstants.SplatWeights2;
    private static readonly Color SplatWeights3 = HexMeshConstants.SplatWeights3;

    private readonly HexMeshCollection meshes;
    private readonly HexRoadRiverTriangulator roadRiverTriangulator;

    public HexTerrainTriangulator(HexMeshCollection meshes)
    {
        this.meshes = meshes;
        this.roadRiverTriangulator = new HexRoadRiverTriangulator(meshes);
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
            t1.AddTriangleCellData(indices, HexMeshConstants.SplatWeights1, HexMeshConstants.SplatWeights2, HexMeshConstants.SplatWeights3);
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
        Color w3 = HexMetrics.TerraceColorLerp(HexMeshConstants.SplatWeights1, HexMeshConstants.SplatWeights2, 1);
        Color w4 = HexMetrics.TerraceColorLerp(HexMeshConstants.SplatWeights1, HexMeshConstants.SplatWeights3, 1);

        var indices = new Vector3(beginCell.Index, leftCell.Index, rightCell.Index);

        var t1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        t1.AddTrianglePerturbedVertices(begin, v3, v4);
        t1.AddTriangleCellData(indices, HexMeshConstants.SplatWeights1, w3, w4);
        meshes.Terrain.CommitPrimitive(t1);

        for (int i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            Vector3 v1 = v3;
            Vector3 v2 = v4;
            Color w1 = w3;
            Color w2 = w4;

            v3 = HexMetrics.TerraceLerp(begin, left, i);
            v4 = HexMetrics.TerraceLerp(begin, right, i);
            w3 = HexMetrics.TerraceColorLerp(HexMeshConstants.SplatWeights1, HexMeshConstants.SplatWeights2, i);
            w4 = HexMetrics.TerraceColorLerp(HexMeshConstants.SplatWeights1, HexMeshConstants.SplatWeights3, i);

            var q1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            q1.AddQuadPerturbedVertices(v1, v2, v3, v4);
            q1.AddQuadCellData(indices, w1, w2, w3, w4);
            meshes.Terrain.CommitPrimitive(q1);
        }

        var q2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        q2.AddQuadPerturbedVertices(v3, v4, left, right);
        q2.AddQuadCellData(indices, w3, w4, HexMeshConstants.SplatWeights2, HexMeshConstants.SplatWeights3);
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
        Color boundaryWeights = HexMeshConstants.SplatWeights1.Lerp(HexMeshConstants.SplatWeights3, b);

        var indices = new Vector3(beginCell.Index, leftCell.Index, rightCell.Index);

        TriangulateBoundaryTriangle(
            begin, HexMeshConstants.SplatWeights1,
            left, HexMeshConstants.SplatWeights2,
            boundary, boundaryWeights,
            indices
        );

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateBoundaryTriangle(
                left, HexMeshConstants.SplatWeights2,
                right, HexMeshConstants.SplatWeights3,
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
            t1.AddTriangleCellData(indices, HexMeshConstants.SplatWeights2, HexMeshConstants.SplatWeights3, boundaryWeights);
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
        Color boundaryWeights = HexMeshConstants.SplatWeights1.Lerp(HexMeshConstants.SplatWeights2, b);

        var indices = new Vector3(beginCell.Index, leftCell.Index, rightCell.Index);

        TriangulateBoundaryTriangle(
            right, HexMeshConstants.SplatWeights3,
            begin, HexMeshConstants.SplatWeights1,
            boundary, boundaryWeights,
            indices
        );

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateBoundaryTriangle(
                left, HexMeshConstants.SplatWeights2,
                right, HexMeshConstants.SplatWeights3,
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
            t1.AddTriangleCellData(indices, HexMeshConstants.SplatWeights2, HexMeshConstants.SplatWeights3, boundaryWeights);
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
            roadRiverTriangulator.TriangulateRoadSegment(
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
            roadRiverTriangulator.TriangulateRoadSegment(
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
}