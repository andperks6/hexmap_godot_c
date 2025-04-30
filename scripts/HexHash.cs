using Godot;

public readonly struct HexHash
{
    public readonly float A;
    public readonly float B;
    public readonly float C;
    public readonly float D;
    public readonly float E;

    private HexHash(float a, float b, float c, float d, float e)
    {
        A = a;
        B = b;
        C = c;
        D = d;
        E = e;
    }

    public static HexHash Create()
    {
        return new HexHash(
            GD.Randf() * 0.999f,
            GD.Randf() * 0.999f,
            GD.Randf() * 0.999f,
            GD.Randf() * 0.999f,
            GD.Randf() * 0.999f
        );
    }
}