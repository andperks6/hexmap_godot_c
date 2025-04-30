using Godot;
using System;

[GlobalClass]
public partial class Main : Node3D
{
    public bool EditMode { get; private set; } = true;

    public HexGrid HexGrid { get; private set; }

    private Camera3D[] _cameras;
    private HexMapEditorUI _editModeUI;
    private HexGameUI _gameModeUI;
    private int _selectedCamera = 0;

    public override void _Ready()
    {
        // Set the seed of the global random number generator
        GD.Seed(1);

        // Enable wireframe debug rendering
        RenderingServer.SetDebugGenerateWireframes(true);

        HexGrid = GetNode<HexGrid>("HexGrid");

        // Initialize camera array
        _cameras =
        [
            GetNode<Camera3D>("HexMapCamera/Swivel/Stick/MainCamera"),
            GetNode<Camera3D>("DebugCamera")
        ];

        // Set default camera
        _cameras[_selectedCamera].MakeCurrent();

        // Get UI references
        _editModeUI = GetNode<HexMapEditorUI>("UI/HexMapEditorUi");
        _gameModeUI = GetNode<HexGameUI>("UI/HexGameUi");

        // Connect signals
        _editModeUI.EditModeExited += HandleEditModeExited;
        _gameModeUI.EditModeEnabled += HandleEditModeEnabled;

        // Initialize UI
        InitializeUI();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey eventKey && eventKey.Pressed)
        {
            switch ((Key)eventKey.Keycode)
            {
                case Key.P:
                    var viewport = GetViewport();
                    viewport.DebugDraw = (Viewport.DebugDrawEnum)(((int)viewport.DebugDraw + 1) % 5);
                    break;

                case Key.C:
                    _selectedCamera = (_selectedCamera + 1) % _cameras.Length;
                    _cameras[_selectedCamera].MakeCurrent();
                    break;

                case Key.G:
                    HexGrid.HexGridOverlayEnabled = !HexGrid.HexGridOverlayEnabled;
                    break;
            }
        }
    }

    private void InitializeUI()
    {
        ToggleEditMode(EditMode);
    }

    private void HandleEditModeExited()
    {
        ToggleEditMode(false);
    }

    private void HandleEditModeEnabled()
    {
        ToggleEditMode(true);
    }

    private void ToggleEditMode(bool toggled)
    {
        EditMode = toggled;

        RenderingServer.GlobalShaderParameterSet("HEX_MAP_EDIT_MODE", EditMode);

        if (EditMode)
        {
            _editModeUI.Enable();
            _gameModeUI.Disable();
        }
        else
        {
            _editModeUI.Disable();
            _gameModeUI.Enable();
        }
    }
}