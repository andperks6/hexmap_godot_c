using Godot;
using System;

public static class GodotConstants
{
    /// Maximum value for a signed 64-bit integer (for Godot compatibility)
    public const long MaxLong = long.MaxValue;

    /// Minimum value for a signed 64-bit integer (for Godot compatibility)
    public const long MinLong = long.MinValue;

    /// Maximum value for array indexing and practical priority queue usage
    public const int MaxInt = int.MaxValue;

    /// Minimum value for array indexing and practical priority queue usage
    public const int MinInt = int.MinValue;
}