# C# Migration Plan for Hex Game

## Core Principles

1. **Godot-C# Interop Awareness**
   - Cache Godot property access to minimize native interop calls
   - Use proper C# property patterns for Vector3/Transform operations
   - Be mindful of collection choices (Godot vs .NET collections)
   - Handle signals through C# events where appropriate

2. **Performance Focus**
   - Minimize garbage collection pressure
   - Reduce native interop calls
   - Optimize struct usage and copying
   - Cache frequently accessed values

## Migration Phases

### Phase 1: Core Data Structures

1. **HexCoordinates**
```csharp
public readonly struct HexCoordinates : IEquatable<HexCoordinates>
{
    public readonly int X { get; }
    public readonly int Z { get; }
    public int Y => -X - Z;

    public HexCoordinates(int x, int z)
    {
        X = x;
        Z = z;
    }
}
```

2. **HexMetrics**
```csharp
public static class HexMetrics
{
    public const float OuterToInner = 0.866025404f;
    public const float InnerToOuter = 1f / OuterToInner;
    public const float OuterRadius = 10f;
    public const float InnerRadius = OuterRadius * OuterToInner;

    // Cache frequently used vectors
    private static readonly Vector3[] corners;

    static HexMetrics()
    {
        corners = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            var angle = 2f * Mathf.Pi * i / 6;
            corners[i] = new Vector3(
                OuterRadius * Mathf.Cos(angle),
                0f,
                OuterRadius * Mathf.Sin(angle)
            );
        }
    }
}
```

### Phase 2: Core Components

1. **HexCell**
```csharp
public partial class HexCell : Node3D
{
    [Signal]
    public delegate void CellChangedEventHandler();

    [Export] public Node3D CellContent { get; set; }
    [Export] public Label3D CellInformationLabel { get; set; }

    private Vector3 _position;
    public override Vector3 Position
    {
        get => _position;
        set
        {
            if (_position != value)
            {
                _position = value;
                EmitSignal(SignalName.CellChanged);
            }
        }
    }
}
```

2. **HexGrid**
```csharp
public partial class HexGrid : Node3D
{
    private readonly Dictionary<HexCoordinates, HexCell> _cells = new();
    
    // Cache transforms to reduce interop
    private Transform3D _localTransform;
    private Transform3D _globalTransform;

    public override void _Ready()
    {
        _localTransform = Transform;
        _globalTransform = GlobalTransform;
    }
}
```

### Phase 3: Features & Systems

1. **Mesh Generation**
```csharp
public class HexMesh : MeshInstance3D
{
    private Vector3[] _vertices;
    private int[] _indices;
    
    public void Begin()
    {
        // Preallocate arrays to reduce GC
        _vertices = new Vector3[1024];
        _indices = new int[1024 * 3];
    }
}
```

2. **Unit Movement**
```csharp
public partial class HexUnit : Node3D
{
    [Signal]
    public delegate void MovementCompletedEventHandler();

    private Vector3 _targetPosition;
    private HexCell _location;

    public HexCell Location
    {
        get => _location;
        set
        {
            if (_location != value)
            {
                _location?.EmitSignal(HexCell.SignalName.CellChanged);
                _location = value;
                _location?.EmitSignal(HexCell.SignalName.CellChanged);
                
                // Cache position
                var newPos = _location.Position;
                Position = newPos;
            }
        }
    }
}
```

### Phase 4: UI & Editor Systems

1. **Editor UI**
```csharp
public partial class HexMapEditorUI : Node3D
{
    [Signal]
    public delegate void EditModeChangedEventHandler(bool enabled);

    [Export]
    public HexGrid Grid { get; set; }

    // Cache frequently accessed nodes
    private Camera3D _camera;
    private Control _uiRoot;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera");
        _uiRoot = GetNode<Control>("UIRoot");
    }
}
```

## Migration Strategy

1. **incremental Migration**
   - Convert inrementally
   - Maintain existing functionality
   - Add C# improvements incrementally

2. **Testing Approach**
   - Test each converted component
   - Verify visual consistency
   - Check performance metrics

3. **Performance Considerations**
   - Profile native interop calls
   - Monitor garbage collection
   - Measure frame times

## Key Godot-C# Patterns

1. **Property Updates**
```csharp
// Instead of
Position.X = 100f;

// Use
Position = Position with { X = 100f };
// or
var newPos = Position;
newPos.X = 100f;
Position = newPos;
```

2. **Signal Handling**
```csharp
// Define signal
[Signal]
public delegate void ValueChangedEventHandler(float newValue);

// Connect signal
otherNode.ValueChanged += OnValueChanged;

// Emit signal
EmitSignal(SignalName.ValueChanged, 42f);
```

3. **Collection Usage**
```csharp
// Use .NET collections for internal logic
private readonly List<HexCell> _cells = new();

// Use Godot collections for engine interaction
[Export]
public Godot.Collections.Array<NodePath> PathsToWatch { get; set; }