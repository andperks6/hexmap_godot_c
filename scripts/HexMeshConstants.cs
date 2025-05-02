using Godot;

public static class HexMeshConstants
{
    // Splat weights used for terrain texturing
    public static readonly Color SplatWeights1 = new(1f, 0f, 0f);
    public static readonly Color SplatWeights2 = new(0f, 1f, 0f);
    public static readonly Color SplatWeights3 = new(0f, 0f, 1f);

    // Mesh rendering priorities
    public const int TerrainRenderPriority = 0;
    public const int OverlayRenderPriority = 1;

    // Feature offsets
    public const float WallSortingOffset = 10f;
    public const float RoadSortingOffset = 10f;
    public const float WaterSortingOffset = 10f;

    // Height offsets
    public const float RoadHeightOffset = 0.1f;
    public const float RoadSegmentHeightOffset = 0.01f;

    // UV coordinates
    public static readonly Vector2 RoadUV_00 = new(0f, 0f);
    public static readonly Vector2 RoadUV_10 = new(1f, 0f);
    public static readonly Vector2 RoadUV_01 = new(0f, 1f);
    public static readonly Vector2 RoadUV_11 = new(1f, 1f);

    // River UV coordinates
    public static readonly Vector2 RiverUV_Center = new(0.5f, 0.4f);
    public static readonly Vector2 RiverUV_Left = new(0f, 0.6f);
    public static readonly Vector2 RiverUV_Right = new(1f, 0.6f);
    public static readonly Vector2 RiverUV_ReverseLeft = new(1f, 0.2f);
    public static readonly Vector2 RiverUV_ReverseRight = new(0f, 0.2f);

    // Feature placement
    public const float FeatureThirdOffset = 1f / 3f;
    public const float RiverSurfaceElevationOffset = 0.6f;
}