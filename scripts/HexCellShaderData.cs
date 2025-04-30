using Godot;
using System;

public class HexCellShaderData
{
    private Image _cellTextureSourceImage;
    private ImageTexture _cellTexture;
    private Color[] _cellTextureData = Array.Empty<Color>();
    private bool _requiresUpdate;
    private bool _requiresVisibilityReset;

    public HexGrid HexGrid { get; set; }

    public void Initialize(int x, int z)
    {
        var texelSize = new Vector4(
            1.0f / x,
            1.0f / z,
            x,
            z
        );

        // Resize array if needed
        if (_cellTextureData.Length != x * z)
        {
            _cellTextureData = new Color[x * z];
        }

        // Initialize each color to all zeros
        // Each color represents a cell in the hex grid
        // Alpha channel stores the terrain index
        Array.Fill(_cellTextureData, Colors.Transparent);

        // Create empty source image
        _cellTextureSourceImage = Image.CreateEmpty(x, z, false, Image.Format.Rgbaf);

        // Create texture from source image
        _cellTexture = ImageTexture.CreateFromImage(_cellTextureSourceImage);

        // Set global shader parameters
        RenderingServer.GlobalShaderParameterSet("_HexCellData", _cellTexture);
        RenderingServer.GlobalShaderParameterSet("_HexCellData_TexelSize", texelSize);

        _requiresUpdate = true;
    }

    public void RefreshTerrain(HexCell cell)
    {
        var color = _cellTextureData[cell.Index];
        color.A = cell.TerrainTypeIndex;
        _cellTextureData[cell.Index] = color;
        _requiresUpdate = true;
    }

    public void RefreshVisibility(HexCell cell)
    {
        var index = cell.Index;
        var color = _cellTextureData[index];
        
        color.R = cell.IsVisibleInGame ? 1.0f : 0.0f;
        color.G = cell.IsExplored ? 1.0f : 0.0f;
        
        _cellTextureData[index] = color;
        _requiresUpdate = true;
    }

    public void LateUpdate()
    {
        if (_requiresVisibilityReset && HexGrid != null)
        {
            _requiresVisibilityReset = false;
            HexGrid.ResetVisibility();
        }

        if (_requiresUpdate)
        {
            _requiresUpdate = false;

            var x = _cellTextureSourceImage.GetWidth();
            var z = _cellTextureSourceImage.GetHeight();

            // Convert Color[] to byte array for image data
            var byteArray = new byte[_cellTextureData.Length * 16]; // 16 bytes per Color (RGBA float)
            for (int i = 0; i < _cellTextureData.Length; i++)
            {
                var color = _cellTextureData[i];
                var offset = i * 16;
                
                // Convert each float component to bytes
                BitConverter.GetBytes(color.R).CopyTo(byteArray, offset);
                BitConverter.GetBytes(color.G).CopyTo(byteArray, offset + 4);
                BitConverter.GetBytes(color.B).CopyTo(byteArray, offset + 8);
                BitConverter.GetBytes(color.A).CopyTo(byteArray, offset + 12);
            }

            _cellTextureSourceImage.SetData(x, z, false, Image.Format.Rgbaf, byteArray);
            _cellTexture.Update(_cellTextureSourceImage);
        }
    }

    public void ViewElevationChanged()
    {
        _requiresVisibilityReset = true;
        _requiresUpdate = true;
    }

    public void SetMapData(HexCell cell, float data)
    {
        var value = Mathf.Clamp(data, 0.0f, 1.0f);
        
        var color = _cellTextureData[cell.Index];
        color.B = value;
        _cellTextureData[cell.Index] = color;
        
        _requiresUpdate = true;
    }
}