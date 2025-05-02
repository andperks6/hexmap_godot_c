using Godot;

public class HexRoadRiverTriangulator
{
    private static readonly Color w1 = HexMeshConstants.SplatWeights1;
    private static readonly Color w2 = HexMeshConstants.SplatWeights2;

    private readonly HexMeshCollection meshes;

    public HexRoadRiverTriangulator(HexMeshCollection meshes)
    {
        this.meshes = meshes;
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

            center = center with { Y = center.Y + HexMeshConstants.RoadHeightOffset };
            mC = mC with { Y = mC.Y + HexMeshConstants.RoadHeightOffset };
            mL = mL with { Y = mL.Y + HexMeshConstants.RoadHeightOffset };
            mR = mR with { Y = mR.Y + HexMeshConstants.RoadHeightOffset };

            var road1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            road1.AddTrianglePerturbedVertices(center, mL, mC);
            road1.AddTriangleUv1(HexMeshConstants.RoadUV_10, HexMeshConstants.RoadUV_00, HexMeshConstants.RoadUV_10);
            road1.AddTriangleCellDataUniform(indices, w1);
            meshes.Roads.CommitPrimitive(road1);

            var road2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
            road2.AddTrianglePerturbedVertices(center, mC, mR);
            road2.AddTriangleUv1(HexMeshConstants.RoadUV_10, HexMeshConstants.RoadUV_10, HexMeshConstants.RoadUV_00);
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
        center = center with { Y = center.Y + HexMeshConstants.RoadHeightOffset };
        mR = mR with { Y = mR.Y + HexMeshConstants.RoadHeightOffset };
        mL = mL with { Y = mL.Y + HexMeshConstants.RoadHeightOffset };

        var indices = new Vector3(index, index, index);

        var road = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        road.AddTrianglePerturbedVertices(center, mL, mR);
        road.AddTriangleUv1(HexMeshConstants.RoadUV_10, HexMeshConstants.RoadUV_00, HexMeshConstants.RoadUV_00);
        road.AddTriangleCellDataUniform(indices, w1);
        meshes.Roads.CommitPrimitive(road);
    }

    public void TriangulateRoadSegment(
        Vector3 v1, Vector3 v2, Vector3 v3,
        Vector3 v4, Vector3 v5, Vector3 v6,
        Color w1, Color w2, Vector3 indices)
    {
        v1 = v1 with { Y = v1.Y + HexMeshConstants.RoadSegmentHeightOffset };
        v2 = v2 with { Y = v2.Y + HexMeshConstants.RoadSegmentHeightOffset };
        v3 = v3 with { Y = v3.Y + HexMeshConstants.RoadSegmentHeightOffset };
        v4 = v4 with { Y = v4.Y + HexMeshConstants.RoadSegmentHeightOffset };
        v5 = v5 with { Y = v5.Y + HexMeshConstants.RoadSegmentHeightOffset };
        v6 = v6 with { Y = v6.Y + HexMeshConstants.RoadSegmentHeightOffset };

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
                HexMeshConstants.RiverUV_Center,
                HexMeshConstants.RiverUV_ReverseLeft,
                HexMeshConstants.RiverUV_ReverseRight
            );
        }
        else
        {
            primitive.AddTriangleUv1(
                HexMeshConstants.RiverUV_Center,
                HexMeshConstants.RiverUV_Left,
                HexMeshConstants.RiverUV_Right
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