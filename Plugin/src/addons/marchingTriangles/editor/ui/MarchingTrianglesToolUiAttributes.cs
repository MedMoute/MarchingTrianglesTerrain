using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using Godot;
using Godot.Collections;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using Microsoft.VisualBasic.CompilerServices;
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


    private readonly System.Collections.Generic.Dictionary<string, UiSettingType> _terrainSettingsData = new()
    {
        { nameof(TerrainSettings.ChunkDimensions), UiSettingType.Vector2I },
        { nameof(TerrainSettings.CellScale), UiSettingType.EditorSpinSlider },
        { nameof(TerrainSettings.ChunkBlendMode), UiSettingType.OptionButton },
        { nameof(TerrainSettings.ChunkBlendModeFallBack), UiSettingType.OptionButton },
        { nameof(TerrainSettings.ThresholdComputationMode), UiSettingType.OptionButton },
        { nameof(TerrainSettings.ThresholdValue), UiSettingType.EditorSpinSlider },
        { nameof(TerrainSettings.CollisionLayer), UiSettingType.OptionButton }
    };

    public static MarchingTriangleTerrainToolAttributesList Attributes { get; } = new();

    private SettingType _lastSettingType = SettingType.Error;
    public GdPluginHexTerrainChunk? SelectedChunk { get; set; }

    private static readonly StringName NewChunk = "New Chunk";

    /// <summary>
    ///  The container for the tool settings
    /// </summary>
    private HBoxContainer _hboxContainer = new ();

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

    /// <summary>
    /// The most important method of this component : Parses the tool's attribute list and display
    /// the tool's attributes in a panel. 
    /// </summary>
    /// <param name="toolIdx"></param>
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


        if (_terrainPlugin.Ui?.Toolbar?.ToolBox == null)
        {
            return;
        }

        MarchingTrianglesTool tool = MarchingTrianglesToolbox.Tools[(TerrainToolMode)toolIdx];

        MarchingTrianglesToolAttributeSettings settings = tool.AttributeSettings;


        // Get the tool's relevant attributes.
        List<Godot.Collections.Dictionary<string, Variant>> toolAttributes = settings.GetPropertiesFlagList()
            .Where(data => data.Item2)
            .Select(filteredData =>
            {
                Godot.Collections.Dictionary<string, Variant>? propertiesAttributes = null;
                foreach (var propertyInfo in Attributes.GetType().GetProperties())
                {
                    if (propertyInfo.Name == filteredData.Item1)
                    {
                        propertiesAttributes = (Godot.Collections.Dictionary<string, Variant>)propertyInfo.GetValue(Attributes)!;
                    }
                }

                return propertiesAttributes ==null ? throw new Exception(): propertiesAttributes;
            }).ToList();

        foreach (var toolAttribute in toolAttributes)
        {
            // Find the setting's UI type and map it to the relevant SettingType
            if (toolAttribute.ContainsKey(UiAttributeKey.Type) &&
                toolAttribute[UiAttributeKey.Type].VariantType == Variant.Type.Int)
            {
                toolAttribute[UiAttributeKey.Type] = (int)toolAttribute[UiAttributeKey.Type];
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
    private void AddToolSetting(Godot.Collections.Dictionary<string, Variant> toolSettingParameters)
    {
        string settingName = (String)toolSettingParameters.GetValueOrDefault(UiAttributeKey.Name, "");
        Enum.TryParse((string)toolSettingParameters.GetValueOrDefault(UiAttributeKey.Type, (int)SettingType.Error),
            out SettingType settingType);
        string labelText = (String)toolSettingParameters.GetValueOrDefault(UiAttributeKey.Label, "");

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

        Variant savedSettingValue = GetCurrentPluginAttributeValue(settingName);
        //Process per setting type
        switch (settingType)
        {
            case SettingType.Checkbox:
                ProcessCheckboxSetting(savedSettingValue, toolSettingParameters);
                break;
            case SettingType.Slider:
                ProcessSliderSetting(savedSettingValue, toolSettingParameters);
                break;
            case SettingType.Option:
                ProcessOptionSetting(savedSettingValue, toolSettingParameters);
                break;
            case SettingType.Text:
                ProcessTextSetting(savedSettingValue, toolSettingParameters);
                break;
            case SettingType.Preset:
                ProcessTexturePresetSetting(savedSettingValue, toolSettingParameters);
                break;
            case SettingType.QuickPaint:
                throw new NotSupportedException("Quickpaint not supported");
                //ProcessQuickPaintSetting(savedSettingValue, toolSettingParameters);
            case SettingType.Chunk:
                ProcessChunkSetting();
                break;
            case SettingType.Terrain:
                ProcessTerrainSettings();
                break;
            case SettingType.GeometryModePicker:
                ProcessGeometryModePickerSetting();
                break;
            case SettingType.GeometryModeParameterEditor:
                ProcessGeometryModeParameterSetting();
                break;
            case SettingType.Error:
                GD.PushError("Couldn't load tool attributes setting");
                break;
        }
    }

    private void ProcessGeometryModeParameterSetting()
    {
        int maxSupportedGeometryParameters = 2;
        GeometryMode[] modes = new GeometryMode[2];
        modes[0] = (GeometryMode)_terrainPlugin.ToolAttributes.GeometryModes.X;
        modes[1] = (GeometryMode)_terrainPlugin.ToolAttributes.GeometryModes.Y;
        var vBoxContainer = new VBoxContainer();

        EditorSpinSlider?[,] sliderArray = new EditorSpinSlider[2, 2];

        for (int i = 0; i < 2; i++)
        {
            GeometryMode mode = modes[i];
            var cont = new HBoxContainer();
            for (int j = 0; j < maxSupportedGeometryParameters; j++)
            {
                var subCont = new HBoxContainer();

                Label label = new();
                label.Text = $"Parameter {j}";
                if (mode >= 0 && mode.SupportsParameter(j))
                {
                    EditorSpinSlider value = new();
                    sliderArray[i, j] = value;
                    value.EditingInteger = false;
                    value.MinValue = 0f;
                    value.MaxValue = 1f;
                    value.SetCustomMinimumSize(new Vector2(50, 35));
                    subCont.AddChild(value);
                }
                else
                {
                    sliderArray[i, j] = null;

                    Label unsupported = new();
                    unsupported.Text = "No parameter";
                    subCont.AddChild(unsupported);
                }

                cont.AddChild(label);
                cont.AddChild(subCont);
            }


            vBoxContainer.AddChild(cont);
        }

        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < maxSupportedGeometryParameters; j++)
            {
                if (sliderArray[i, j] is not null)
                {
                    sliderArray[i, j]!.ValueChanged += val =>
                    {
                        Vector4 result = new Vector4();
                        for (int k = 0; k < 2; k++)
                        {
                            for (int l = 0; l < maxSupportedGeometryParameters; l++)
                            {
                                result[2 * k + l] = (float)(sliderArray[k, l] == null
                                    ? -1f
                                    : sliderArray[k, l]!.Value);
                            }
                        }

                        result[2 * i + j] = (float)val;
                        OnSettingChanged("GeometryModeParameters", result);
                    };
                }
            }
        }

        _hboxContainer.AddChild(vBoxContainer, true);
    }

    private void ProcessGeometryModePickerSetting()
    {
        const int iconSize = 16;

        //Add behavior selector buttons
        var optionButton = OptionButton(iconSize);
        var overThresholdOptionButton = OptionButton(iconSize);

        var cont = new CenterContainer();
        cont.AddChild(optionButton, true);
        var cont2 = new CenterContainer();

        cont2.AddChild(overThresholdOptionButton, true);
        var vBoxContainer = new VBoxContainer();

        cont.SetCustomMinimumSize(new Vector2(85, 35));
        vBoxContainer.AddChild(cont);
        vBoxContainer.AddChild(cont2);

        _hboxContainer.AddChild(vBoxContainer, true);

        // -1 for NOOP offset;
        optionButton.ItemSelected += val => OnSettingChanged(
            "GeometryMode",
            new Vector2I((int)val - 1, overThresholdOptionButton.Selected - 1));

        overThresholdOptionButton.ItemSelected += val => OnSettingChanged(
            "GeometryMode",
            new Vector2I(optionButton.Selected - 1, (int)val - 1));


        OptionButton OptionButton(int _iconSize)
        {
            OptionButton button = new();
            button.SetCustomMinimumSize(new Vector2(65, 35));
            button.SetFlat(true);
            var texture = EngineUtils.Resize2DTexture("res://addons/marchingTriangles/editor/icons/empty_texture.png",
                _iconSize, _iconSize);
            button.AddIconItem(texture, "Noop");
            foreach (GeometryMode mode in Enum.GetValues(typeof(GeometryMode)))
            {
                var gradient = new Gradient();
                gradient.SetColors([mode.GetModePalette()(0f, 0f)]);
                var gradTexture = new GradientTexture2D();
                gradTexture.SetGradient(gradient);
                gradTexture.SetHeight(_iconSize);
                gradTexture.SetWidth(_iconSize);
                button.AddIconItem(gradTexture, mode.ToString()); // +1 is offset due
            }

            return button;
        }

        optionButton.Select(_terrainPlugin.ToolAttributes.GeometryModes.X + 1);
        overThresholdOptionButton.Select(_terrainPlugin.ToolAttributes.GeometryModes.Y + 1);
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
        if (_terrainPlugin.CurTerrainNode == null || _terrainPlugin.PluginHelper == null)
        {
            return;
        }

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
            // It should be in the TerrainSettings field
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

        if (missingProperties.Count > 0)
        {
            StringBuilder sb = new();
            foreach (string missingProperty in missingProperties)
            {
                sb.Append(missingProperty + " , ");
            }

            throw new ConstraintException("Cannot process the UI for the Terrain settings as the "
                                          + nameof(MarchingTrianglesTerrain) + "." + nameof(TerrainSettings)
                                          + " class does not expose the following properties : [ " + sb + "]");
        }

        var settingsType = typeof(TerrainSettings);
        var properties = settingsType.GetProperties(
            BindingFlags.DeclaredOnly |
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public);
        var enumSettings = new System.Collections.Generic.Dictionary<string, Type>();

        foreach (var pInfo in properties)
        {
            if (pInfo.PropertyType.IsEnum)
            {
                enumSettings.Add(pInfo.Name, pInfo.PropertyType);
            }
        }

        foreach (var editorSetting in _terrainSettingsData)
        {
            Func<Variant> pluginSettingValue = () => propertyInSettings.Contains(editorSetting.Key)
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

            switch (editorSetting.Value) // Process every sub-control depending on its type
            {
                case UiSettingType.Vector2I:
                case UiSettingType.Vector3I:
                case UiSettingType.Vector2:
                case UiSettingType.Vector3:
                    var editor = _CreateVectorEditorContainer(
                        pluginSettingValue,
                        editorSetting,
                        propertyInSettings.Contains(editorSetting.Key));
                    NestChildControl(editor, hBox);
                    break;
                case UiSettingType.SpinBox:
                    SpinBox spinBox = new();
                    spinBox.Value = pluginSettingValue.Invoke().AsDouble();
                    spinBox.ValueChanged += (val) =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    spinBox.SetCustomMinimumSize(new Vector2(25, 25));
                    NestChildControl(spinBox, hBox);
                    break;
                case UiSettingType.EditorSpinSlider:
                    EditorSpinSlider spinSlider = new();
                    spinSlider.SetFlat(true);
                    spinSlider.SetMin(0);
                    spinSlider.SetMax(editorSetting.Key == "wallThreshold" ? 0.5 : 1.0);
                    spinSlider.SetStep(0.01);
                    spinSlider.SetValue(pluginSettingValue.Invoke().AsDouble());
                    spinSlider.ValueChanged += val =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    spinSlider.SetCustomMinimumSize(new Vector2(105, 25));
                    NestChildControl(spinSlider, hBox);
                    break;
                case UiSettingType.EditorResourcePicker:
                    EditorResourcePicker picker = new();
                    picker.SetBaseType(editorSetting.Key == "noiseHmap" ? "Noise" : "Texture2D");
                    picker.EditedResource =
                        (Resource)_terrainPlugin.CurTerrainNode.TerrainSettings.Get(editorSetting.Key);
                    if (picker.GetChild(0) is Button button) button.Visible = false;
                    picker.ResourceChanged += val =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    picker.SetCustomMinimumSize(new Vector2(100, 25));
                    NestChildControl(picker, hBox);
                    break;
                case UiSettingType.ColorPickerButton:
                    ColorPickerButton colorPickerButton = new();
                    colorPickerButton.Color =
                        _terrainPlugin.CurTerrainNode.TerrainSettings.Get(editorSetting.Key).AsColor();
                    colorPickerButton.ColorChanged += val =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    colorPickerButton.SetCustomMinimumSize(new Vector2(100, 25));
                    NestChildControl(colorPickerButton, hBox);
                    break;
                case UiSettingType.Checkbox:
                    CheckBox checkBox = new();
                    checkBox.SetFlat(true);
                    checkBox.ButtonPressed =
                        _terrainPlugin.CurTerrainNode.TerrainSettings.Get(editorSetting.Key).AsBool();
                    checkBox.Toggled += val =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    checkBox.SetCustomMinimumSize(new Vector2(25, 25));
                    NestChildControl(checkBox, hBox);
                    break;
                case UiSettingType.OptionButton:
                    OptionButton optionButton = new();
                    optionButton.SetFlat(true);
                    //Try parsing as an enum
                    if (enumSettings.TryGetValue(editorSetting.Key, out var setting))
                    {
                        var values = Enum.GetNames(setting);
                        foreach (var value in values)
                        {
                            optionButton.AddItem(value);
                        }
                    }
                    //Special case for layers
                    else if (editorSetting.Key == "CollisionLayer")
                    {
                        for (int i = 0; i < 24; i++)
                        {
                            optionButton.AddItem(string.Format((i + 9).ToString()));
                        }
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(string.Format(
                            "Unsupported Option type {0} , supported option types are " +
                            "enums and \"extraCollisionLayer\" .",
                            editorSetting.Key));
                    }

                    optionButton.Selected =
                        editorSetting.Key == "extraCollisionLayer"
                            ? pluginSettingValue.Invoke().AsInt32() - 9
                            : pluginSettingValue.Invoke().AsInt32();
                    optionButton.ItemSelected += (val) =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    optionButton.SetCustomMinimumSize(new Vector2(100, 35));
                    NestChildControl(optionButton, hBox);
                    break;
                case UiSettingType.LineEdit:
                    LineEdit lineEdit = new();
                    lineEdit.SetFlat(true);
                    lineEdit.Text = _terrainPlugin.CurTerrainNode.TerrainSettings.Get(editorSetting.Key).ToString();
                    lineEdit.PlaceholderText = "(AutoGenerated - Scene relative)";
                    lineEdit.TextSubmitted += (val) =>
                    {
                        OnTerrainPropertyChanged(editorSetting.Key, val,
                            propertyInSettings.Contains(editorSetting.Key));
                    };
                    lineEdit.SetCustomMinimumSize(new Vector2(200, 25));
                    NestChildControl(lineEdit, hBox);
                    break;
                case UiSettingType.FolderPicker:
                    HBoxContainer folderHBox = new HBoxContainer();
                    folderHBox.AddThemeConstantOverride("separation", 4);
                    //Folder LineEdit
                    LineEdit folderLineEdit = new();
                    folderLineEdit.SetFlat(true);
                    folderLineEdit.Text =
                        _terrainPlugin.CurTerrainNode.TerrainSettings.Get(editorSetting.Key).ToString();
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

        fileDialog.DirSelected += dir =>
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


    private HBoxContainer _CreateVectorEditorContainer(Func<Variant> valueSupplier,
        KeyValuePair<string, UiSettingType> setting,
        bool propertyInSettings)
    {
        var previousValue = valueSupplier.Invoke();
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

        var subSpinBoxes = new SpinBox[vectorMembers];
        for (int i = 0; i < vectorMembers; i++)
        {
            string vectorFieldChar = ((char)('X' + i)).ToString();
            FieldInfo? field = previousValue.Obj?.GetType().GetField(vectorFieldChar);
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
                var curValue = valueSupplier.Invoke();
                // Variant needs to be unboxed , updated, then re-boxed for this to be applied
                var unboxed = typeof(Variant)
                    .GetMethod("As")!
                    .MakeGenericMethod(curValue.Obj!.GetType())
                    .Invoke(curValue, null);
                Variant boxed;
                if (curValue.VariantType is Variant.Type.Vector2I or Variant.Type.Vector3I
                    or Variant.Type.Vector4I)
                {
                    field.SetValue(unboxed, (int)v);
                }
                else
                {
                    field.SetValue(curValue.Obj, v);
                }

                boxed = (Variant)typeof(Variant)
                    .GetMethod("From")!
                    .MakeGenericMethod(curValue.Obj.GetType())
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
        if (_terrainPlugin.CurTerrainNode == null || _terrainPlugin.PluginHelper == null)
        {
            return;
        }

        if (inTerrainSettings)
        {
            _terrainPlugin.CurTerrainNode.TerrainSettings.Set(propertyName, value);
        }

        _terrainPlugin.CurTerrainNode.Set(propertyName, value);
    }

    /// <summary>
    /// Processes the UI elements' settings for the Chunk Management Tool mode.
    /// </summary>
    private void ProcessChunkSetting()
    {
        if (_terrainPlugin.CurTerrainNode == null || _terrainPlugin.PluginHelper == null)
        {
            return;
        }

        //Create the chunk selectors
        var xSelectorButton = new OptionButton();
        xSelectorButton.SetFlat(true);
        xSelectorButton.Text = "Chunk X coordinate";
        xSelectorButton.AddItem(NewChunk, int.MinValue);
        foreach (var x in _terrainPlugin.CurTerrainNode.Chunks.Keys.Select(v => v.X).Distinct())
        {
            xSelectorButton.AddItem(x.ToString());
        }

        xSelectorButton.SetCustomMinimumSize(new Vector2(65, 35));

        var zSelectorButton = new OptionButton();
        zSelectorButton.SetFlat(true);
        zSelectorButton.Text = "Chunk Z coordinate";
        zSelectorButton.AddItem(NewChunk, int.MinValue);
        foreach (var z in _terrainPlugin.CurTerrainNode.Chunks.Keys.Select(v => v.Y).Distinct())
        {
            zSelectorButton.AddItem(z.ToString());
        }

        zSelectorButton.SetCustomMinimumSize(new Vector2(65, 35));

        //Add behavior selector buttons
        OptionButton optionButton = new();
        optionButton.SetCustomMinimumSize(new Vector2(65, 35));
        optionButton.SetFlat(true);
        foreach (GeometryMode mode in Enum.GetValues(typeof(GeometryMode)))
        {
            optionButton.AddItem(mode.ToString());
        }


        OptionButton overThresholdOptionButton = new();
        overThresholdOptionButton.SetCustomMinimumSize(new Vector2(65, 35));
        overThresholdOptionButton.SetFlat(true);
        foreach (GeometryMode mode in Enum.GetValues(typeof(GeometryMode)))
        {
            overThresholdOptionButton.AddItem(mode.ToString());
        }

        //Add the update strategy for selected :
        // changing the value updates the list of provided chunks in the other button 
        // it also updates the 
        xSelectorButton.ItemSelected += index =>
        {
            var selectedChunk = UpdateValuesOfImpactedButton(
                xSelectorButton, index, zSelectorButton,
                v => v.Y,
                p => p.Item1.X == p.Item2,
                (a, b) => new Vector2I(a, b));
            if (selectedChunk == null)
            {
                optionButton.Select((int)_terrainPlugin.CurTerrainNode.TerrainSettings.ChunkBlendMode);
                overThresholdOptionButton.Select((int)_terrainPlugin.CurTerrainNode.TerrainSettings
                    .ChunkBlendModeFallBack);
            }
            else
            {
                optionButton.Select((int)selectedChunk.Underlying.DefaultGeometryModes!.Item1);
                overThresholdOptionButton.Select((int)selectedChunk.Underlying.DefaultGeometryModes.Item2);
            }
        };
        zSelectorButton.ItemSelected += index =>
        {
            var selectedChunk = UpdateValuesOfImpactedButton(
                zSelectorButton, index, xSelectorButton,
                v => v.X,
                p => p.Item1.Y == p.Item2,
                (a, b) => new Vector2I(b, a));
            if (selectedChunk == null)
            {
                optionButton.Select((int)_terrainPlugin.CurTerrainNode.TerrainSettings.ChunkBlendMode);
                overThresholdOptionButton.Select((int)_terrainPlugin.CurTerrainNode.TerrainSettings
                    .ChunkBlendModeFallBack);
            }
            else
            {
                optionButton.Select((int)selectedChunk.Underlying.DefaultGeometryModes!.Item1);
                overThresholdOptionButton.Select((int)selectedChunk.Underlying.DefaultGeometryModes.Item2);
            }
        };

        var terrain = _terrainPlugin.CurTerrainNode;

        if (terrain.Chunks.Count > 0)
        {
            _terrainPlugin.PluginHelper.CurrentSelectedChunk = terrain.Chunks.First().Value;
        }

        overThresholdOptionButton.ItemSelected += item => OnChunkUpdated(
            ((GeometryMode)optionButton.Selected, (GeometryMode)item),
            SelectedChunk);

        optionButton.ItemSelected += item => OnChunkUpdated(
            ((GeometryMode)item, (GeometryMode)overThresholdOptionButton.Selected),
            SelectedChunk);


        WrapInVBox([xSelectorButton, zSelectorButton], "Chunk Selection");
        WrapInVBox([optionButton, overThresholdOptionButton], "Chunk Geometry Behaviour");
        return;

        // Local function that is called on row/column selection.
        // This method computes the matching chunk and updates the UI selection of the chunk
        GdPluginHexTerrainChunk? UpdateValuesOfImpactedButton(
            OptionButton updatedButton,
            long index,
            OptionButton impactedButton,
            Func<Vector2I, int> selector,
            Predicate<(Vector2I, int)> filter,
            Func<int, int, Vector2I> builder)
        {
            Vector2I? tmpChunkSelected;

            var impactedPreviouslyWasChunk = int.TryParse(
                impactedButton.GetItemText(impactedButton.Selected),
                out var impactedSelectedValue);


            var selectedText = updatedButton.GetItemText((int)index);
            var updatedIsChunk = int.TryParse(selectedText, out var selectedValue);

            var terrain = _terrainPlugin.CurTerrainNode;
            //Computed values for the affected button
            List<int> impactedButtonChunkValues;

            int? computedImpactedButtonSelection;

            if (updatedIsChunk) //We selected an existing chunk row/column
            {
                impactedButtonChunkValues = terrain.Chunks.Keys.Where(v => filter((v, selectedValue)))
                    .Select(selector)
                    .Distinct().Order().ToList();
                if (impactedPreviouslyWasChunk) // The impacted button was previously a chunk, we keep the value 
                {
                    tmpChunkSelected = builder(selectedValue, impactedSelectedValue);
                }
                else // The impacted button was not set, we will select the firs value
                {
                    tmpChunkSelected = builder(selectedValue, impactedButtonChunkValues.First());
                }

                computedImpactedButtonSelection = selector(tmpChunkSelected.Value);
            }
            else //We explicitly selected a non-existing chunk row/column, we set the other to non-existing as well and 
            {
                // The values are not filtered
                impactedButtonChunkValues = terrain.Chunks.Keys.Select(selector)
                    .Distinct().Order().ToList();
                tmpChunkSelected = null;
                computedImpactedButtonSelection = null;
            }

            impactedButton.Clear();
            impactedButton.AddItem(NewChunk, int.MinValue);
            foreach (var chunkValue in impactedButtonChunkValues)
            {
                if (chunkValue == -1) //Avoid id = -1 (https://github.com/godotengine/godot/issues/124037)
                {
                    impactedButton.AddItem(chunkValue.ToString(), int.MinValue + 1);
                }
                else
                {
                    //We set the chunk values as Idx to find them later
                    impactedButton.AddItem(chunkValue.ToString(), chunkValue);
                }
            }

            //Avoid id = -1
            int? impactedSelectionIndex = computedImpactedButtonSelection == null
                ? null
                : impactedButton.GetItemIndex(
                    computedImpactedButtonSelection.Value == -1
                        ? int.MinValue + 1
                        : computedImpactedButtonSelection.Value);
            if (impactedSelectionIndex < 0)
            {
                throw new InvalidOperationException();
            }

            //We reupdate the selection of the affected button 
            if (impactedSelectionIndex is not null)
            {
                impactedButton.Select(impactedSelectionIndex.Value);
            }

            Console.WriteLine("Selected index : " + index + " [chunkValue = " + selectedValue + "] => " +
                              "Impacted selection index :" + impactedSelectionIndex
                              + " [chunkValue = " + computedImpactedButtonSelection + "]");

            Console.WriteLine("Selected  chunk : " + (tmpChunkSelected is null
                ? "NONE"
                : terrain.Chunks[tmpChunkSelected.Value].Underlying.Coordinates
                  + " => Geometry default state : < " +
                  terrain.Chunks[tmpChunkSelected.Value].Underlying.DefaultGeometryModes!.Item1 + " ; " +
                  terrain.Chunks[tmpChunkSelected.Value].Underlying.DefaultGeometryModes!.Item2 + " >"));

            _terrainPlugin.PluginHelper.CurrentSelectedChunk =
                tmpChunkSelected == null ? null : terrain.Chunks[tmpChunkSelected.Value];

            return _terrainPlugin.PluginHelper.CurrentSelectedChunk;
        }

        void WrapInVBox(List<Control> buttons, string vBoxText = "")
        {
            var vBox = new VBoxContainer();
            if (vBoxText != "")
            {
                var textZone = new Label();
                textZone.Text = vBoxText;
                vBox.AddChild(textZone);
            }

            foreach (var button in buttons)
            {
                var container = new CenterContainer();
                container.SetCustomMinimumSize(new Vector2(85, 35));
                container.AddChild(button, true);
                vBox.AddChild(container, true);
            }

            _hboxContainer.AddChild(vBox, true);
        }
    }

    // private void ProcessQuickPaintSetting(Variant savedSetting,
    //     Godot.Collections.Dictionary<string, Variant> toolParameters)
    // {
    //     OptionButton quickPaint = new();
    //     quickPaint.AddItem("None");
    //     quickPaint.SetItemMetadata(0, new Variant());
    //     // 1. Load GLOBAL quick paints from folder (always available)
    //     var dir = DirAccess.Open(_defaultQuickPaintPath);
    //     if (dir != null)
    //     {
    //         dir.ListDirBegin();
    //         var fileName = dir.GetNext();
    //         while (fileName != "")
    //         {
    //             if (fileName.EndsWith(".tres") || fileName.EndsWith(".res"))
    //             {
    //                 // TODO implement => tool_attributes ll.300 -> 350
    //                 GD.PushError("Found a .(t)res in the quick pain preset, not doing anything with it");
    //             }
    //
    //             fileName = dir.GetNext();
    //         }
    //     }
    //
    //     var container = new CenterContainer();
    //     container.SetCustomMinimumSize(new Vector2(65, 35));
    //     container.AddChild(quickPaint, true);
    //     _hboxContainer.AddChild(container, true);
    // }

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
        lineEdit.PlaceholderText =
            toolParameters.GetValueOrDefault(UiAttributeKey.Default, "New text here...").AsString();
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
        var terrain = _terrainPlugin.CurTerrainNode;
        if (terrain == null)
        {
            return;
        }

        string settingName = toolParameters.GetValueOrDefault("name", "").AsString();
        var rangeData = toolParameters.GetValueOrDefault("rangeData", new Vector3(1.0f, 50f, 0.5f)).AsVector3();
        // Cell size vs Dimension
        var cellScaleFactor =
            Math.Clamp(
                (terrain.TerrainSettings.CellScale +
                 terrain.TerrainSettings.CellScale) / 4.0,
                0.3f, 1f);
        var dimensionsScaleFactor =
            Math.Clamp(
                (terrain.TerrainSettings.ChunkDimensions.X +
                 terrain.TerrainSettings.ChunkDimensions.Y) / 4.0,
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
            container.AddChild(hSlider, true);
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
        EmitSignal(nameof(PluginSettingChanged), settingName, value);
    }

    //TODO : Use signal to help making UI structure-agnostic.
    public void OnChunkUpdated((GeometryMode, GeometryMode) state, GdPluginHexTerrainChunk? chunk)
    {
        if (_terrainPlugin.PluginHelper == null)
        {
            throw new InvalidOperationException("Plugin helper was not initialized.");
        }

        //Update the plugin UI default geometry mode value for all modes
        _terrainPlugin.ToolAttributes.GeometryModes = new Vector2I((int)state.Item1, (int)state.Item2);

        if (chunk != null)
        {
            chunk.Underlying.DefaultGeometryModes = new Tuple<GeometryMode, GeometryMode>(state.Item1, state.Item2);
            _terrainPlugin.PluginHelper.CurrentSelectedChunk = chunk;
            chunk.GenerateTerrainMesh();
            _terrainPlugin.GizmoPlugin.TriggerRedraw(_terrainPlugin.CurTerrainNode);
        }

        Console.WriteLine("Updated  chunk : " + (_terrainPlugin.PluginHelper.CurrentSelectedChunk is null
            ? "NONE"
            : _terrainPlugin.PluginHelper.CurrentSelectedChunk.Underlying.Coordinates
              + " => Geometry default state : < " +
              _terrainPlugin.PluginHelper.CurrentSelectedChunk.Underlying.DefaultGeometryModes!.Item1 + " ; " +
              _terrainPlugin.PluginHelper.CurrentSelectedChunk.Underlying.DefaultGeometryModes!.Item2 + " >"));
    }

    public void SetPluginAttributeValue(String settingName, Variant value)
    {
        TerrainToolAttributes curToolAttributes = _terrainPlugin.ToolAttributes;
        switch (settingName)
        {
            case "brushType":
                curToolAttributes.BrushIndex = value.AsInt32();
                return;
            case "size":
                curToolAttributes.BrushSize = value.AsDouble();
                return;
            case "easeValue":
                curToolAttributes.EaseValue = value.AsDouble();
                return;
            case "height":
                curToolAttributes.Height = value.AsDouble();
                return;
            case "strength":
                curToolAttributes.Strength = value.AsDouble();
                return;
            case "flatten":
                curToolAttributes.Flatten = value.AsBool();
                return;
            case "falloff":
                curToolAttributes.Falloff = value.AsBool();
                return;

            case "maskMode":
            case "material":
            case "texturePreset":
            case "quickPaintSelection":
                throw new NotSupportedException("Legacy attribute value.");
            case "chunkManagement":
                curToolAttributes.SelectedChunk = value.AsVector2I();
                return;
            case "paintWalls":
                curToolAttributes.PaintWalls = value.AsBool();
                return;
            case "terrainSettings":
                curToolAttributes.TerrainSettings = value;
                return;
            case "GeometryMode":
                curToolAttributes.GeometryModes = value.AsVector2I();
                return;
            case "GeometryModeParameters":
                curToolAttributes.GeometryModesParameters = value.AsVector4();
                return;
            default:
                GD.PushError(
                    "Couldn't find the plugin's tool attributes value from the provided attribute setting name : " +
                    settingName);
                return;
        }
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
            case "maskMode":
            case "material":
            case "textureName":
            case "texturePreset":
            case "quickPaintSelection": throw new NotSupportedException("Legacy attribute value.");
            case "chunkManagement": return curToolAttributes.SelectedChunk;
            case "paintWalls": return curToolAttributes.PaintWalls;
            case "terrainSettings": return curToolAttributes.TerrainSettings;
            case "geometryMode": return curToolAttributes.GeometryModes;
            case "geometryModeParameterEditor": return curToolAttributes.GeometryModesParameters;
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

        if (_terrainPlugin.Ui?.Toolbar == null)
        {
            return;
        }

        var tool = MarchingTrianglesToolbox.Tools[(TerrainToolMode)toolIndex];
        MarchingTrianglesToolAttributeSettings toolAttributes = tool.AttributeSettings;

        List<Godot.Collections.Dictionary<string, Variant>> toolSettings = new();

        // TODO iterate over properties and select the ones in AttributesList 
        if (toolAttributes.BrushType)
        {
            toolSettings.Add(Attributes.BrushType);
        }

        if (toolAttributes.Size)
        {
            toolSettings.Add(Attributes.Size);
        }

        if (toolAttributes.EaseValue)
        {
            toolSettings.Add(Attributes.EaseValue);
        }

        if (toolAttributes.Height)
        {
            toolSettings.Add(Attributes.Height);
        }

        if (toolAttributes.Flatten)
        {
            toolSettings.Add(Attributes.Flatten);
        }

        if (toolAttributes.Falloff)
        {
            toolSettings.Add(Attributes.Falloff);
        }

        if (toolAttributes.ChunkManagement)
        {
            toolSettings.Add(Attributes.ChunkManagement);
        }

        if (toolAttributes.TerrainSettings)
        {
            toolSettings.Add(Attributes.TerrainSettings);
        }

        if (toolAttributes.GeometryMode)
        {
            toolSettings.Add(Attributes.GeometryModePicker);
        }

        if (toolAttributes.GeometryModeParameterEditors)
        {
            toolSettings.Add(Attributes.GeometryModeParameterEditor);
        }

        foreach (var toolSettingAttributes in toolSettings)
        {
            Godot.Collections.Dictionary<string, Variant> settingDictionary = toolSettingAttributes;
            var result = settingDictionary.GetValueOrDefault(UiAttributeKey.Type);
            if (result.VariantType != Variant.Type.Nil && result.VariantType == Variant.Type.Int)
            {
                settingDictionary[UiAttributeKey.Type] = result;
            }

            AddToolSetting(settingDictionary);
        }

        AddChild(_hboxContainer);
        _lastSettingType = SettingType.Error; //Reset the setting type for correct VSeparators
        if (_terrainPlugin.CurTerrainNode != null)
        {
            _terrainPlugin.GizmoPlugin.TriggerRedraw(_terrainPlugin.CurTerrainNode);
        }
    }

    /// <summary>
    /// Disables the underlying control nodes of the tool.
    /// Warning : They are NEVER RE-ENABLED 
    /// </summary>
    public void DisableEditToolAttributes()
    {
        var recursiveChildern = _hboxContainer.GetChildren(true);
        foreach (var node in recursiveChildern)
        {
            if (node is Control ctrl)
            {
                ctrl.SetProcessMode(ProcessModeEnum.Disabled);
            }
        }
    }
}

/// <summary>
/// Supported types for top-level UI settings
/// </summary>
public enum SettingType
{
    Checkbox,
    Slider,
    Option,
    Text,
    Chunk,
    Terrain,
    Preset,
    QuickPaint,
    Error,
    GeometryModePicker,
    GeometryModeParameterEditor
}

/// <summary>
/// Enum of the supported types for low-level UI attributes.
/// </summary>
public enum UiSettingType
{
    Vector2I,
    Vector2,
    Vector3I,
    Vector3,
    SpinBox,
    EditorSpinSlider,
    EditorResourcePicker,
    ColorPickerButton,
    Checkbox,
    OptionButton,
    LineEdit,
    FolderPicker
}