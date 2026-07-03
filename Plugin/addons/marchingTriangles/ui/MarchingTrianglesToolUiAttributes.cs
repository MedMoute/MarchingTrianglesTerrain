using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using Godot;
using Godot.Collections;
using Array = Godot.Collections.Array;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.ui;

/// <summary>
/// UI-based attributes handler for the various plugin tools.
/// </summary>
[Tool]
public partial class MarchingTrianglesToolUiAttributes
    : ScrollContainer
{
    [Signal]
    public delegate void PluginSettingChangedEventHandler(string setting, Variant value);

    [Signal]
    public delegate void TerrainSettingChangedEventHandler(string setting, Variant variant);

    private readonly MarchingTrianglesTerrainPlugin _terrainPlugin;

    // FIXME : move to correct path
    private string _defaultTexturesPath = "res://addons/marchingTriangles/resources/texture_presets/";
    private string _defaultQuickPaintPath = "res://addons/marchingTriangles/resources/quick_paints/global/";

    private readonly System.Collections.Generic.Dictionary<string, SettingType> _typeMap = new()
    {
        { "slider", SettingType.Slider },
        { "checkbox", SettingType.Checkbox },
        { "option", SettingType.Option },
        { "text", SettingType.Text },
        { "chunk", SettingType.Chunk },
        { "terrain", SettingType.Terrain },
        { "preset", SettingType.Preset },
        { "quick_paint", SettingType.QuickPaint }
    };

    enum SettingType
    {
        Checkbox,
        Slider,
        Option,
        Text,
        Chunk,
        Terrain,
        Preset,
        QuickPaint,
        Error
    }

    private readonly System.Collections.Generic.Dictionary<string, string> _terrainSettingsData = new()
    {
        { "ChunkDimensions", "Vector2i" },
        { "CellScale", "EditorSpinSlider" },
        //TODO: FIX DEACTIVATED OPTIONS
        //  { "blend_mode", "OptionButton" },
        // // { "noise_hmap", "EditorResourcePicker" },
        //  { "default_wall_texture", "OptionButton" },
        //  { "extra_collision_layer", "OptionButton" },
        //  //Special texture settings
        //  { "use_ridge_texture", "CheckBox" },
        //  { "use_ledge_texture", "CheckBox" },
        //  { "ridge_threshold", "EditorSpinSlider" },
        //  { "ledge_threshold", "EditorSpinSlider" },
    };

    public static MarchingTriangleTerrainToolAttributesList Attributes { get; } = new();

    private readonly System.Collections.Generic.Dictionary<int, Variant> _settings = new();

    private SettingType _lastSettingType = SettingType.Error;
    public GdPluginHexTerrainChunk SelectedChunk { get; set; }

    private readonly List<GdPluginHexTerrainChunk> _currentAvailableChunks = [];

    private HBoxContainer _hboxContainer;

    public MarchingTrianglesToolUiAttributes(MarchingTrianglesTerrainPlugin terrainPlugin)
    {
        _terrainPlugin = terrainPlugin;
    }

    public override void _Ready()
    {
        SetCustomMinimumSize(new Vector2(0, 35));
        AddThemeConstantOverride("separation", 5);
        AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        VerticalScrollMode = ScrollMode.Disabled;
    }


    public void DisplayToolAttributes(int toolIdx)
    {
        _hboxContainer = new HBoxContainer();
        _hboxContainer.AddThemeConstantOverride("separation", 5);
        _hboxContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _hboxContainer.SizeFlagsVertical = SizeFlags.Fill;

        if (!Visible)
        {
            return;
        }

        foreach (var node in GetChildren())
        {
            node.QueueFree();
        }

        _settings.Clear();

        if (_terrainPlugin.Ui.Toolbar.ToolBox == null)
        {
            return;
        }

        MarchingTrianglesTool tool = MarchingTrianglesToolbox.Tools[toolIdx];

        MarchingTrianglesToolAttributeSettings settings = tool.AttributeSettings;


        // Get the tool's relevant attributes.
        List<Godot.Collections.Dictionary<string, Variant>> toolAttributes = settings.GetPropertiesFlagList()
            .Where((data) => data.Item2)
            .Select((filteredData) =>
            {
                Godot.Collections.Dictionary<string, Variant> propertiesAttributes = null;
                foreach (var propertyInfo in Attributes.GetType().GetProperties())
                {
                    if (propertyInfo.Name == filteredData.Item1)
                    {
                        propertiesAttributes =
                            (Godot.Collections.Dictionary<string, Variant>)propertyInfo.GetValue(Attributes);
                    }
                }

                return propertiesAttributes;
            }).ToList();

        foreach (var toolAttribute in toolAttributes)
        {
            // Find the setting's UI type and map it to the relevant SettingType
            if (toolAttribute.ContainsKey("type") && toolAttribute["type"].VariantType == Variant.Type.String)
            {
                toolAttribute["type"] =
                    (int)_typeMap.GetValueOrDefault((string)toolAttribute["type"], SettingType.Error);
            }

            AddToolSetting(toolAttribute);
        }

        AddChild(_hboxContainer);
        _lastSettingType = SettingType.Error; // Reset the setting type for correct VSeparators
        _terrainPlugin.GizmoPlugin.TriggerRedraw(_terrainPlugin.CurTerrainNode);
    }

    /// <summary>
    /// Processes the provided tool parameters to fill the UI with the relevant information.
    /// </summary>
    private void AddToolSetting(Godot.Collections.Dictionary<string, Variant> toolParameters)
    {
        string settingName = (String)toolParameters.GetValueOrDefault("name", "");
        SettingType.TryParse((string)toolParameters.GetValueOrDefault("type", (int)SettingType.Error),
            out SettingType settingType);
        string labelText = (String)toolParameters.GetValueOrDefault("label", "");

        if (_lastSettingType != SettingType.Error)
        {
            if (_lastSettingType == SettingType.Slider && settingType == SettingType.Slider)
            {
                return;
            }
            else if (_lastSettingType != settingType)
            {
                _hboxContainer.AddChild(new VSeparator());
            }
        }

        bool addLabel = !(settingType is SettingType.Chunk or SettingType.Terrain);

        if (addLabel)
        {
            Label label = new();
            label.SetText(labelText + ":");
            label.SetVerticalAlignment(VerticalAlignment.Center);
            label.SetCustomMinimumSize(new Vector2(50, 25));

            CenterContainer cCont = new();
            cCont.SetCustomMinimumSize(new Vector2(50, 35));
            cCont.AddChild(label, true);
            _hboxContainer.AddChild(cCont, true);
        }

        CenterContainer container = null;
        Variant savedSettingValue = GetCurrentPluginAttributeValue(settingName);
        //Process per setting type
        switch (settingType)
        {
            case SettingType.Checkbox:
                ProcessCheckboxSetting(savedSettingValue, toolParameters);
                break;
            case SettingType.Slider:
                ProcessSliderSetting(savedSettingValue, toolParameters);
                break;
            case SettingType.Option:
                ProcessOptionSetting(savedSettingValue, toolParameters);
                break;
            case SettingType.Text:
                ProcessTextSetting(savedSettingValue, toolParameters);
                break;
            case SettingType.Preset:
                ProcessTexturePresetSetting(savedSettingValue, toolParameters);
                break;
            case SettingType.QuickPaint:
                ProcessQuickPaintSetting(savedSettingValue, toolParameters);
                break;
            case SettingType.Chunk:
                ProcessChunkSetting(savedSettingValue, toolParameters);
                break;
            case SettingType.Terrain:
                ProcessTerrainSettings();
                break;
            case SettingType.Error:
                GD.PushError("Couldn't load tool attributes setting");
                break;
        }
    }

    /// <summary>
    /// Processes the UI elements' settings for the Terrain Tool mode.
    ///
    /// Contrary to the other UI Processing methods, this method relies on a preset dictionary mapping
    /// fields and their UI representation (_terrainSettingsData)
    /// The key of the dictionaries are expected to be exposed as settable properties in the Terrain Node class.
    /// </summary>
    private void ProcessTerrainSettings()
    {
        VBoxContainer vBox = new();

        void NestChildControl(Control nestedControl, HBoxContainer parentContainer)
        {
            Control control = new CenterContainer();
            control.SetCustomMinimumSize(nestedControl.GetCustomMinimumSize() + new Vector2(5, 5));
            control.AddChild(nestedControl, true);
            parentContainer.AddChild(control, true);
            vBox.AddChild(parentContainer, true);
        }

        List<string> propertyInSettings = new();
        List<string> missingProperties = new();
        // Pre-check on the existence of the expected fields in the terrain Node
        foreach (var editorSetting in _terrainSettingsData)
        {
            if (_terrainPlugin.CurTerrainNode.Get(editorSetting.Key).VariantType == Variant.Type.Nil)
            {
                // It may be in the TerrainSettings field
                if (_terrainPlugin.CurTerrainNode.TerrainSettings.Get(editorSetting.Key).VariantType ==
                    Variant.Type.Nil)
                {
                    missingProperties.Add(editorSetting.Key);
                }
                else
                {
                    propertyInSettings.Add(editorSetting.Key);
                }
            }
        }

        if (missingProperties.Count > 0)
        {
            StringBuilder sb = new();
            foreach (string missingProperty in missingProperties)
            {
                sb.Append(missingProperty + " , ");
            }

            throw new ConstraintException("Cannot process the UI for the Terrain settings as the "
                                          + nameof(MarchingTrianglesTerrain)
                                          + " class does not expose the following properties : [ " + sb + "]");
        }

        foreach (var editorSetting in _terrainSettingsData)
        {
            string pluginSettingType = editorSetting.Value;
            Variant pluginSettingValue = propertyInSettings.Contains(editorSetting.Key)
                ? _terrainPlugin.CurTerrainNode.TerrainSettings.Get(editorSetting.Key)
                : _terrainPlugin.CurTerrainNode.Get(editorSetting.Key);

            var hBox = new HBoxContainer();

            var label = new Label();
            label.SetText(editorSetting.Key + " :");
            label.SetCustomMinimumSize(new Vector2(50, 25));
            label.SetVerticalAlignment(VerticalAlignment.Center);

            var labelContainer = new CenterContainer();
            labelContainer.SetCustomMinimumSize(new Vector2(50, 35));
            labelContainer.OffsetRight = 200;
            labelContainer.AddChild(label, true);
            hBox.AddChild(labelContainer, true);

            var spacer = new Control();
            spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hBox.AddChild(spacer);

            Control terrainSettingsControl;

            switch (pluginSettingType) // Process every sub-control depending on its type
            {
                case "Vector2i":
                case "Vector3i":
                case "Vector2":
                case "Vector3":
                    var editor = _CreateVectorEditorContainer(
                        pluginSettingValue,
                        editorSetting,
                        propertyInSettings.Contains(editorSetting.Key));
                    NestChildControl(editor, hBox);
                    break;
                case "SpinBox":
                    SpinBox spinBox = new();
                    spinBox.Value = pluginSettingValue.AsDouble();
                    spinBox.ValueChanged += (val) =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    spinBox.SetCustomMinimumSize(new Vector2(25, 25));
                    NestChildControl(spinBox, hBox);
                    break;
                case "EditorSpinSlider":
                    EditorSpinSlider spinSlider = new();
                    spinSlider.SetFlat(true);
                    spinSlider.SetMin(0);
                    spinSlider.SetMax(editorSetting.Key == "wallThreshold" ? 0.5 : 1.0);
                    spinSlider.SetStep(0.01);
                    spinSlider.SetValue(pluginSettingValue.AsDouble());
                    spinSlider.ValueChanged += val =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    spinSlider.SetCustomMinimumSize(new Vector2(105, 25));
                    NestChildControl(spinSlider, hBox);
                    break;
                case "EditorResourcePicker":
                    EditorResourcePicker picker = new();
                    picker.SetBaseType(editorSetting.Key == "noiseHmap" ? "Noise" : "Texture2D");
                    picker.EditedResource = (Resource)_terrainPlugin.CurTerrainNode.Get(editorSetting.Key);
                    if (picker.GetChild(0) is Button button) button.Visible = false;
                    picker.ResourceChanged += val =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    picker.SetCustomMinimumSize(new Vector2(100, 25));
                    NestChildControl(picker, hBox);
                    break;
                case "ColorPickerButton":
                    ColorPickerButton colorPickerButton = new();
                    colorPickerButton.Color = _terrainPlugin.CurTerrainNode.Get(editorSetting.Key).AsColor();
                    colorPickerButton.ColorChanged += val =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    colorPickerButton.SetCustomMinimumSize(new Vector2(100, 25));
                    NestChildControl(colorPickerButton, hBox);
                    break;
                case "Checkbox":
                    CheckBox checkBox = new();
                    checkBox.SetFlat(true);
                    checkBox.ButtonPressed = _terrainPlugin.CurTerrainNode.Get(editorSetting.Key).AsBool();
                    checkBox.Toggled += val =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    checkBox.SetCustomMinimumSize(new Vector2(25, 25));
                    NestChildControl(checkBox, hBox);
                    break;
                case "OptionButton":
                    OptionButton optionButton = new();
                    optionButton.SetFlat(true);
                    if (editorSetting.Key == "defaultWallTexture")
                    {
                        // Populate with texture names
                        foreach (var textureName in Attributes.VpTexturePaints.TextureNames)
                        {
                            optionButton.AddItem(textureName);
                        }
                    }
                    else if (editorSetting.Key == "blendMode")
                    {
                        optionButton.AddItem(("Smoothed Triangles"));
                        optionButton.AddItem("Hard Squares");
                        optionButton.AddItem("Hard Triangles");
                    }
                    else if (editorSetting.Key == "extraCollisionLayer")
                    {
                        for (int i = 0; i < 24; i++)
                        {
                            optionButton.AddItem(string.Format((i + 9).ToString()));
                        }
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(string.Format(
                            "Unsupported Option type {0} , suppported option types are [defaultWallTexture,blendMode,extraCollisionLayer]",
                            editorSetting.Key));
                    }

                    optionButton.Selected = _terrainPlugin.CurTerrainNode.Get(editorSetting.Key).AsInt32() -
                                            (editorSetting.Key == "extraCollisionLayer" ? 0 : 9);
                    optionButton.ItemSelected += (val) =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    optionButton.SetCustomMinimumSize(new Vector2(100, 35));
                    NestChildControl(optionButton, hBox);
                    break;
                case "LineEdit":
                    LineEdit lineEdit = new();
                    lineEdit.SetFlat(true);
                    lineEdit.Text = _terrainPlugin.CurTerrainNode.Get(editorSetting.Key).ToString();
                    lineEdit.PlaceholderText = "(AutoGenerated - Scene relative)";
                    lineEdit.TextSubmitted += (val) =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    lineEdit.SetCustomMinimumSize(new Vector2(200, 25));
                    NestChildControl(lineEdit, hBox);
                    break;
                case "FolderPicker":
                    HBoxContainer folderHBox = new HBoxContainer();
                    folderHBox.AddThemeConstantOverride("separation", 4);
                    //Folder LineEdit
                    LineEdit folderLineEdit = new();
                    folderLineEdit.SetFlat(true);
                    folderLineEdit.Text = _terrainPlugin.CurTerrainNode.Get(editorSetting.Key).ToString();
                    folderLineEdit.PlaceholderText = "(AutoGenerated - Scene relative)";
                    folderLineEdit.TextSubmitted += (val) =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    folderLineEdit.SetCustomMinimumSize(new Vector2(180, 25));
                    folderHBox.AddChild(folderLineEdit);
                    // Browse button
                    Button browseButton = new();
                    browseButton.Text = "...";
                    browseButton.TooltipText = "Browse for folder";
                    browseButton.Pressed += () =>
                    {
                        _OpenFolderDialog(editorSetting.Key, folderLineEdit,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unhandled type of sub-Attribute" +
                                                          " : Unexpetedly received : " +
                                                          editorSetting.Key + " which is a " + editorSetting.Value);
            }

            if (vBox.GetChildCount() / 3 > 0) // Box "break" (new column of settings) after 3 settings
            {
                _hboxContainer.AddChild(vBox);
                _hboxContainer.AddChild(new VSeparator());
                vBox = new VBoxContainer();
            }
        }

        if (vBox.GetChildCount() > 0)
        {
            _hboxContainer.AddChild(vBox);
        }
    }

    private void _OpenFolderDialog(string editorSettingKey, LineEdit pathEditor, bool propertyInSettings)
    {
        EditorFileDialog fileDialog = new();
        fileDialog.FileMode = FileDialog.FileModeEnum.OpenDir;
        fileDialog.Access = FileDialog.AccessEnum.Resources;
        fileDialog.Title = "Select Folder";
        var currPath = pathEditor.Text;
        fileDialog.CurrentDir = currPath.Length == 0 ? "res://" : currPath.GetBaseDir();

        fileDialog.DirSelected += (dir) =>
        {
            pathEditor.Text = dir;
            OnTerrainPropertyChanged(editorSettingKey, dir, propertyInSettings);
            fileDialog.QueueFree();
        };

        fileDialog.Canceled += fileDialog.QueueFree;
        //Add the dialog to the Editor Interface's control for proper behaviour of the dialog sub window
        EditorInterface.Singleton.GetBaseControl().AddChild(fileDialog);
        fileDialog.PopupCentered(new Vector2I(500, 400));
    }


    private HBoxContainer _CreateVectorEditorContainer(Variant previousValue, KeyValuePair<string, string> setting,
        bool propertyInSettings)
    {
        var container = new HBoxContainer();
        int vectorMembers;
        // We can assume the size of the Vector looking at the defaultValue type
        switch (previousValue.VariantType)
        {
            case Variant.Type.Vector2:
            case Variant.Type.Vector2I:
                vectorMembers = 2;
                break;
            case Variant.Type.Vector3:
            case Variant.Type.Vector3I:
                vectorMembers = 3;
                break;
            case Variant.Type.Vector4:
            case Variant.Type.Vector4I:
                vectorMembers = 4;
                break;
            default:
                throw new ArgumentException("The provided argument is not Vector-typed Variant");
        }

        SpinBox[] subSpinBoxes = new SpinBox[vectorMembers];
        for (int i = 0; i < vectorMembers; i++)
        {
            string vectorFieldChar = ((char)('X' + i)).ToString();
            FieldInfo field = previousValue.Obj?.GetType().GetField(vectorFieldChar);
            if (field == null)
            {
                throw new ArgumentException("The provided default value somehow doesnt have a +" + vectorFieldChar +
                                            " field");
            }

            var spinBox = new SpinBox();
            spinBox.SetStep(
                previousValue.VariantType is Variant.Type.Vector2I or Variant.Type.Vector3I or Variant.Type.Vector4I
                    ? 1
                    : 0.1);
            spinBox.SetValue(
                previousValue.VariantType is Variant.Type.Vector2I or Variant.Type.Vector3I or Variant.Type.Vector4I
                    ? (int)field.GetValue(previousValue.Obj)!
                    : (double)field.GetValue(previousValue.Obj)!);
            spinBox.SetCustomMinimumSize(new Vector2(50, 25));
            subSpinBoxes[i] = spinBox;
            var handler = (double v) =>
            { 
                // Variant needs to be unboxed , updated, then re-boxed  for this to be applied
                var unboxed = typeof(Variant)
                    .GetMethod("As")!
                    .MakeGenericMethod(previousValue.Obj.GetType())
                    .Invoke(previousValue, null);
                Variant boxed;
                if (previousValue.VariantType is Variant.Type.Vector2I or Variant.Type.Vector3I
                    or Variant.Type.Vector4I)
                {
                    field.SetValue(unboxed, (int)v);
                }
                else
                {
                    field.SetValue(previousValue.Obj, v);
                }

                boxed = (Variant)typeof(Variant)
                    .GetMethod("From")!
                    .MakeGenericMethod(previousValue.Obj.GetType())
                    .Invoke(null, new[] { unboxed })!;


                OnTerrainPropertyChanged(
                    setting.Key,
                    boxed,
                    propertyInSettings);
            };
            spinBox.ValueChanged += handler.Invoke;
            container.AddChild(spinBox);
        }

        return container;
    }

    private void OnTerrainPropertyChanged(string propertyName, Variant value, bool inTerrainSettings)
    {
        if (inTerrainSettings)
        {
            _terrainPlugin.CurTerrainNode.TerrainSettings.Set(propertyName, value);
        }

        _terrainPlugin.CurTerrainNode.Set(propertyName, value);
    }

    /// <summary>
    /// Processes the UI elements' settings for the Chunk Management Tool mode.
    /// </summary>
    /// <param name="savedSetting"> previously saved value for the setting</param>
    /// <param name="toolParameters">internal parameters of the UI setting</param>
    private void ProcessChunkSetting(Variant savedSetting,
        Godot.Collections.Dictionary<string, Variant> toolParameters)
    {
        if (_terrainPlugin.CurTerrainNode.GetChildCount() == 0)
        {
            return;
        }

        _currentAvailableChunks.Clear();
        var chunkButton = new OptionButton();
        Array<Node> children = _terrainPlugin.CurTerrainNode.GetChildren();
        foreach (Node node in children)
        {
            if (node is GdPluginHexTerrainChunk chunk)
            {
                chunkButton.AddItem("Chunk" + chunk.Underlying.Coordinates);
                _currentAvailableChunks.Add(chunk);
            }
        }

        var selectedChunkIdx =
            _currentAvailableChunks.FindIndex((chk) => chk == _terrainPlugin.PluginHelper.CurrentSelectedChunk);
        int safeSelectedChunkIdx =
            (_currentAvailableChunks.Count != 0 && _terrainPlugin.PluginHelper.CurrentSelectedChunk != null)
                ? selectedChunkIdx
                : -1;
        chunkButton.Selected = safeSelectedChunkIdx;
        if (_currentAvailableChunks.Count != 0 || _terrainPlugin.PluginHelper.CurrentSelectedChunk != null)
        {
            SelectedChunk = _terrainPlugin.PluginHelper.CurrentSelectedChunk;
        }

        OptionButton optionButton = new();
        optionButton.SetCustomMinimumSize(new Vector2(65, 35));
        optionButton.SetFlat(true);
        foreach (GdPluginHexTerrainChunk.Mode mode in Enum.GetValues(typeof(GdPluginHexTerrainChunk.Mode)))
        {
            optionButton.AddItem(mode.ToString());
        }

        optionButton.Selected =
            (_currentAvailableChunks.Count != 0 && _terrainPlugin.PluginHelper.CurrentSelectedChunk != null)
                ? _terrainPlugin.PluginHelper.CurrentSelectedChunk.Underlying.MergeMode
                : -1;
        optionButton.ItemSelected += (chunk) => OnChunkSelected(optionButton, chunkButton.GetItemText((int)chunk));
        chunkButton.SetCustomMinimumSize(new Vector2(65, 35));

        // TODO support multiple choice 
        // marching_squares_too_attributes => ll.375 - 400

        var container = new CenterContainer();
        container.SetCustomMinimumSize(new Vector2(65, 35));
        container.AddChild(optionButton, true);
        _hboxContainer.AddChild(container, true);
    }

    private void ProcessQuickPaintSetting(Variant savedSetting,
        Godot.Collections.Dictionary<string, Variant> toolParameters)
    {
        OptionButton quickPaint = new();
        quickPaint.AddItem("None");
        quickPaint.SetItemMetadata(0, new Variant());
        /// 1. Load GLOBAL quick paints from folder (always available)
        var dir = DirAccess.Open(_defaultQuickPaintPath);
        if (dir != null)
        {
            dir.ListDirBegin();
            var fileName = dir.GetNext();
            while (fileName != "")
            {
                if (fileName.EndsWith(".tres") || fileName.EndsWith(".res"))
                {
                    // TODO implement => tool_attributes ll.300 -> 350
                    GD.PushError("Found a .(t)res in the quick pain preset, not doing anything with it");
                }
            }
        }

        var container = new CenterContainer();
        container.SetCustomMinimumSize(new Vector2(65, 35));
        container.AddChild(quickPaint, true);
        _hboxContainer.AddChild(container, true);
    }

    /// <summary>
    /// Process a UI setting that will take shape of a TexturePreset selection in the Editor
    /// </summary>
    /// <param name="savedSetting"> previously saved value for the setting</param>
    /// <param name="toolParameters">internal parameters of the UI setting</param>
    private void ProcessTexturePresetSetting(Variant savedSetting,
        Godot.Collections.Dictionary<string, Variant> toolParameters)
    {
        throw new NotImplementedException();
    }

    private void ProcessTextSetting(Variant savedSetting,
        Godot.Collections.Dictionary<string, Variant> toolParameters)
    {
        string settingName = toolParameters.GetValueOrDefault("name", "").AsString();
        LineEdit lineEdit = new();
        lineEdit.ExpandToTextLength = true;
        lineEdit.PlaceholderText = toolParameters.GetValueOrDefault("default", "New text here...").AsString();
        lineEdit.TextSubmitted += (txt) => OnSettingChanged(settingName, txt);
        lineEdit.TextSubmitted += (_) => lineEdit.Clear();
        lineEdit.SetCustomMinimumSize(new Vector2(25, 25));

        var cont = new CenterContainer();
        cont.SetCustomMinimumSize(new Vector2(35, 35));
        cont.AddChild(lineEdit, true);
        _hboxContainer.AddChild(cont, true);
    }

    /// <summary>
    /// Process a UI setting that will take shape of an OptionButton in the Editor
    /// </summary>
    /// <param name="savedSetting"> previously saved value for the setting</param>
    /// <param name="toolParameters">internal parameters of the UI setting</param>
    private void ProcessOptionSetting(Variant savedSetting,
        Godot.Collections.Dictionary<string, Variant> toolParameters)
    {
        string settingName = toolParameters.GetValueOrDefault("name", "").AsString();
        Array options = toolParameters.GetValueOrDefault("options",
            new Array()).AsGodotArray();
        OptionButton optionButton = new();
        foreach (Variant option in options)
        {
            optionButton.AddItem(option.AsString());
        }

        Variant defValue = toolParameters.GetValueOrDefault("default", 0);
        if (defValue.VariantType != Variant.Type.String && defValue.AsString() != "ERROR")
        {
            defValue = savedSetting;
        }

        optionButton.Selected = defValue.AsInt32();
        optionButton.SetFlat(false);
        optionButton.ItemSelected += (idx) => OnSettingChanged(settingName, idx);

        optionButton.SetCustomMinimumSize(new Vector2(65, 35));
        var container = new CenterContainer();
        container.SetCustomMinimumSize(optionButton.GetCustomMinimumSize());
        container.AddChild(optionButton, true);
        _hboxContainer.AddChild(container, true);
    }


    /// <summary>
    /// Process a UI setting that will take shape of a Slider in the Editor
    /// </summary>
    /// <param name="savedSetting"> previously saved value for the setting</param>
    /// <param name="toolParameters">internal parameters of the UI setting</param>
    private void ProcessSliderSetting(Variant savedSetting,
        Godot.Collections.Dictionary<string, Variant> toolParameters)
    {
        string settingName = toolParameters.GetValueOrDefault("name", "").AsString();
        var rangeData = toolParameters.GetValueOrDefault("rangeData", new Vector3(1.0f, 50f, 0.5f)).AsVector3();
        // Cell size vs Dimension
        var cellScaleFactor =
            Math.Clamp(
                (_terrainPlugin.CurTerrainNode.TerrainSettings.CellScale +
                 _terrainPlugin.CurTerrainNode.TerrainSettings.CellScale) / 4.0,
                0.3f, 1f);
        var dimensionsScaleFactor =
            Math.Clamp(
                (_terrainPlugin.CurTerrainNode.TerrainSettings.ChunkDimensions.X +
                 _terrainPlugin.CurTerrainNode.TerrainSettings.ChunkDimensions.Y) / 4.0,
                0.3f, 1f);
        float scaleFactor = (float)(dimensionsScaleFactor * cellScaleFactor);
        float defVal = (float)toolParameters.GetValueOrDefault("default", 10f).AsDouble();
        if (settingName == "size")
        {
            rangeData *= scaleFactor;
            defVal *= scaleFactor;
        }

        float rangeMin = rangeData.X;
        float rangeMax = rangeData.Y;
        float rangeStep = rangeData.Z;

        if (savedSetting.VariantType != Variant.Type.String && savedSetting.AsString() != "ERROR")
        {
            defVal = (float)savedSetting.AsDouble();
        }

        var container = new MarginContainer();
        container.SetCustomMinimumSize(new Vector2(80, 35));
        if (settingName == "height" || settingName == "easeValue")
        {
            EditorSpinSlider spinSlider = new EditorSpinSlider();
            spinSlider.SetFlat(true);
            spinSlider.AllowGreater = true;
            spinSlider.AllowLesser = true;
            spinSlider.SetMin(rangeMin);
            spinSlider.SetMax(rangeMax);
            spinSlider.SetStep(rangeStep);
            spinSlider.SetValue(defVal);
            spinSlider.ValueChanged += (value) => OnSettingChanged(settingName, value);
            spinSlider.SetCustomMinimumSize(new Vector2(80, 35));

            container.AddThemeConstantOverride("margin_top", -5);
            container.AddChild(spinSlider, true);
        }
        else
        {
            HSlider hSlider = new HSlider();
            hSlider.SetMin(rangeMin);
            hSlider.SetMax(rangeMax);
            hSlider.SetStep(rangeStep);
            hSlider.SetValue(defVal);
            hSlider.SetCustomMinimumSize(new Vector2(80, 35));
            hSlider.ValueChanged += (val) => OnSettingChanged(settingName, val);
        }

        _hboxContainer.AddChild(container);
    }

    /// <summary>
    /// Process a UI setting that will take shape of a Checkbox in the Editor
    /// </summary>
    /// <param name="savedSetting"> previously saved value for the setting</param>
    /// <param name="toolParameters">internal parameters of the UI setting</param>
    private void ProcessCheckboxSetting(Variant savedSetting,
        Godot.Collections.Dictionary<string, Variant> toolParameters)
    {
        var checkBox = new CheckBox();
        checkBox.SetFlat((true));
        checkBox.ButtonPressed = (bool)toolParameters.GetValueOrDefault("default", false);
        if (savedSetting.VariantType != Variant.Type.String && savedSetting.AsString() != "ERROR")
        {
            checkBox.ButtonPressed = savedSetting.AsBool();
        }

        checkBox.Toggled += pressed =>
            OnSettingChanged(toolParameters.GetValueOrDefault("name", "").AsString(), pressed);
        checkBox.SetCustomMinimumSize(new Vector2(25, 25));
        var container = new CenterContainer();
        container.SetCustomMinimumSize(new Vector2(35, 35));
        container.AddChild(checkBox);
        _hboxContainer.AddChild(container, true);
    }

    public void OnSettingChanged(string settingName, Variant value)
    {
        EmitSignal(nameof(PluginSettingChangedEventHandler), settingName, value);
    }

    public void OnChunkSelected(OptionButton button, string chunkDesc)
    {
        GdPluginHexTerrainChunk chunk = _terrainPlugin.CurTerrainNode.FindChild(chunkDesc) as GdPluginHexTerrainChunk;

        button.Selected = chunk.Underlying.MergeMode;
        SelectedChunk = _terrainPlugin.CurTerrainNode.FindChild(chunkDesc) as GdPluginHexTerrainChunk;
        _terrainPlugin.PluginHelper.CurrentSelectedChunk = SelectedChunk;

        _terrainPlugin.GizmoPlugin.TriggerRedraw(_terrainPlugin.CurTerrainNode);
    }

    public Variant GetCurrentPluginAttributeValue(String settingName)
    {
        TerrainToolAttributes curToolAttributes = _terrainPlugin.ToolAttributes;
        switch (settingName)
        {
            case "brushType": return curToolAttributes.BrushIndex;
            case "size": return curToolAttributes.BrushSize;
            case "easeValue": return curToolAttributes.EaseValue;
            case "height": return curToolAttributes.Height;
            case "strength": return curToolAttributes.Strength;
            case "flatten": return curToolAttributes.Flatten;
            case "falloff": return curToolAttributes.Falloff;
            case "maskMode": return curToolAttributes.MaskGrass;
            case "material": return curToolAttributes.VertexColorIndex;
            case "textureName":
            case "texturePreset": return curToolAttributes.CurrentTexturePreset;
            case "quickPaintSelection": return curToolAttributes.CurrentQuickPaint;
            case "chunkManagement": return curToolAttributes.SelectedChunk;
            case "paintWalls": return curToolAttributes.PaintWalls;
            case "terrainSettings": return curToolAttributes.TerrainSettings;
            default:
                GD.PushError(
                    "Couldn't find the plugin's tool attributes value from the provided attribute setting name : " +
                    settingName);
                return "ERROR";
        }
    }

    public void ShowToolAttributes(int toolIndex)
    {
        _hboxContainer = new();
        _hboxContainer.AddThemeConstantOverride("separation", 5);
        _hboxContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _hboxContainer.SizeFlagsVertical = SizeFlags.Fill;
        if (!Visible)
        {
            return;
        }

        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }

        _settings.Clear();

        if (_terrainPlugin.Ui.Toolbar?.ToolBox == null)
        {
            return;
        }

        var tool = MarchingTrianglesToolbox.Tools[toolIndex];
        MarchingTrianglesToolAttributeSettings toolAttributes = tool.AttributeSettings;

        List<Godot.Collections.Dictionary<string, Variant>> newAttributes = new();

        // TODO iterate over properties and select the ones in AttributesList 
        if (toolAttributes.BrushType)
        {
            newAttributes.Add(Attributes.BrushType);
        }

        if (toolAttributes.Size)
        {
            newAttributes.Add(Attributes.Size);
        }

        if (toolAttributes.EaseValue)
        {
            newAttributes.Add(Attributes.EaseValue);
        }

        if (toolAttributes.Height)
        {
            newAttributes.Add(Attributes.Height);
        }

        if (toolAttributes.Flatten)
        {
            newAttributes.Add(Attributes.Flatten);
        }

        if (toolAttributes.Falloff)
        {
            newAttributes.Add(Attributes.Falloff);
        }

        if (toolAttributes.Material)
        {
            newAttributes.Add(Attributes.Material);
        }

        if (toolAttributes.TextureName)
        {
            newAttributes.Add(Attributes.TextureName);
        }

        if (toolAttributes.TexturePreset)
        {
            newAttributes.Add(Attributes.TexturePreset);
        }

        if (toolAttributes.QuickPaintSelection)
        {
            newAttributes.Add(Attributes.QuickPaintSelection);
        }

        if (toolAttributes.PaintWalls)
        {
            newAttributes.Add(Attributes.PaintWalls);
        }

        if (toolAttributes.ChunkManagement)
        {
            newAttributes.Add(Attributes.ChunkManagement);
        }

        if (toolAttributes.TerrainSettings)
        {
            newAttributes.Add(Attributes.TerrainSettings);
        }

        foreach (var attribute in newAttributes)
        {
            Godot.Collections.Dictionary<string, Variant> settingDictionary = attribute;
            var result = settingDictionary.GetValueOrDefault("type");
            if (result.VariantType != Variant.Type.Nil && result.VariantType == Variant.Type.String)
            {
                settingDictionary["type"] = (int)_typeMap.GetValueOrDefault(result.AsString(), SettingType.Error);
            }

            AddToolSetting(settingDictionary);
        }

        AddChild(_hboxContainer);
        _lastSettingType = SettingType.Error; //Reset the setting type for correct VSeparators
        _terrainPlugin.GizmoPlugin.TriggerRedraw(_terrainPlugin.CurTerrainNode);
    }
}