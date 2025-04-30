using Godot;
using System;

public partial class HexMesh : MeshInstance3D
{
    private readonly SurfaceTool _surfaceTool;
    
    [Export]
    public bool UseCollider { get; set; } = true;
    [Export]
    public bool UseUVCoordinates { get; set; }
    [Export]
    public bool UseUV2Coordinates { get; set; }
    [Export]
    public bool UseCellData { get; set; }
    [Export]
    public bool GenerateTangents { get; set; }
    // [Export]
    // public float SortingOffset { get; set; }
    
    public HexMesh()
    {
        // Initialize SurfaceTool in constructor to avoid repeated instantiation
        _surfaceTool = new SurfaceTool();
    }
    
    public void Begin()
    {
        // Begin creating the mesh
        _surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
        
        // Set the first custom channel to be RGB float
        _surfaceTool.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);
        
        // Set the smooth group to -1, which produces flat normals for the mesh
        _surfaceTool.SetSmoothGroup(UInt32.MaxValue);
    }

    public void CommitPrimitive(HexMeshPrimitive primitive)
    {
        primitive.Commit(_surfaceTool);
    }

    public void End(Material material)
    {
        // Generate the normals for the mesh
        _surfaceTool.GenerateNormals();
        
        // Generate the tangents for the mesh only if needed
        if (GenerateTangents && UseUVCoordinates)
        {
            _surfaceTool.GenerateTangents();
        }
        
        // Cache the mesh creation to reduce interop calls
        var newMesh = _surfaceTool.Commit();
        Mesh = newMesh;
        
        // Create the collision object for the mesh if needed
        if (UseCollider)
        {
            CreateTrimeshCollision();
        }
        
        // Set the material for the mesh
        MaterialOverride = material;
    }
    
    public override void _Ready()
    {
        if (SortingOffset != 0)
        {
            // Apply sorting offset to affect render order
            Position = Position with { Y = Position.Y + SortingOffset };
        }
    }
}