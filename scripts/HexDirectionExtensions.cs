using Godot;
using System;

public enum HexDirection
{
    NE,
    E,
    SE,
    SW,
    W,
    NW
}

public static class HexDirectionExtensions
{
    public static HexDirection Opposite(this HexDirection direction)
    {
        return direction < HexDirection.SW ? 
            direction + 3 : 
            direction - 3;
    }

    public static HexDirection Previous(this HexDirection direction)
    {
        return direction == HexDirection.NE ? 
            HexDirection.NW : 
            direction - 1;
    }

    public static HexDirection Next(this HexDirection direction)
    {
        return direction == HexDirection.NW ? 
            HexDirection.NE : 
            direction + 1;
    }

    public static HexDirection Previous2(this HexDirection direction)
    {
        var value = (int)direction - 2;
        return value >= 0 ? 
            (HexDirection)value : 
            (HexDirection)(value + 6);
    }

    public static HexDirection Next2(this HexDirection direction)
    {
        var value = (int)direction + 2;
        return value <= (int)HexDirection.NW ? 
            (HexDirection)value : 
            (HexDirection)(value - 6);
    }
}