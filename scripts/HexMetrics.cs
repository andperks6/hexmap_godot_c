using Godot;
using System;
using System.Collections.Generic;

public static class HexMetrics
{
    public const int MapFileVersion = 5;
    public const float OuterToInner = 0.866025404f;
    public const float InnerToOuter = 1f / OuterToInner;
    public const float OuterRadius = 10f;
    public const float InnerRadius = OuterRadius * OuterToInner;
    public const float InnerDiameter = InnerRadius * 2f;

    private static readonly Vector3[] corners = {
        new(0f, 0f, OuterRadius),
        new(InnerRadius, 0f, 0.5f * OuterRadius),
        new(InnerRadius, 0f, -0.5f * OuterRadius),
        new(0f, 0f, -OuterRadius),
        new(-InnerRadius, 0f, -0.5f * OuterRadius),
        new(-InnerRadius, 0f, 0.5f * OuterRadius),
        new(0f, 0f, OuterRadius)
    };

    public const float SolidFactor = 0.8f;
    public const float BlendFactor = 1f - SolidFactor;
    public const float ElevationStep = 3f;
    public const int TerracesPerSlope = 2;
    public const int TerraceSteps = TerracesPerSlope * 2 + 1;
    public const float HorizontalTerraceStepSize = 1f / TerraceSteps;
    public const float VerticalTerraceStepSize = 1f / (TerracesPerSlope + 1);
    public const float CellPerturbStrength = 4f;
    public const float NoiseScale = 4f;
    public const float ElevationPerturbStrength = 1.5f;
    public const int ChunkSizeX = 5;
    public const int ChunkSizeZ = 5;
    public const float StreamBedElevationOffset = -1.75f;
    public const float RiverSurfaceElevationOffset = 0.5f * StreamBedElevationOffset;
    public const float WaterElevationOffset = -0.5f;
    public const float WaterFactor = 0.6f;
    public const float WaterBlendFactor = 1f - WaterFactor;
    public const int HashGridSize = 256;
    public const float HashGridScale = 0.25f;
    public const int FeatureThresholdLevels = 3;
    public const int FeatureThresholdSubLevels = 3;
    public const float WallHeight = 4f;
    public const float WallYOffset = -1f;
    public const float WallThickness = 0.75f;
    public const float WallElevationOffset = VerticalTerraceStepSize;
    public const float WallTowerThreshold = 0.5f;
    public const float BridgeDesignLength = 7f;

    private static readonly FastNoiseLite[] noiseGenerators = new FastNoiseLite[4];
    private static readonly HexHash[] hashGrid = new HexHash[HashGridSize * HashGridSize];
    private static readonly float[] featureThresholds = {
        0f, 0f, 0.4f,    // Low
        0f, 0.4f, 0.6f,  // Medium
        0.4f, 0.6f, 0.8f // High
    };

    private static Color[] _colors = Array.Empty<Color>();
    private static DisplayMode _displayMode = DisplayMode.TerrainTextures;
    private static int _wrapSize;

    public static Color[] Colors
    {
        get => _colors;
        set => _colors = value;
    }

    public static bool Wrapping => _wrapSize > 0;

    public static int WrapSize
    {
        get => _wrapSize;
        set => _wrapSize = value;
    }

    public static DisplayMode DisplayMode
    {
        get => _displayMode;
        set => _displayMode = value;
    }

    public static Color GetColor(int index) => _colors[index];

    public static void InitializeNoiseGenerator()
    {
        for (int i = 0; i < noiseGenerators.Length; i++)
        {
            var noise = new FastNoiseLite();
            noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
            noise.Seed = i;
            noise.Frequency = 0.025f;
            noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
            noise.FractalOctaves = 2;
            noise.FractalLacunarity = 2f;
            noise.FractalGain = 0.5f;
            noise.FractalWeightedStrength = 0f;
            noiseGenerators[i] = noise;
        }
    }

    public static Vector4 SampleNoise(Vector3 position)
    {
        var sample = Vector4.Zero;
        for (int i = 0; i < noiseGenerators.Length; i++)
        {
            sample[i] = noiseGenerators[i].GetNoise2D(
                position.X * NoiseScale,
                position.Z * NoiseScale
            );
        }

        if (Wrapping && position.X < InnerDiameter * 1.5f)
        {
            var sample2 = Vector4.Zero;
            for (int i = 0; i < noiseGenerators.Length; i++)
            {
                sample2[i] = noiseGenerators[i].GetNoise2D(
                    (position.X + _wrapSize * InnerDiameter) * NoiseScale,
                    position.Z * NoiseScale
                );
            }
            sample = sample2.Lerp(sample, position.X * (1f / InnerDiameter) - 0.5f);
        }

        return sample;
    }

    public static void InitializeHashGrid()
    {
        for (int i = 0; i < hashGrid.Length; i++)
        {
            hashGrid[i] = HexHash.Create();
        }
    }

    public static HexHash SampleHashGrid(Vector3 position)
    {
        int x = (int)((position.X * 10f) * HashGridScale) % HashGridSize;
        if (x < 0) x += HashGridSize;

        int z = (int)((position.Z * 10f) * HashGridScale) % HashGridSize;
        if (z < 0) z += HashGridSize;

        return hashGrid[x + z * HashGridSize];
    }

    public static float[] GetFeatureThresholds(int level)
    {
        int idx = level * FeatureThresholdSubLevels;
        var result = new float[FeatureThresholdSubLevels];

        for (int i = 0; i < FeatureThresholdSubLevels; i++)
        {
            if (idx + i < featureThresholds.Length)
                result[i] = featureThresholds[idx + i];
        }

        return result;
    }

    public static Vector3 GetFirstCorner(HexDirection direction) => corners[(int)direction];
    public static Vector3 GetSecondCorner(HexDirection direction) => corners[(int)direction + 1];
    public static Vector3 GetFirstSolidCorner(HexDirection direction) => corners[(int)direction] * SolidFactor;
    public static Vector3 GetSecondSolidCorner(HexDirection direction) => corners[(int)direction + 1] * SolidFactor;
    public static Vector3 GetFirstWaterCorner(HexDirection direction) => corners[(int)direction] * WaterFactor;
    public static Vector3 GetSecondWaterCorner(HexDirection direction) => corners[(int)direction + 1] * WaterFactor;
    public static Vector3 GetBridge(HexDirection direction) => (corners[(int)direction] + corners[(int)direction + 1]) * BlendFactor;
    public static Vector3 GetWaterBridge(HexDirection direction) => (corners[(int)direction] + corners[(int)direction + 1]) * WaterBlendFactor;

    public static Vector3 TerraceLerp(Vector3 a, Vector3 b, int step)
    {
        float h = step * HorizontalTerraceStepSize;
        a.X += (b.X - a.X) * h;
        a.Z += (b.Z - a.Z) * h;

        float v = ((step + 1) / 2) * VerticalTerraceStepSize;
        a.Y += (b.Y - a.Y) * v;

        return a;
    }

    public static Color TerraceColorLerp(Color a, Color b, int step)
    {
        float h = step * HorizontalTerraceStepSize;
        return a.Lerp(b, h);
    }

    public static HexEdgeType GetEdgeType(int elevation1, int elevation2)
    {
        if (elevation1 == elevation2) return HexEdgeType.Flat;
        int delta = elevation2 - elevation1;
        return (delta == 1 || delta == -1) ? HexEdgeType.Slope : HexEdgeType.Cliff;
    }

    public static Vector3 GetSolidEdgeMiddle(HexDirection direction) =>
        (corners[(int)direction] + corners[(int)direction + 1]) * (0.5f * SolidFactor);

    public static Vector3 Perturb(Vector3 position)
    {
        Vector4 sample = SampleNoise(position);
        position.X += (sample.X * 2f - 1f) * CellPerturbStrength;
        position.Z += (sample.Z * 2f - 1f) * CellPerturbStrength;
        return position;
    }

    public static Vector3 WallThicknessOffset(Vector3 near, Vector3 far)
    {
        Vector3 offset = new(
            far.X - near.X,
            0f,
            far.Z - near.Z
        );
        return offset.Normalized() * (WallThickness * 0.5f);
    }

    public static Vector3 WallLerp(Vector3 near, Vector3 far)
    {
        near.X += (far.X - near.X) * 0.5f;
        near.Z += (far.Z - near.Z) * 0.5f;

        float v = WallElevationOffset;
        if (near.Y < far.Y)
            v = 1f - WallElevationOffset;

        near.Y += (far.Y - near.Y) * v + WallYOffset;
        return near;
    }
}