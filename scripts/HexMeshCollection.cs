using Godot;
using System.Collections.Generic;

public class HexMeshCollection
{
    private readonly Dictionary<HexMaterialType, HexMesh> meshes;
    private readonly HexMaterialManager materialManager;

    public HexMaterialManager MaterialManager => materialManager;
    public HexMesh Terrain => meshes[HexMaterialType.Terrain];
    public HexMesh Rivers => meshes[HexMaterialType.Rivers];
    public HexMesh Roads => meshes[HexMaterialType.Roads];
    public HexMesh Water => meshes[HexMaterialType.Water];
    public HexMesh WaterShore => meshes[HexMaterialType.WaterShore];
    public HexMesh Estuaries => meshes[HexMaterialType.Estuaries];

    public HexMeshCollection()
    {
        materialManager = new HexMaterialManager();
        meshes = new Dictionary<HexMaterialType, HexMesh>
        {
            { HexMaterialType.Terrain, new HexMesh() },
            { HexMaterialType.Rivers, new HexMesh() },
            { HexMaterialType.Roads, new HexMesh() },
            { HexMaterialType.Water, new HexMesh() },
            { HexMaterialType.WaterShore, new HexMesh() },
            { HexMaterialType.Estuaries, new HexMesh() }
        };
    }

    public void InitializeMeshes(Node3D parent)
    {
        foreach (var mesh in meshes.Values)
        {
            parent.AddChild(mesh);
        }
    }

    public void BeginMeshes()
    {
        BeginTerrain();
        BeginRivers();
        BeginRoads();
        BeginWater();
        BeginWaterShore();
        BeginEstuaries();
    }

    public void EndMeshes()
    {
        foreach (var (type, mesh) in meshes)
        {
            mesh.End(materialManager.GetMaterial(type));
        }
    }

    private void BeginTerrain()
    {
        var mesh = meshes[HexMaterialType.Terrain];
        mesh.Begin();
        mesh.UseCellData = true;
    }

    private void BeginRivers()
    {
        var mesh = meshes[HexMaterialType.Rivers];
        mesh.Begin();
        mesh.UseCollider = false;
        mesh.UseUVCoordinates = true;
        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        mesh.UseCellData = true;
    }

    private void BeginRoads()
    {
        var mesh = meshes[HexMaterialType.Roads];
        mesh.Begin();
        mesh.UseCollider = false;
        mesh.UseUVCoordinates = true;
        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        mesh.SortingOffset = HexMeshConstants.RoadSortingOffset;
        mesh.UseCellData = true;
    }

    private void BeginWater()
    {
        var mesh = meshes[HexMaterialType.Water];
        mesh.Begin();
        mesh.UseCollider = false;
        mesh.UseUVCoordinates = true;
        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        mesh.SortingOffset = HexMeshConstants.WaterSortingOffset;
        mesh.UseCellData = true;
    }

    private void BeginWaterShore()
    {
        var mesh = meshes[HexMaterialType.WaterShore];
        mesh.Begin();
        mesh.UseCollider = false;
        mesh.UseUVCoordinates = true;
        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        mesh.SortingOffset = HexMeshConstants.WaterSortingOffset;
        mesh.UseCellData = true;
    }

    private void BeginEstuaries()
    {
        var mesh = meshes[HexMaterialType.Estuaries];
        mesh.Begin();
        mesh.UseCollider = false;
        mesh.UseUVCoordinates = true;
        mesh.UseUV2Coordinates = true;
        mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        mesh.SortingOffset = HexMeshConstants.WaterSortingOffset;
        mesh.UseCellData = true;
    }
}