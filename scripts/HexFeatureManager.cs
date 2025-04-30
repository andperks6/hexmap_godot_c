using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class HexFeatureManager : Node3D
{
    private readonly List<HexFeatureCollection> urbanCollections = [];
    private readonly List<HexFeatureCollection> farmCollections = [];
    private readonly List<HexFeatureCollection> plantCollections = [];
    public HexMesh Walls;

    private PackedScene wallTowerPrefab;
    private PackedScene bridgePrefab;
    private PackedScene flagPrefab;
    private readonly List<PackedScene> specialPrefabs = [];

    public override void _Ready()
    {
        // Add the walls mesh as a child
        Walls = new HexMesh { UseCollider = true };
        AddChild(Walls);

        // Load prefabs
        wallTowerPrefab = GD.Load<PackedScene>("res://scenes/prefabs/wall_tower.tscn");
        bridgePrefab = GD.Load<PackedScene>("res://scenes/prefabs/bridge.tscn");
        flagPrefab = GD.Load<PackedScene>("res://scenes/prefabs/flag.tscn");

        specialPrefabs.Add(GD.Load<PackedScene>("res://scenes/prefabs/castle.tscn"));
        specialPrefabs.Add(GD.Load<PackedScene>("res://scenes/prefabs/ziggurat.tscn"));
        specialPrefabs.Add(GD.Load<PackedScene>("res://scenes/prefabs/megaflora.tscn"));

        InitializeCollections();
    }

    private void InitializeCollections()
    {
        // Urban Collections
        var smallUrban = new HexFeatureCollection();
        smallUrban.AddPrefab(GD.Load<BoxMesh>("res://resources/urban_features/small/feature_urban_small_01.tres"));
        smallUrban.AddPrefab(GD.Load<BoxMesh>("res://resources/urban_features/small/feature_urban_small_02.tres"));

        var mediumUrban = new HexFeatureCollection();
        mediumUrban.AddPrefab(GD.Load<BoxMesh>("res://resources/urban_features/medium/feature_urban_medium_01.tres"));
        mediumUrban.AddPrefab(GD.Load<BoxMesh>("res://resources/urban_features/medium/feature_urban_medium_02.tres"));

        var largeUrban = new HexFeatureCollection();
        largeUrban.AddPrefab(GD.Load<BoxMesh>("res://resources/urban_features/large/feature_urban_large_01.tres"));
        largeUrban.AddPrefab(GD.Load<BoxMesh>("res://resources/urban_features/large/feature_urban_large_02.tres"));

        urbanCollections.Add(largeUrban);
        urbanCollections.Add(mediumUrban);
        urbanCollections.Add(smallUrban);

        // Farm Collections
        var smallFarm = new HexFeatureCollection();
        smallFarm.AddPrefab(GD.Load<BoxMesh>("res://resources/farm_features/small/feature_farm_small_01.tres"));
        smallFarm.AddPrefab(GD.Load<BoxMesh>("res://resources/farm_features/small/feature_farm_small_02.tres"));

        var mediumFarm = new HexFeatureCollection();
        mediumFarm.AddPrefab(GD.Load<BoxMesh>("res://resources/farm_features/medium/feature_farm_medium_01.tres"));
        mediumFarm.AddPrefab(GD.Load<BoxMesh>("res://resources/farm_features/medium/feature_farm_medium_02.tres"));

        var largeFarm = new HexFeatureCollection();
        largeFarm.AddPrefab(GD.Load<BoxMesh>("res://resources/farm_features/large/feature_farm_large_01.tres"));
        largeFarm.AddPrefab(GD.Load<BoxMesh>("res://resources/farm_features/large/feature_farm_large_02.tres"));

        farmCollections.Add(largeFarm);
        farmCollections.Add(mediumFarm);
        farmCollections.Add(smallFarm);

        // Plant Collections
        var smallPlant = new HexFeatureCollection();
        smallPlant.AddPrefab(GD.Load<BoxMesh>("res://resources/plant_features/small/feature_plant_small_01.tres"));
        smallPlant.AddPrefab(GD.Load<BoxMesh>("res://resources/plant_features/small/feature_plant_small_02.tres"));

        var mediumPlant = new HexFeatureCollection();
        mediumPlant.AddPrefab(GD.Load<BoxMesh>("res://resources/plant_features/medium/feature_plant_medium_01.tres"));
        mediumPlant.AddPrefab(GD.Load<BoxMesh>("res://resources/plant_features/medium/feature_plant_medium_02.tres"));

        var largePlant = new HexFeatureCollection();
        largePlant.AddPrefab(GD.Load<BoxMesh>("res://resources/plant_features/large/feature_plant_large_01.tres"));
        largePlant.AddPrefab(GD.Load<BoxMesh>("res://resources/plant_features/large/feature_plant_large_02.tres"));

        plantCollections.Add(largePlant);
        plantCollections.Add(mediumPlant);
        plantCollections.Add(smallPlant);
    }

    public void Clear()
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        Walls = new HexMesh { UseCollider = true };
        AddChild(Walls);
    }

    public void Apply() { }

    public void AddFeature(HexCell cell, Vector3 position)
    {
        if (cell.IsSpecial) return;

        var hexHash = HexMetrics.SampleHashGrid(position);

        var urbanPrefab = PickPrefab(urbanCollections, cell.UrbanLevel, hexHash.A, hexHash.D);
        var farmPrefab = PickPrefab(farmCollections, cell.FarmLevel, hexHash.B, hexHash.D);
        var plantPrefab = PickPrefab(plantCollections, cell.PlantLevel, hexHash.C, hexHash.D);

        BoxMesh prefab = urbanPrefab;
        float usedHash = hexHash.A;

        if (urbanPrefab != null)
        {
            if (farmPrefab != null && hexHash.B < hexHash.A)
            {
                prefab = farmPrefab;
                usedHash = hexHash.B;
            }
        }
        else if (farmPrefab != null)
        {
            prefab = farmPrefab;
            usedHash = hexHash.B;
        }

        if (prefab != null)
        {
            if (plantPrefab != null && hexHash.C < usedHash)
                prefab = plantPrefab;
        }
        else if (plantPrefab != null)
            prefab = plantPrefab;
        else
            return;

        var feature = new MeshInstance3D
        {
            Mesh = prefab,
            Position = HexMetrics.Perturb(position)
        };
        feature.SetInstanceShaderParameter("_index", cell.Index);

        var featureHeight = feature.Mesh.GetAabb().Size.Y;
        feature.Position = feature.Position with { Y = feature.Position.Y + featureHeight / 2f };

        feature.Quaternion = Quaternion.FromEuler(new Vector3(0, 360f * hexHash.E, 0));

        AddChild(feature);
    }

    private BoxMesh PickPrefab(List<HexFeatureCollection> collection, int level, float hexHash, float choice)
    {
        if (level <= 0) return null;

        var thresholds = HexMetrics.GetFeatureThresholds(level - 1);
        for (int i = 0; i < thresholds.Length; i++)
        {
            if (hexHash < thresholds[i])
                return collection[i].Pick(choice);
        }

        return null;
    }

    public void AddWall(EdgeVertices near, HexCell nearCell, EdgeVertices far, HexCell farCell, bool hasRiver, bool hasRoad)
    {
        if (nearCell.Walled != farCell.Walled && !nearCell.IsUnderwater && !farCell.IsUnderwater &&
            nearCell.GetEdgeType(farCell) != HexEdgeType.Cliff)
        {
            AddWallSegment(nearCell, near.V1, far.V1, near.V2, far.V2);

            if (hasRiver || hasRoad)
            {
                AddWallCap(nearCell, near.V2, far.V2);
                AddWallCap(nearCell, far.V4, near.V4);
            }
            else
            {
                AddWallSegment(nearCell, near.V2, far.V2, near.V3, far.V3);
                AddWallSegment(nearCell, near.V3, far.V3, near.V4, far.V4);
            }

            AddWallSegment(nearCell, near.V4, far.V4, near.V5, far.V5);
        }
    }

    public void AddWallThreeCells(Vector3 c1, HexCell cell1, Vector3 c2, HexCell cell2, Vector3 c3, HexCell cell3)
    {
        if (cell1.Walled)
        {
            if (cell2.Walled)
            {
                if (!cell3.Walled)
                    AddWallSegmentWithPivot(c3, cell3, c1, cell1, c2, cell2);
            }
            else if (cell3.Walled)
                AddWallSegmentWithPivot(c2, cell2, c3, cell3, c1, cell1);
            else
                AddWallSegmentWithPivot(c1, cell1, c2, cell2, c3, cell3);
        }
        else if (cell2.Walled)
        {
            if (cell3.Walled)
                AddWallSegmentWithPivot(c1, cell1, c2, cell2, c3, cell3);
            else
                AddWallSegmentWithPivot(c2, cell2, c3, cell3, c1, cell1);
        }
        else if (cell3.Walled)
            AddWallSegmentWithPivot(c3, cell3, c1, cell1, c2, cell2);
    }

    private void AddWallSegmentWithPivot(Vector3 pivot, HexCell pivotCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
    {
        if (pivotCell.IsUnderwater) return;

        var hasLeftWall = !leftCell.IsUnderwater && pivotCell.GetEdgeType(leftCell) != HexEdgeType.Cliff;
        var hasRightWall = !rightCell.IsUnderwater && pivotCell.GetEdgeType(rightCell) != HexEdgeType.Cliff;

        if (hasLeftWall)
        {
            if (hasRightWall)
            {
                var hasTower = false;
                if (leftCell.Elevation == rightCell.Elevation)
                {
                    var hash = HexMetrics.SampleHashGrid((pivot + left + right) * (1f / 3f));
                    hasTower = hash.E < HexMetrics.WallTowerThreshold;
                }

                AddWallSegment(pivotCell, pivot, left, pivot, right, hasTower);
            }
            else if (leftCell.Elevation < rightCell.Elevation)
                AddWallWedge(pivotCell, pivot, left, right);
            else
                AddWallCap(pivotCell, pivot, left);
        }
        else if (hasRightWall)
        {
            if (rightCell.Elevation < leftCell.Elevation)
                AddWallWedge(pivotCell, right, pivot, left);
            else
                AddWallCap(pivotCell, right, pivot);
        }
    }

    private void AddWallSegment(HexCell cell, Vector3 nearLeft, Vector3 farLeft, Vector3 nearRight, Vector3 farRight, bool addTower = false)
    {
        nearLeft = HexMetrics.Perturb(nearLeft);
        farLeft = HexMetrics.Perturb(farLeft);
        nearRight = HexMetrics.Perturb(nearRight);
        farRight = HexMetrics.Perturb(farRight);

        var left = HexMetrics.WallLerp(nearLeft, farLeft);
        var right = HexMetrics.WallLerp(nearRight, farRight);

        var leftThicknessOffset = HexMetrics.WallThicknessOffset(nearLeft, farLeft);
        var rightThicknessOffset = HexMetrics.WallThicknessOffset(nearRight, farRight);

        var leftTop = left.Y + HexMetrics.WallHeight;
        var rightTop = right.Y + HexMetrics.WallHeight;

        var v1 = left - leftThicknessOffset;
        var v2 = right - rightThicknessOffset;
        var v3 = left - leftThicknessOffset with { Y = leftTop };
        var v4 = right - rightThicknessOffset with { Y = rightTop };

        var indices = new Vector3(cell.Index, cell.Index, cell.Index);

        var w1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        w1.AddQuadUnperturbedVertices(v1, v2, v3, v4);
        w1.AddQuadCellDataUnified(indices, Colors.Black);
        Walls.CommitPrimitive(w1);

        var t1 = v3;
        var t2 = v4;

        v1 = left + leftThicknessOffset;
        v2 = right + rightThicknessOffset;
        v3 = v1 with { Y = leftTop };
        v4 = v2 with { Y = rightTop };

        var w2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        w2.AddQuadUnperturbedVertices(v2, v1, v4, v3);
        w2.AddQuadCellDataUnified(indices, Colors.Black);
        Walls.CommitPrimitive(w2);

        var w3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        w3.AddQuadUnperturbedVertices(t1, t2, v3, v4);
        w3.AddQuadCellDataUnified(indices, Colors.Black);
        Walls.CommitPrimitive(w3);

        if (addTower)
        {
            var tower = wallTowerPrefab.Instantiate<Node3D>();
            tower.Position = (left + right) * 0.5f;

            foreach (var child in tower.GetChildren())
            {
                if (child is GeometryInstance3D geom)
                    geom.SetInstanceShaderParameter("_index", cell.Index);
            }

            var rightDirection = right - left with { Y = 0 };
            tower.Quaternion = new Quaternion(tower.Basis.X, rightDirection);

            AddChild(tower);
        }
    }

    private void AddWallCap(HexCell cell, Vector3 near, Vector3 far)
    {
        near = HexMetrics.Perturb(near);
        far = HexMetrics.Perturb(far);

        var center = HexMetrics.WallLerp(near, far);
        var thickness = HexMetrics.WallThicknessOffset(near, far);

        var v1 = center - thickness;
        var v2 = center + thickness;
        var v3 = v1 with { Y = center.Y + HexMetrics.WallHeight };
        var v4 = v2 with { Y = v3.Y };

        var indices = new Vector3(cell.Index, cell.Index, cell.Index);

        var w1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        w1.AddQuadUnperturbedVertices(v1, v2, v3, v4);
        w1.AddQuadCellDataUnified(indices, Colors.Black);
        Walls.CommitPrimitive(w1);
    }

    private void AddWallWedge(HexCell cell, Vector3 near, Vector3 far, Vector3 point)
    {
        near = HexMetrics.Perturb(near);
        far = HexMetrics.Perturb(far);
        point = HexMetrics.Perturb(point);

        var center = HexMetrics.WallLerp(near, far);
        var thickness = HexMetrics.WallThicknessOffset(near, far);

        point = point with { Y = center.Y };
        var pointTop = point with { Y = center.Y + HexMetrics.WallHeight };

        var v1 = center - thickness;
        var v2 = center + thickness;
        var v3 = v1 with { Y = center.Y + HexMetrics.WallHeight };
        var v4 = v2 with { Y = v3.Y };

        var indices = new Vector3(cell.Index, cell.Index, cell.Index);

        var w1 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        w1.AddQuadUnperturbedVertices(v1, point, v3, pointTop);
        w1.AddQuadCellDataUnified(indices, Colors.Black);
        Walls.CommitPrimitive(w1);

        var w2 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Quad);
        w2.AddQuadUnperturbedVertices(point, v2, pointTop, v4);
        w2.AddQuadCellDataUnified(indices, Colors.Black);
        Walls.CommitPrimitive(w2);

        var w3 = new HexMeshPrimitive(HexMeshPrimitive.PrimitiveType.Triangle);
        w3.AddTriangleUnperturbedVertices(pointTop, v3, v4);
        w3.AddTriangleCellDataUniform(indices, Colors.Black);
        Walls.CommitPrimitive(w3);
    }

    public void AddBridge(HexCell cell, Vector3 roadCenter1, Vector3 roadCenter2)
    {
        roadCenter1 = HexMetrics.Perturb(roadCenter1);
        roadCenter2 = HexMetrics.Perturb(roadCenter2);

        if (roadCenter2.Z < roadCenter1.Z)
            (roadCenter1, roadCenter2) = (roadCenter2, roadCenter1);

        var bridge = bridgePrefab.Instantiate<Node3D>();
        bridge.Position = (roadCenter1 + roadCenter2) * 0.5f;
        bridge.Quaternion = new Quaternion(bridge.GlobalTransform.Basis.Z, roadCenter2 - roadCenter1);

        foreach (Node child in bridge.GetChildren())
        {
            foreach (Node grandChild in child.GetChildren())
            {
                if (grandChild is GeometryInstance3D geom)
                    geom.SetInstanceShaderParameter("_index", cell.Index);
            }
        }

        var bridgeLength = roadCenter1.DistanceTo(roadCenter2);
        bridge.Scale = new Vector3(1f, 1f, bridgeLength * (1f / HexMetrics.BridgeDesignLength));

        AddChild(bridge);
    }

    public void AddSpecialFeature(HexCell cell, Vector3 position)
    {
        var instance = specialPrefabs[cell.SpecialIndex - 1].Instantiate<Node3D>();
        instance.Position = HexMetrics.Perturb(position);

        foreach (Node child in instance.GetChildren())
        {
            if (child is GeometryInstance3D geom)
                geom.SetInstanceShaderParameter("_index", cell.Index);
        }

        var hexHash = HexMetrics.SampleHashGrid(position);
        instance.Quaternion = Quaternion.FromEuler(new Vector3(0, 360f * hexHash.E, 0));

        AddChild(instance);
    }
}