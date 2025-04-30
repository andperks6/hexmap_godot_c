using Godot;

public class HexMeshCollection
{
    public HexMesh Terrain { get; }
    public HexMesh Rivers { get; }
    public HexMesh Roads { get; }
    public HexMesh Water { get; }
    public HexMesh WaterShore { get; }
    public HexMesh Estuaries { get; }

    private ShaderMaterial terrainMaterial;
    private ShaderMaterial riversMaterial;
    private ShaderMaterial roadMaterial;
    private ShaderMaterial waterMaterial;
    private ShaderMaterial waterShoreMaterial;
    private ShaderMaterial estuariesMaterial;

    public HexMeshCollection()
    {
        Terrain = new HexMesh();
        Rivers = new HexMesh();
        Roads = new HexMesh();
        Water = new HexMesh();
        WaterShore = new HexMesh();
        Estuaries = new HexMesh();
    }

    public void InitializeMeshes(Node3D parent)
    {
        parent.AddChild(Terrain);
        parent.AddChild(Rivers);
        parent.AddChild(Roads);
        parent.AddChild(Water);
        parent.AddChild(WaterShore);
        parent.AddChild(Estuaries);
    }

    public void SetTerrainMaterial(ShaderMaterial mat)
    {
        terrainMaterial = mat;
        terrainMaterial.RenderPriority = 0;
    }

    public void SetRiversMaterial(ShaderMaterial mat)
    {
        riversMaterial = mat;
        riversMaterial.RenderPriority = 1;
    }

    public void SetRoadMaterial(ShaderMaterial mat)
    {
        roadMaterial = mat;
        roadMaterial.RenderPriority = 1;
    }

    public void SetWaterMaterial(ShaderMaterial mat)
    {
        waterMaterial = mat;
        waterMaterial.RenderPriority = 1;
    }

    public void SetWaterShoreMaterial(ShaderMaterial mat)
    {
        waterShoreMaterial = mat;
        waterShoreMaterial.RenderPriority = 1;
    }

    public void SetEstuariesMaterial(ShaderMaterial mat)
    {
        estuariesMaterial = mat;
        estuariesMaterial.RenderPriority = 1;
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
        Terrain.End(terrainMaterial);
        Rivers.End(riversMaterial);
        Roads.End(roadMaterial);
        Water.End(waterMaterial);
        WaterShore.End(waterShoreMaterial);
        Estuaries.End(estuariesMaterial);
    }

    private void BeginTerrain()
    {
        Terrain.Begin();
        Terrain.UseCellData = true;
    }

    private void BeginRivers()
    {
        Rivers.Begin();
        Rivers.UseCollider = false;
        Rivers.UseUVCoordinates = true;
        Rivers.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Rivers.UseCellData = true;
    }

    private void BeginRoads()
    {
        Roads.Begin();
        Roads.UseCollider = false;
        Roads.UseUVCoordinates = true;
        Roads.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Roads.SortingOffset = 10f;
        Roads.UseCellData = true;
    }

    private void BeginWater()
    {
        Water.Begin();
        Water.UseCollider = false;
        Water.UseUVCoordinates = true;
        Water.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Water.SortingOffset = 10f;
        Water.UseCellData = true;
    }

    private void BeginWaterShore()
    {
        WaterShore.Begin();
        WaterShore.UseCollider = false;
        WaterShore.UseUVCoordinates = true;
        WaterShore.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        WaterShore.SortingOffset = 10f;
        WaterShore.UseCellData = true;
    }

    private void BeginEstuaries()
    {
        Estuaries.Begin();
        Estuaries.UseCollider = false;
        Estuaries.UseUVCoordinates = true;
        Estuaries.UseUV2Coordinates = true;
        Estuaries.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Estuaries.SortingOffset = 10f;
        Estuaries.UseCellData = true;
    }
}