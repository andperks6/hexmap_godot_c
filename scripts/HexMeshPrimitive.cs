using Godot;
using System.Collections.Generic;

public class HexMeshPrimitive
{
    #region Enums

    public enum PrimitiveType
    {
        Quad,
        Triangle
    }

    #endregion

    #region Private Fields

    private readonly PrimitiveType _primitiveType;
    private readonly List<Vector3> _vertices = new();
    private readonly List<Vector2> _uv1 = new();
    private readonly List<Vector2> _uv2 = new();
    private readonly List<Vector3> _cellIndices = new();
    private readonly List<Color> _cellWeights = new();

    #endregion

    #region Constructor

    public HexMeshPrimitive(PrimitiveType primitiveType)
    {
        _primitiveType = primitiveType;
    }

    #endregion

    #region Public Methods

    public void AddTriangleUnperturbedVertices(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        _vertices.Add(v1);
        _vertices.Add(v2);
        _vertices.Add(v3);
    }

    public void AddTrianglePerturbedVertices(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        _vertices.Add(HexMetrics.Perturb(v1));
        _vertices.Add(HexMetrics.Perturb(v2));
        _vertices.Add(HexMetrics.Perturb(v3));
    }

    public void AddQuadUnperturbedVertices(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
    {
        _vertices.Add(v1);
        _vertices.Add(v2);
        _vertices.Add(v3);
        _vertices.Add(v4);
    }

    public void AddQuadPerturbedVertices(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
    {
        _vertices.Add(HexMetrics.Perturb(v1));
        _vertices.Add(HexMetrics.Perturb(v2));
        _vertices.Add(HexMetrics.Perturb(v3));
        _vertices.Add(HexMetrics.Perturb(v4));
    }

    public void AddTriangleUv1(Vector2 v1, Vector2 v2, Vector2 v3)
    {
        _uv1.Add(v1);
        _uv1.Add(v2);
        _uv1.Add(v3);
    }

    public void AddTriangleUv2(Vector2 v1, Vector2 v2, Vector2 v3)
    {
        _uv2.Add(v1);
        _uv2.Add(v2);
        _uv2.Add(v3);
    }

    public void AddQuadUv1Floats(float uMin, float uMax, float vMin, float vMax)
    {
        var uv1 = new Vector2(uMin, vMin);
        var uv2 = new Vector2(uMax, vMin);
        var uv3 = new Vector2(uMin, vMax);
        var uv4 = new Vector2(uMax, vMax);

        AddQuadUv1Vectors(uv1, uv2, uv3, uv4);
    }

    public void AddQuadUv2Floats(float uMin, float uMax, float vMin, float vMax)
    {
        var uv1 = new Vector2(uMin, vMin);
        var uv2 = new Vector2(uMax, vMin);
        var uv3 = new Vector2(uMin, vMax);
        var uv4 = new Vector2(uMax, vMax);

        AddQuadUv2Vectors(uv1, uv2, uv3, uv4);
    }

    public void AddQuadUv1Vectors(Vector2 v1, Vector2 v2, Vector2 v3, Vector2 v4)
    {
        _uv1.Add(v1);
        _uv1.Add(v2);
        _uv1.Add(v3);
        _uv1.Add(v4);
    }

    public void AddQuadUv2Vectors(Vector2 v1, Vector2 v2, Vector2 v3, Vector2 v4)
    {
        _uv2.Add(v1);
        _uv2.Add(v2);
        _uv2.Add(v3);
        _uv2.Add(v4);
    }

    public void AddTriangleCellData(Vector3 indices, Color weights1, Color weights2, Color weights3)
    {
        _cellIndices.Add(indices);
        _cellIndices.Add(indices);
        _cellIndices.Add(indices);
        _cellWeights.Add(weights1);
        _cellWeights.Add(weights2);
        _cellWeights.Add(weights3);
    }

    public void AddTriangleCellDataUniform(Vector3 indices, Color weights)
    {
        AddTriangleCellData(indices, weights, weights, weights);
    }

    public void AddQuadCellData(Vector3 indices, Color weights1, Color weights2, Color weights3, Color weights4)
    {
        _cellIndices.Add(indices);
        _cellIndices.Add(indices);
        _cellIndices.Add(indices);
        _cellIndices.Add(indices);
        _cellWeights.Add(weights1);
        _cellWeights.Add(weights2);
        _cellWeights.Add(weights3);
        _cellWeights.Add(weights4);
    }

    public void AddQuadCellDataDual(Vector3 indices, Color weights1, Color weights2)
    {
        AddQuadCellData(indices, weights1, weights1, weights2, weights2);
    }

    public void AddQuadCellDataUnified(Vector3 indices, Color weights)
    {
        AddQuadCellData(indices, weights, weights, weights, weights);
    }

    public void Commit(SurfaceTool st)
    {
        if (_primitiveType == PrimitiveType.Quad)
        {
            CommitQuad(st);
        }
        else
        {
            CommitTriangle(st);
        }
    }

    #endregion

    #region Private Methods

    private void CommitVertex(SurfaceTool st, int vertexIdx)
    {
        // Set the color of the vertex
        if (_cellWeights.Count > vertexIdx)
        {
            st.SetColor(_cellWeights[vertexIdx]);
        }

        // Set the uv1 of the vertex
        if (_uv1.Count > vertexIdx)
        {
            st.SetUV(_uv1[vertexIdx]);
        }

        // Set the uv2 of the vertex
        if (_uv2.Count > vertexIdx)
        {
            st.SetUV2(_uv2[vertexIdx]);
        }

        // Set the terrain index of the vertex
        if (_cellIndices.Count > vertexIdx)
        {
            var t = _cellIndices[vertexIdx];
            var c = new Color(t.X, t.Y, t.Z, 0);
            st.SetCustom(0, c);
        }

        // Add the vertex itself
        if (_vertices.Count > vertexIdx)
        {
            st.AddVertex(_vertices[vertexIdx]);
        }
    }

    private void CommitTriangleWithIndices(SurfaceTool st, int[] indices)
    {
        // Add the first vertex
        CommitVertex(st, indices[0]);

        // Add the second vertex
        CommitVertex(st, indices[1]);

        // Add the third vertex
        CommitVertex(st, indices[2]);
    }

    private void CommitTriangle(SurfaceTool st)
    {
        CommitTriangleWithIndices(st, new[] { 0, 2, 1 });
    }

    private void CommitQuad(SurfaceTool st)
    {
        CommitTriangleWithIndices(st, new[] { 0, 1, 2 });
        CommitTriangleWithIndices(st, new[] { 1, 3, 2 });
    }

    #endregion
}