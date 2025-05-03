using Godot;
using System.Collections.Generic;

public enum HexMaterialType
{
    Terrain,
    Rivers,
    Roads,
    Water,
    WaterShore,
    Estuaries,
    Walls
}

public class HexMaterialManager
{
    private readonly Dictionary<HexMaterialType, ShaderMaterial> materials = new();

    public void SetMaterial(HexMaterialType type, ShaderMaterial material)
    {
        if (material == null)
        {
            GD.PrintErr($"Attempted to set null material for type {type}");
            return;
        }
        
        materials[type] = material;
        material.RenderPriority = type == HexMaterialType.Terrain
            ? HexMeshConstants.TerrainRenderPriority
            : HexMeshConstants.OverlayRenderPriority;
        
        GD.Print($"Set material for type {type}"); // Debug logging
    }

    public ShaderMaterial GetMaterial(HexMaterialType type)
    {
        if (materials.TryGetValue(type, out ShaderMaterial material))
        {
            if (material == null)
            {
                GD.PrintErr($"Material of type {type} is null");
                return null;
            }
            return material;
        }
        GD.PrintErr($"Material of type {type} has not been set");
        return null;
    }

    public bool ValidateMaterials()
    {
        bool allValid = true;
        foreach (HexMaterialType type in System.Enum.GetValues(typeof(HexMaterialType)))
        {
            if (!materials.ContainsKey(type))
            {
                GD.PrintErr($"Missing material: {type}");
                allValid = false;
            }
            else if (materials[type] == null)
            {
                GD.PrintErr($"Material is null: {type}");
                allValid = false;
            }
        }
        return allValid;
    }
}