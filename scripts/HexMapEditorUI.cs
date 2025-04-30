using Godot;
using System;

[GlobalClass]
public partial class HexMapEditorUI : Node3D
{
    #region Signals

    [Signal]
    public delegate void EditModeExitedEventHandler();

    #endregion

    #region Public Properties

    public bool PaintTerrainElevationEnabled { get; set; }
    public bool ApplyWaterLevel { get; set; }
    public bool ApplyUrbanLevel { get; set; }
    public bool ApplyFarmLevel { get; set; }
    public bool ApplyPlantLevel { get; set; }
    public bool ApplySpecialFeature { get; set; }

    public int ActiveTerrainTypeIndex { get; set; }
    public int ActiveElevation { get; set; }
    public int ActiveBrushSize { get; set; }
    public bool ActiveShowLabels { get; set; } = true;
    public int ActiveWaterLevel { get; set; }
    public int ActiveUrbanLevel { get; set; }
    public int ActiveFarmLevel { get; set; }
    public int ActivePlantLevel { get; set; }
    public int ActiveSpecialFeature { get; set; }

    public OptionalToggle RiverMode { get; set; } = OptionalToggle.Ignore;
    public OptionalToggle RoadMode { get; set; } = OptionalToggle.Ignore;
    public OptionalToggle WallsMode { get; set; } = OptionalToggle.Ignore;

    #endregion

    #region Exported Properties

    [Export]
    public HexGrid HexGrid { get; set; }

    [Export]
    public HexMapCamera MainCameraAssembly { get; set; }

    [Export]
    public DebugCamera DebugCamera { get; set; }

    #endregion

    #region Private Fields

    private bool _isDrag;
    private HexDirection _dragDirection = HexDirection.NE;
    private HexCell _mouseDownCell;
    private HexCell _mouseUpCell;
    private bool _isLeftShiftPressed;
    private bool _enabled;
    private readonly HexMapGenerator _hexMapGenerator = new();
    private int _selectedMapSize;
    private bool _shouldGenerateRandomMap;
    private bool _shouldUseWrapping;

    // UI Controls
    private Label _elevationLabel;
    private CheckButton _checkButtonEnableElevation;
    private Label _brushSizeValueLabel;
    private CheckButton _checkButtonShowLabels;
    private CheckBox _checkButtonColorNone;
    private CheckBox _checkButtonColorYellow;
    private CheckBox _checkButtonColorGreen;
    private CheckBox _checkButtonColorBlue;
    private CheckBox _checkButtonColorOrange;
    private CheckBox _checkButtonColorWhite;
    private CheckBox _checkButtonRiversIgnore;
    private CheckBox _checkButtonRiversYes;
    private CheckBox _checkButtonRiversNo;
    private CheckBox _checkButtonRoadsIgnore;
    private CheckBox _checkButtonRoadsYes;
    private CheckBox _checkButtonRoadsNo;
    private CheckButton _checkButtonWaterLevel;
    private Label _waterLevelValueLabel;
    private CheckButton _checkButtonUrbanLevel;
    private Label _urbanLevelValueLabel;
    private CheckButton _checkButtonFarmLevel;
    private Label _farmLevelValueLabel;
    private CheckButton _checkButtonPlantLevel;
    private Label _plantLevelValueLabel;
    private CheckBox _checkButtonWallsIgnore;
    private CheckBox _checkButtonWallsYes;
    private CheckBox _checkButtonWallsNo;
    private CheckButton _checkButtonSpecialFeature;
    private OptionButton _dropDownSpecialFeature;
    private PopupPanel _newMapPopupPanel;
    private FileDialog _saveMapFileDialog;
    private FileDialog _loadMapFileDialog;
    private Label _currentCameraValueLabel;
    private CheckButton _checkButtonEditMode;

    #endregion

    #region Lifecycle Methods

    public override void _Ready()
    {
        InitializeNodeReferences();
        InitializeUI();
        // HexGrid = GetNode<HexGrid>("../../HexGrid");
        // MainCameraAssembly = GetNode<HexMapCamera>("../../HexMapCamera");
        // DebugCamera = GetNode<DebugCamera>("../../DebugCamera");

        _hexMapGenerator.HexGrid = HexGrid;
    }

    public override void _Process(double delta)
    {
        if (_enabled)
        {
            var pos = GetWorldPositionUnderCursor();
            foreach (var unit in HexGrid.Units)
            {
                unit.LookAt(pos);
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_enabled) return;

        if (@event is InputEventKey eventKey)
        {
            HandleKeyInput(eventKey);
        }
        else if (@event is InputEventMouseButton eventMouse)
        {
            HandleMouseInput(eventMouse);
        }
    }

    #endregion

    #region Public Methods

    public void Enable()
    {
        if (!_enabled)
        {
            _enabled = true;
            GetNode<CanvasLayer>("CanvasLayer").Visible = true;

            HexGrid.SetAllCellLabelModes(ActiveShowLabels ? 
                CellInformationLabelMode.Position : 
                CellInformationLabelMode.Off);
        }
    }

    public void Disable()
    {
        if (_enabled)
        {
            _enabled = false;
            HexGrid.DisableAllCellHighlights();
            HexGrid.ResetAllCellDistances();
            HexGrid.ResetAllCellLabels();
            HexGrid.SetAllCellLabelModes(CellInformationLabelMode.Information);
            GetNode<CanvasLayer>("CanvasLayer").Visible = false;
            EmitSignal(SignalName.EditModeExited);
        }
    }

    public void ShowUI(bool visible)
    {
        if (HexGrid != null)
        {
            HexGrid.SetAllCellLabelModes(visible ?
                CellInformationLabelMode.Position :
                CellInformationLabelMode.Off);
        }
    }

    #endregion

    #region Private Methods

    private void InitializeNodeReferences()
    {
        _elevationLabel = GetNode<Label>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/HBoxContainer/ElevationValueLabel");
        _checkButtonEnableElevation = GetNode<CheckButton>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/CheckButton_EnableElevation");
        _brushSizeValueLabel = GetNode<Label>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/HBoxContainer2/BrushSizeValueLabel");
        _checkButtonShowLabels = GetNode<CheckButton>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/CheckButton_ShowLabels");
        
        _checkButtonColorNone = GetNode<CheckBox>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/GridContainer/CheckBox_NoColor");
        _checkButtonColorYellow = GetNode<CheckBox>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/GridContainer/CheckBox_ColorYellow");
        _checkButtonColorGreen = GetNode<CheckBox>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/GridContainer/CheckBox_ColorGreen");
        _checkButtonColorBlue = GetNode<CheckBox>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/GridContainer/CheckBox_ColorBlue");
        _checkButtonColorOrange = GetNode<CheckBox>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/GridContainer/CheckBox_ColorOrange");
        _checkButtonColorWhite = GetNode<CheckBox>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/GridContainer/CheckBox_ColorWhite");
        
        _checkButtonRiversIgnore = GetNode<CheckBox>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/HBoxContainer3/CheckBox_RiversIgnore");
        _checkButtonRiversYes = GetNode<CheckBox>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/HBoxContainer3/CheckBox_RiversYes");
        _checkButtonRiversNo = GetNode<CheckBox>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/HBoxContainer3/CheckBox_RiversNo");
        
        _checkButtonRoadsIgnore = GetNode<CheckBox>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/HBoxContainer4/CheckBox_RoadsIgnore");
        _checkButtonRoadsYes = GetNode<CheckBox>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/HBoxContainer4/CheckBox_RoadsYes");
        _checkButtonRoadsNo = GetNode<CheckBox>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/HBoxContainer4/CheckBox_RoadsNo");
        
        _checkButtonWaterLevel = GetNode<CheckButton>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/CheckButton_WaterLevel");
        _waterLevelValueLabel = GetNode<Label>("CanvasLayer/HBoxContainer/PanelContainer/VBoxContainer/HBoxContainer5/WaterLevelValueLabel");
        
        _checkButtonUrbanLevel = GetNode<CheckButton>("CanvasLayer/PanelContainer2/MarginContainer/VBoxContainer/CheckButton_UrbanLevel");
        _urbanLevelValueLabel = GetNode<Label>("CanvasLayer/PanelContainer2/MarginContainer/VBoxContainer/HBoxContainer/UrbanLevelValueLabel");
        
        _checkButtonFarmLevel = GetNode<CheckButton>("CanvasLayer/PanelContainer2/MarginContainer/VBoxContainer/CheckButton_FarmLevel");
        _farmLevelValueLabel = GetNode<Label>("CanvasLayer/PanelContainer2/MarginContainer/VBoxContainer/HBoxContainer2/FarmLevelValueLabel");
        
        _checkButtonPlantLevel = GetNode<CheckButton>("CanvasLayer/PanelContainer2/MarginContainer/VBoxContainer/CheckButton_PlantLevel");
        _plantLevelValueLabel = GetNode<Label>("CanvasLayer/PanelContainer2/MarginContainer/VBoxContainer/HBoxContainer3/PlantLevelValueLabel");
        
        _checkButtonWallsIgnore = GetNode<CheckBox>("CanvasLayer/PanelContainer2/MarginContainer/VBoxContainer/HBoxContainer4/CheckBox_WallsIgnore");
        _checkButtonWallsYes = GetNode<CheckBox>("CanvasLayer/PanelContainer2/MarginContainer/VBoxContainer/HBoxContainer4/CheckBox_WallsYes");
        _checkButtonWallsNo = GetNode<CheckBox>("CanvasLayer/PanelContainer2/MarginContainer/VBoxContainer/HBoxContainer4/CheckBox_WallsNo");
        
        _checkButtonSpecialFeature = GetNode<CheckButton>("CanvasLayer/PanelContainer2/MarginContainer/VBoxContainer/CheckButton_SpecialFeature");
        _dropDownSpecialFeature = GetNode<OptionButton>("CanvasLayer/PanelContainer2/MarginContainer/VBoxContainer/OptionButton_SpecialFeature");
        
        _newMapPopupPanel = GetNode<PopupPanel>("CanvasLayer/PopupPanel");
        _saveMapFileDialog = GetNode<FileDialog>("CanvasLayer/SaveFileDialog");
        _loadMapFileDialog = GetNode<FileDialog>("CanvasLayer/LoadFileDialog");
        
        _currentCameraValueLabel = GetNode<Label>("CanvasLayer/HBoxContainer/PanelContainer3/MarginContainer/VBoxContainer/HBoxContainer/CurrentCameraValueLabel");
        _checkButtonEditMode = GetNode<CheckButton>("CanvasLayer/PanelContainer2/MarginContainer/VBoxContainer/CheckButton_EditMode");
    }

    private void InitializeUI()
    {
        _checkButtonShowLabels.ButtonPressed = ActiveShowLabels;
        _checkButtonEnableElevation.ButtonPressed = PaintTerrainElevationEnabled;
        _checkButtonWaterLevel.ButtonPressed = ApplyWaterLevel;
        _checkButtonUrbanLevel.ButtonPressed = ApplyUrbanLevel;
        _checkButtonFarmLevel.ButtonPressed = ApplyFarmLevel;
        _checkButtonPlantLevel.ButtonPressed = ApplyPlantLevel;
        _checkButtonSpecialFeature.ButtonPressed = ApplySpecialFeature;

        // Initialize special features dropdown
        _dropDownSpecialFeature.Clear();
        _dropDownSpecialFeature.AddItem("None");
        _dropDownSpecialFeature.AddItem("Castle");
        _dropDownSpecialFeature.AddItem("Ziggurat");
        _dropDownSpecialFeature.AddItem("Megaflora");
        _dropDownSpecialFeature.Selected = ActiveSpecialFeature;

        InitializeTerrainTypeButtons();
        InitializeRiverModeButtons();
        InitializeRoadModeButtons();
        InitializeWallModeButtons();
    }

    private void InitializeTerrainTypeButtons()
    {
        switch (ActiveTerrainTypeIndex)
        {
            case -1:
                _checkButtonColorNone.ButtonPressed = true;
                break;
            case 0:
                _checkButtonColorYellow.ButtonPressed = true;
                break;
            case 1:
                _checkButtonColorGreen.ButtonPressed = true;
                break;
            case 2:
                _checkButtonColorBlue.ButtonPressed = true;
                break;
            case 3:
                _checkButtonColorOrange.ButtonPressed = true;
                break;
            case 4:
                _checkButtonColorWhite.ButtonPressed = true;
                break;
        }
    }

    private void InitializeRiverModeButtons()
    {
        switch (RiverMode)
        {
            case OptionalToggle.Ignore:
                _checkButtonRiversIgnore.ButtonPressed = true;
                break;
            case OptionalToggle.Yes:
                _checkButtonRiversYes.ButtonPressed = true;
                break;
            case OptionalToggle.No:
                _checkButtonRiversNo.ButtonPressed = true;
                break;
        }
    }

    private void InitializeRoadModeButtons()
    {
        switch (RoadMode)
        {
            case OptionalToggle.Ignore:
                _checkButtonRoadsIgnore.ButtonPressed = true;
                break;
            case OptionalToggle.Yes:
                _checkButtonRoadsYes.ButtonPressed = true;
                break;
            case OptionalToggle.No:
                _checkButtonRoadsNo.ButtonPressed = true;
                break;
        }
    }

    private void InitializeWallModeButtons()
    {
        switch (WallsMode)
        {
            case OptionalToggle.Ignore:
                _checkButtonWallsIgnore.ButtonPressed = true;
                break;
            case OptionalToggle.Yes:
                _checkButtonWallsYes.ButtonPressed = true;
                break;
            case OptionalToggle.No:
                _checkButtonWallsNo.ButtonPressed = true;
                break;
        }
    }

    private void HandleKeyInput(InputEventKey eventKey)
    {
        if (eventKey.Pressed)
        {
            if (eventKey.Keycode == Key.Shift && eventKey.Location == KeyLocation.Left)
            {
                _isLeftShiftPressed = true;
            }
            else if (eventKey.Keycode == Key.U)
            {
                if (_isLeftShiftPressed)
                {
                    DestroyUnit();
                }
                else
                {
                    CreateUnit();
                }
            }
        }
        else if (eventKey.Keycode == Key.Shift && eventKey.Location == KeyLocation.Left)
        {
            _isLeftShiftPressed = false;
        }
    }

    private void HandleMouseInput(InputEventMouseButton eventMouse)
    {
        if (eventMouse.ButtonIndex != MouseButton.Left) return;

        var cell = GetCellUnderCursor();
        if (cell == null) return;

        if (eventMouse.Pressed)
        {
            _mouseDownCell = cell;
        }
        else
        {
            _mouseUpCell = cell;
            if (_mouseDownCell != null && _mouseUpCell != null && _mouseDownCell != _mouseUpCell)
            {
                ValidateDrag();
            }
            else
            {
                _isDrag = false;
            }
            EditCells(cell);
        }
    }

    private void ValidateDrag()
    {
        _dragDirection = HexDirection.NE;
        while (_dragDirection <= HexDirection.NW)
        {
            if (_mouseDownCell.GetNeighbor(_dragDirection) == _mouseUpCell)
            {
                _isDrag = true;
                return;
            }
            _dragDirection++;
        }
        _isDrag = false;
    }

    private HexCell GetCellUnderCursor()
    {
        const float RayLength = 1000f;
        var mousePos = GetViewport().GetMousePosition();
        var camera = GetViewport().GetCamera3D();
        var origin = camera.ProjectRayOrigin(mousePos);
        var end = origin + camera.ProjectRayNormal(mousePos) * RayLength;

        var rayQuery = PhysicsRayQueryParameters3D.Create(origin, end);
        rayQuery.CollideWithAreas = true;

        return HexGrid.GetCellFromRay(rayQuery);
    }

    private Vector3 GetWorldPositionUnderCursor()
    {
        const float RayLength = 1000f;
        var mousePos = GetViewport().GetMousePosition();
        var camera = GetViewport().GetCamera3D();
        var origin = camera.ProjectRayOrigin(mousePos);
        var end = origin + camera.ProjectRayNormal(mousePos) * RayLength;

        var rayQuery = PhysicsRayQueryParameters3D.Create(origin, end);
        rayQuery.CollideWithAreas = true;

        var spaceState = GetWorld3D().DirectSpaceState;
        var result = spaceState.IntersectRay(rayQuery);
        if (result != null && result.ContainsKey("position"))
        {
            return (Vector3)result["position"];
        }
        return Vector3.Zero;
    }

    private void EditCells(HexCell centerCell)
    {
        var centerX = centerCell.HexCoordinates.X;
        var centerZ = centerCell.HexCoordinates.Z;

        // Edit bottom half of brush
        for (int r = 0, z = centerZ - ActiveBrushSize; z <= centerZ; z++, r++)
        {
            for (int x = centerX - r; x <= centerX + ActiveBrushSize; x++)
            {
                var coords = new HexCoordinates(x, z);
                EditCell(HexGrid.GetCellFromCoordinates(coords));
            }
        }

        // Edit top half of brush
        for (int r = 0, z = centerZ + ActiveBrushSize; z > centerZ; z--, r++)
        {
            for (int x = centerX - ActiveBrushSize; x <= centerX + r; x++)
            {
                var coords = new HexCoordinates(x, z);
                EditCell(HexGrid.GetCellFromCoordinates(coords));
            }
        }
    }

    private void EditCell(HexCell cell)
    {
        if (cell == null) return;

        if (ActiveTerrainTypeIndex >= 0)
        {
            cell.TerrainTypeIndex = ActiveTerrainTypeIndex;
        }

        if (PaintTerrainElevationEnabled)
        {
            cell.Elevation = ActiveElevation;
        }

        if (ApplyWaterLevel)
        {
            cell.WaterLevel = ActiveWaterLevel;
        }

        if (ApplySpecialFeature)
        {
            cell.SpecialIndex = ActiveSpecialFeature;
        }

        if (ApplyUrbanLevel)
        {
            cell.UrbanLevel = ActiveUrbanLevel;
        }

        if (ApplyFarmLevel)
        {
            cell.FarmLevel = ActiveFarmLevel;
        }

        if (ApplyPlantLevel)
        {
            cell.PlantLevel = ActivePlantLevel;
        }

        HandleOptionalToggles(cell);
    }

    private void HandleOptionalToggles(HexCell cell)
    {
        if (RiverMode == OptionalToggle.No)
        {
            cell.RemoveRiver();
        }

        if (RoadMode == OptionalToggle.No)
        {
            cell.RemoveRoads();
        }

        if (WallsMode != OptionalToggle.Ignore)
        {
            cell.Walled = (WallsMode == OptionalToggle.Yes);
        }

        if (_isDrag)
        {
            var oppositeDirection = _dragDirection.Opposite();
            var otherCell = cell.GetNeighbor(oppositeDirection);
            if (otherCell != null)
            {
                if (RiverMode == OptionalToggle.Yes)
                {
                    otherCell.SetOutgoingRiver(_dragDirection);
                }

                if (RoadMode == OptionalToggle.Yes)
                {
                    otherCell.AddRoad(_dragDirection);
                }
            }
        }
    }

    private void SaveMap(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.Store32((uint)HexMetrics.MapFileVersion);
        HexGrid.Save(file);
    }

    private void LoadMap(string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        var version = (int)file.Get32();
        if (version <= HexMetrics.MapFileVersion)
        {
            HexGrid.Load(file, version);
            MainCameraAssembly.ValidatePosition();
        }
    }

    private void CreateUnit()
    {
        var cell = GetCellUnderCursor();
        if (cell != null && cell.Unit == null)
        {
            var unit = GD.Load<PackedScene>("res://scenes/hex_unit.tscn").Instantiate<HexUnit>();
            HexGrid.AddUnit(unit, cell, GD.Randf() * 360f);
        }
    }

    private void DestroyUnit()
    {
        var cell = GetCellUnderCursor();
        if (cell?.Unit != null)
        {
            HexGrid.RemoveUnit(cell.Unit);
        }
    }

    #endregion

    #region Event Handlers

    private void OnCheckBoxNoColorPressed() => ActiveTerrainTypeIndex = -1;
    private void OnCheckBoxColorYellowPressed() => ActiveTerrainTypeIndex = 0;
    private void OnCheckBoxColorGreenPressed() => ActiveTerrainTypeIndex = 1;
    private void OnCheckBoxColorBluePressed() => ActiveTerrainTypeIndex = 2;
    private void OnCheckBoxColorOrangePressed() => ActiveTerrainTypeIndex = 3;
    private void OnCheckBoxColorWhitePressed() => ActiveTerrainTypeIndex = 4;

    private void OnElevationSliderValueChanged(float value)
    {
        ActiveElevation = (int)value;
        _elevationLabel.Text = ActiveElevation.ToString();
    }

    private void OnCheckButtonEnableElevationToggled(bool toggledOn)
    {
        PaintTerrainElevationEnabled = toggledOn;
    }

    private void OnBrushSizeSliderValueChanged(float value)
    {
        ActiveBrushSize = (int)value;
        _brushSizeValueLabel.Text = ActiveBrushSize.ToString();
    }

    private void OnCheckButtonShowLabelsToggled(bool toggledOn)
    {
        ActiveShowLabels = toggledOn;
        ShowUI(ActiveShowLabels);
    }

    private void OnCheckBoxRiversIgnorePressed() => RiverMode = OptionalToggle.Ignore;
    private void OnCheckBoxRiversYesPressed() => RiverMode = OptionalToggle.Yes;
    private void OnCheckBoxRiversNoPressed() => RiverMode = OptionalToggle.No;

    private void OnCheckBoxRoadsIgnorePressed() => RoadMode = OptionalToggle.Ignore;
    private void OnCheckBoxRoadsYesPressed() => RoadMode = OptionalToggle.Yes;
    private void OnCheckBoxRoadsNoPressed() => RoadMode = OptionalToggle.No;

    private void OnCheckButtonWaterLevelToggled(bool toggledOn)
    {
        ApplyWaterLevel = toggledOn;
    }

    private void OnWaterLevelSliderValueChanged(float value)
    {
        ActiveWaterLevel = (int)value;
        _waterLevelValueLabel.Text = ActiveWaterLevel.ToString();
    }

    private void OnUrbanLevelSliderValueChanged(float value)
    {
        ActiveUrbanLevel = (int)value;
        _urbanLevelValueLabel.Text = ActiveUrbanLevel.ToString();
    }

    private void OnCheckButtonUrbanLevelToggled(bool toggledOn)
    {
        ApplyUrbanLevel = toggledOn;
    }

    private void OnCheckButtonFarmLevelToggled(bool toggledOn)
    {
        ApplyFarmLevel = toggledOn;
    }

    private void OnFarmLevelSliderValueChanged(float value)
    {
        ActiveFarmLevel = (int)value;
        _farmLevelValueLabel.Text = ActiveFarmLevel.ToString();
    }

    private void OnCheckButtonPlantLevelToggled(bool toggledOn)
    {
        ApplyPlantLevel = toggledOn;
    }

    private void OnPlantLevelSliderValueChanged(float value)
    {
        ActivePlantLevel = (int)value;
        _plantLevelValueLabel.Text = ActivePlantLevel.ToString();
    }

    private void OnCheckBoxWallsIgnorePressed() => WallsMode = OptionalToggle.Ignore;
    private void OnCheckBoxWallsYesPressed() => WallsMode = OptionalToggle.Yes;
    private void OnCheckBoxWallsNoPressed() => WallsMode = OptionalToggle.No;

    private void OnCheckButtonSpecialFeatureToggled(bool toggledOn)
    {
        ApplySpecialFeature = toggledOn;
    }

    private void OnOptionButtonSpecialFeatureItemSelected(int index)
    {
        ActiveSpecialFeature = index;
    }

    private void OnSaveButtonPressed()
    {
        MainCameraAssembly.Locked = true;
        DebugCamera.Locked = true;
        _saveMapFileDialog.Show();
    }

    private void OnLoadButtonPressed()
    {
        MainCameraAssembly.Locked = true;
        DebugCamera.Locked = true;
        _loadMapFileDialog.Show();
    }

    private void OnNewMapButtonPressed()
    {
        _newMapPopupPanel.Show();
        MainCameraAssembly.Locked = true;
        DebugCamera.Locked = true;
    }

    private void OnCancelButtonPressed()
    {
        _newMapPopupPanel.Hide();
        MainCameraAssembly.Locked = false;
        DebugCamera.Locked = false;
    }

    private void OnConfirmButtonPressed()
    {
        _newMapPopupPanel.Hide();
        MainCameraAssembly.Locked = false;
        DebugCamera.Locked = false;

        var (x, z) = _selectedMapSize switch
        {
            1 => (40, 30),
            2 => (80, 60),
            _ => (20, 15)
        };

        if (_shouldGenerateRandomMap)
        {
            _hexMapGenerator.HexGrid = HexGrid;
            _hexMapGenerator.GenerateMap(x, z, _shouldUseWrapping);
        }
        else
        {
            HexGrid.CreateMap(x, z, _shouldUseWrapping);
        }

        MainCameraAssembly.ValidatePosition();
    }

    private void OnCheckBoxSmallMapPressed() => _selectedMapSize = 0;
    private void OnCheckBoxMediumMapPressed() => _selectedMapSize = 1;
    private void OnCheckBoxLargeMapPressed() => _selectedMapSize = 2;

    private void OnGenerateMapCheckBoxToggled(bool toggledOn)
    {
        _shouldGenerateRandomMap = toggledOn;
    }

    private void OnUseWrappingCheckBoxToggled(bool toggledOn)
    {
        _shouldUseWrapping = toggledOn;
    }

    private void OnSaveFileDialogFileSelected(string path)
    {
        SaveMap(path);
        MainCameraAssembly.Locked = false;
        DebugCamera.Locked = false;
    }

    private void OnSaveFileDialogCanceled()
    {
        MainCameraAssembly.Locked = false;
        DebugCamera.Locked = false;
    }

    private void OnLoadFileDialogFileSelected(string path)
    {
        LoadMap(path);
        MainCameraAssembly.Locked = false;
        DebugCamera.Locked = false;
        MainCameraAssembly.ValidatePosition();
    }

    private void OnLoadFileDialogCanceled()
    {
        MainCameraAssembly.Locked = false;
        DebugCamera.Locked = false;
    }

    private void OnExitEditModeButtonPressed()
    {
        EmitSignal(SignalName.EditModeExited);
    }

    private void OnCheckButtonEditModeToggled(bool toggledOn)
    {
        if (toggledOn)
        {
            Enable();
        }
        else
        {
            Disable();
        }
    }

    #endregion
}