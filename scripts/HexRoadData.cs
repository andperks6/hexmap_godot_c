using Godot;

public class HexRoadData
{
    public bool HasRoadThroughEdge { get; }
    public bool PreviousHasRiver { get; }
    public bool NextHasRiver { get; }
    public Vector2 Interpolators { get; }
    public HexDirection Direction { get; }
    public HexCell Cell { get; }

    public HexRoadData(HexDirection direction, HexCell cell)
    {
        Direction = direction;
        Cell = cell;
        HasRoadThroughEdge = cell.HasRoadThroughEdge(direction);
        PreviousHasRiver = cell.HasRiverThroughEdge(direction.Previous());
        NextHasRiver = cell.HasRiverThroughEdge(direction.Next());
        Interpolators = CalculateInterpolators();
    }

    public bool HasSequentialRiverFlow =>
        Cell.IncomingRiverDirection == Cell.OutgoingRiverDirection.Previous() ||
        Cell.IncomingRiverDirection == Cell.OutgoingRiverDirection.Next();

    public bool HasOppositeRiverFlow =>
        Cell.IncomingRiverDirection == Cell.OutgoingRiverDirection.Opposite();

    private Vector2 CalculateInterpolators()
    {
        if (HasRoadThroughEdge)
        {
            return new Vector2(0.5f, 0.5f);
        }

        float x = Cell.HasRoadThroughEdge(Direction.Previous()) ? 0.5f : 0.25f;
        float y = Cell.HasRoadThroughEdge(Direction.Next()) ? 0.5f : 0.25f;

        return new Vector2(x, y);
    }

    public Vector3 AdjustRoadCenterForSequentialFlow()
    {
        var direction = Cell.IncomingRiverDirection;
        var cornerFactor = 0.2f;

        if (direction == Cell.OutgoingRiverDirection.Previous())
        {
            return Cell.Position - HexMetrics.GetSecondCorner(direction) * cornerFactor;
        }
        return Cell.Position - HexMetrics.GetFirstCorner(direction) * cornerFactor;
    }

    public Vector3 GetRoadCenter(Vector3 center, out Vector3 adjustedCenter)
    {
        Vector3 roadCenter = center;
        adjustedCenter = center;

        if (Cell.HasRiverBeginningOrEnd)
        {
            var dir = Cell.RiverBeginOrEndDirection;
            var oppDir = dir.Opposite();
            return center + HexMetrics.GetSolidEdgeMiddle(oppDir) * HexMeshConstants.FeatureThirdOffset;
        }

        if (HasOppositeRiverFlow)
        {
            Vector3 corner;
            if (PreviousHasRiver)
            {
                if (!HasRoadThroughEdge && !Cell.HasRoadThroughEdge(Direction.Next()))
                {
                    return Vector3.Zero; // Signal to skip triangulation
                }
                corner = HexMetrics.GetSecondSolidCorner(Direction);
            }
            else
            {
                if (!HasRoadThroughEdge && !Cell.HasRoadThroughEdge(Direction.Previous()))
                {
                    return Vector3.Zero; // Signal to skip triangulation
                }
                corner = HexMetrics.GetFirstSolidCorner(Direction);
            }

            roadCenter += corner * 0.5f;
            adjustedCenter += corner * 0.25f;
            return roadCenter;
        }

        if (HasSequentialRiverFlow)
        {
            return AdjustRoadCenterForSequentialFlow();
        }

        if (PreviousHasRiver && NextHasRiver)
        {
            if (!HasRoadThroughEdge)
            {
                return Vector3.Zero; // Signal to skip triangulation
            }

            Vector3 offset = HexMetrics.GetSolidEdgeMiddle(Direction) * HexMetrics.InnerToOuter;
            roadCenter += offset * 0.7f;
            adjustedCenter += offset * 0.5f;
            return roadCenter;
        }

        HexDirection middle = GetMiddleDirection();
        if (!HasValidRoadConfiguration(middle))
        {
            return Vector3.Zero; // Signal to skip triangulation
        }

        Vector3 middleOffset = HexMetrics.GetSolidEdgeMiddle(middle);
        roadCenter += middleOffset * 0.25f;
        return roadCenter;
    }

    private HexDirection GetMiddleDirection()
    {
        if (PreviousHasRiver) return Direction.Next();
        if (NextHasRiver) return Direction.Previous();
        return Direction;
    }

    private bool HasValidRoadConfiguration(HexDirection middle)
    {
        return Cell.HasRoadThroughEdge(middle) ||
               Cell.HasRoadThroughEdge(middle.Previous()) ||
               Cell.HasRoadThroughEdge(middle.Next());
    }
}