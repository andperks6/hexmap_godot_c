using Godot;
using System;

public partial class TextureArrayBuilder : Node
{
    [Export] public Texture2D[] SourceTextures { get; set; }
    [Export] public string OutputPath { get; set; } = "res://assets/terrain.res";
    
    public override void _Ready()
    {
        if (SourceTextures == null || SourceTextures.Length == 0)
        {
            GD.PrintErr("No source textures provided!");
            return;
        }

        try
        {
            // Get dimensions from first texture
            var firstImage = SourceTextures[0].GetImage();
            var width = firstImage.GetWidth();
            var height = firstImage.GetHeight();
            
            // Create array of images
            var images = new Godot.Collections.Array<Image>();
            for (int i = 0; i < SourceTextures.Length; i++)
            {
                var image = SourceTextures[i].GetImage();
                
                // Ensure consistent dimensions
                if (image.GetWidth() != width || image.GetHeight() != height)
                {
                    image.Resize(width, height, Image.Interpolation.Lanczos);
                }
                
                images.Add(image);
            }

            // Create the texture array resource
            var textureArray = new Texture2DArray();
            Error err = textureArray.CreateFromImages(images);
            if (err != Error.Ok)
            {
                GD.PrintErr($"Failed to create texture array: {err}");
                return;
            }
            
            // Save the texture array
            err = ResourceSaver.Save(textureArray, OutputPath);
            if (err != Error.Ok)
            {
                GD.PrintErr($"Failed to save texture array: {err}");
            }
            else
            {
                GD.Print($"Successfully created texture array with {images.Count} layers at {OutputPath}");
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error creating texture array: {e.Message}");
            GD.PrintErr($"Stack trace: {e.StackTrace}");
        }
    }
}