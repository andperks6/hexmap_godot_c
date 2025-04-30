using Godot;

public class HexWaterTriangulator
{
    private static readonly Color SplatWeights1 = new(1f, 0f, 0f);
    private static readonly Color SplatWeights2 = new(0f, 1f, 0f);
    private static readonly Color SplatWeights3 = new(0f, 0f, 1f);

    private readonly HexMeshCollection meshes;

    public HexWaterTriangulator(HexMeshCollection meshes)
    {
        this.meshes = meshes;
    }

    public void TriangulateWater(HexDirection direction, HexCell cell, Vector3 center)
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

    public void TriangulateOpenWater(
        HexDirection direction, HexCell cell, HexCell neighbor, Vector3 center)
    {
        var c1 = center + HexMetrics.GetFirstWaterCorner(direction);
        var c2 = center + HexMetrics.GetSecondWaterCorner(direction);

        var indices = new Vector3(cell.Index, cell.Index, cell.Index);

        var w1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        w1.AddTrianglePerturbedVertices(center, c1, c2);
        w1.AddTriangleCellDataUniform(indices, SplatWeights1);
        meshes.Water.CommitPrimitive(w1);

        if (direction <= HexDirection.SE && neighbor != null)
        {
            var bridge = HexMetrics.GetWaterBridge(direction);
            var e1 = c1 + bridge;
            var e2 = c2 + bridge;

            indices = indices with { Y = neighbor.Index };

            var wquad = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
            wquad.AddQuadPerturbedVertices(c1, c2, e1, e2);
            wquad.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
            meshes.Water.CommitPrimitive(wquad);

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
                meshes.Water.CommitPrimitive(w2);
            }
        }
    }

    public void TriangulateShoreWater(
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
        meshes.Water.CommitPrimitive(w1);

        var w2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        w2.AddTrianglePerturbedVertices(center, e1.V2, e1.V3);
        w2.AddTriangleCellDataUniform(indices, SplatWeights1);
        meshes.Water.CommitPrimitive(w2);

        var w3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        w3.AddTrianglePerturbedVertices(center, e1.V3, e1.V4);
        w3.AddTriangleCellDataUniform(indices, SplatWeights1);
        meshes.Water.CommitPrimitive(w3);

        var w4 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        w4.AddTrianglePerturbedVertices(center, e1.V4, e1.V5);
        w4.AddTriangleCellDataUniform(indices, SplatWeights1);
        meshes.Water.CommitPrimitive(w4);

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
            TriangulateWaterShore(e1, e2, indices);
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
            meshes.WaterShore.CommitPrimitive(wsTri);
        }
    }

    public void TriangulateWaterShore(EdgeVertices e1, EdgeVertices e2, Vector3 indices)
    {
        var ws1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        ws1.AddQuadPerturbedVertices(e1.V1, e1.V2, e2.V1, e2.V2);
        ws1.AddQuadUv1Floats(0, 0, 0, 1);
        ws1.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
        meshes.WaterShore.CommitPrimitive(ws1);

        var ws2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        ws2.AddQuadPerturbedVertices(e1.V2, e1.V3, e2.V2, e2.V3);
        ws2.AddQuadUv1Floats(0, 0, 0, 1);
        ws2.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
        meshes.WaterShore.CommitPrimitive(ws2);

        var ws3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        ws3.AddQuadPerturbedVertices(e1.V3, e1.V4, e2.V3, e2.V4);
        ws3.AddQuadUv1Floats(0, 0, 0, 1);
        ws3.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
        meshes.WaterShore.CommitPrimitive(ws3);

        var ws4 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        ws4.AddQuadPerturbedVertices(e1.V4, e1.V5, e2.V4, e2.V5);
        ws4.AddQuadUv1Floats(0, 0, 0, 1);
        ws4.AddQuadCellDataDual(indices, SplatWeights1, SplatWeights2);
        meshes.WaterShore.CommitPrimitive(ws4);
    }

    public void TriangulateWaterfallInWater(
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
        meshes.Rivers.CommitPrimitive(r1);
    }

    public void TriangulateEstuary(EdgeVertices e1, EdgeVertices e2, bool incomingRiver, Vector3 indices)
    {
        var ws1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        ws1.AddTrianglePerturbedVertices(e2.V1, e1.V2, e1.V1);
        ws1.AddTriangleUv1(new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 0));
        ws1.AddTriangleCellData(indices, SplatWeights2, SplatWeights1, SplatWeights1);
        meshes.WaterShore.CommitPrimitive(ws1);

        var ws2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        ws2.AddTrianglePerturbedVertices(e2.V5, e1.V5, e1.V4);
        ws2.AddTriangleUv1(new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 0));
        ws2.AddTriangleCellData(indices, SplatWeights2, SplatWeights1, SplatWeights1);
        meshes.WaterShore.CommitPrimitive(ws2);

        if (incomingRiver)
        {
            TriangulateEstuaryIncoming(e1, e2, indices);
        }
        else
        {
            TriangulateEstuaryOutgoing(e1, e2, indices);
        }
    }

    private void TriangulateEstuaryIncoming(EdgeVertices e1, EdgeVertices e2, Vector3 indices)
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
        meshes.Estuaries.CommitPrimitive(est1);

        var est2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        est2.AddTrianglePerturbedVertices(e1.V3, e2.V2, e2.V4);
        est2.AddTriangleUv1(
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 1)
        );
        est2.AddTriangleUv2(
            new Vector2(0.5f, 1.1f), new Vector2(1, 0.8f), new Vector2(0, 0.8f)
        );
        est2.AddTriangleCellData(indices, SplatWeights1, SplatWeights2, SplatWeights2);
        meshes.Estuaries.CommitPrimitive(est2);

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
        meshes.Estuaries.CommitPrimitive(est3);
    }

    private void TriangulateEstuaryOutgoing(EdgeVertices e1, EdgeVertices e2, Vector3 indices)
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
        meshes.Estuaries.CommitPrimitive(est1);

        var est2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        est2.AddTrianglePerturbedVertices(e1.V3, e2.V2, e2.V4);
        est2.AddTriangleUv1(
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 1)
        );
        est2.AddTriangleUv2(
            new Vector2(0.5f, -0.3f), new Vector2(0, 0), new Vector2(1, 0)
        );
        est2.AddTriangleCellData(indices, SplatWeights1, SplatWeights2, SplatWeights2);
        meshes.Estuaries.CommitPrimitive(est2);

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
        meshes.Estuaries.CommitPrimitive(est3);
    }
}