using Godot;
using System;
using System.Collections.Generic;

public class HexFeatureCollection
{
    private readonly List<BoxMesh> _prefabs = new();
    
    public IReadOnlyList<BoxMesh> Prefabs => _prefabs;

    public BoxMesh Pick(float choice)
    {
        var index = (int)(choice * _prefabs.Count);
        return _prefabs[index];
    }

    public void AddPrefab(BoxMesh prefab)
    {
        _prefabs.Add(prefab);
    }
}