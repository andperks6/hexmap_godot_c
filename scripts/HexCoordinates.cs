using Godot;
using System;

public readonly struct HexCoordinates : IEquatable<HexCoordinates>
{
    public readonly int X { get; }
    public readonly int Z { get; }
    public int Y => -X - Z;

    public HexCoordinates(int x, int z)
    {
        if (HexMetrics.Wrapping)
        {
            var offsetX = x + z / 2;
            if (offsetX < 0)
                x += HexMetrics.WrapSize;
            else if (offsetX >= HexMetrics.WrapSize)
                x -= HexMetrics.WrapSize;
        }

        X = x;
        Z = z;
    }

    public static HexCoordinates FromOffsetCoordinates(int x, int z)
    {
        return new HexCoordinates(x - z / 2, z);
    }

    /// <summary>
    /// Converts a world position to hex coordinates using cube coordinate system.
    /// </summary>
    public static HexCoordinates FromPosition(Vector3 position)
    {
        float x = position.X / HexMetrics.InnerDiameter;
        float y = -x;

        float offset = position.Z / (HexMetrics.OuterRadius * 3f);
        x -= offset;
        y -= offset;

        int iX = Mathf.RoundToInt(x);
        int iY = Mathf.RoundToInt(y);
        int iZ = Mathf.RoundToInt(-x - y);

        if (iX + iY + iZ != 0)
        {
            float dX = Mathf.Abs(x - iX);
            float dY = Mathf.Abs(y - iY);
            float dZ = Mathf.Abs(-x - y - iZ);

            if (dX > dY && dX > dZ)
                iX = -iY - iZ;
            else if (dZ > dY)
                iZ = -iX - iY;
        }

        return new HexCoordinates(iX, iZ);
    }

    public int DistanceTo(HexCoordinates other)
    {
        int xy = Mathf.Abs(X - other.X) + Mathf.Abs(Y - other.Y);

        if (HexMetrics.Wrapping)
        {
            other = new HexCoordinates(other.X + HexMetrics.WrapSize, other.Z);
            int xyWrapped = Mathf.Abs(X - other.X) + Mathf.Abs(Y - other.Y);

            if (xyWrapped < xy)
                xy = xyWrapped;
            else
            {
                other = new HexCoordinates(other.X - 2 * HexMetrics.WrapSize, other.Z);
                xyWrapped = Mathf.Abs(X - other.X) + Mathf.Abs(Y - other.Y);
                if (xyWrapped < xy)
                    xy = xyWrapped;
            }
        }

        return (xy + Mathf.Abs(Z - other.Z)) / 2;
    }

    public void SaveToFile(FileAccess writer)
    {
        writer.Store64((ulong)X);
        writer.Store64((ulong)Z);
    }

    public static HexCoordinates LoadFromFile(FileAccess reader)
    {
        ulong x = reader.Get64();
        ulong z = reader.Get64();
        return new HexCoordinates((int)x, (int)z);
    }

    public override string ToString() => $"({X}, {Y}, {Z})";

    public string ToStringOnSeparateLines() => $"{X}\n{Y}\n{Z}";

    public bool Equals(HexCoordinates other) => X == other.X && Z == other.Z;

    public override bool Equals(object obj) => obj is HexCoordinates other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Z);

    public static bool operator ==(HexCoordinates a, HexCoordinates b) => a.Equals(b);

    public static bool operator !=(HexCoordinates a, HexCoordinates b) => !a.Equals(b);
}