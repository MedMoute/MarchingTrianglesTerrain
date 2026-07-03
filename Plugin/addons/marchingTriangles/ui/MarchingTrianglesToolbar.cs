using System;
using System.Collections.Generic;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.ui;

[Tool]
public partial class MarchingTrianglesToolbar : VFlowContainer
{
    public MarchingTrianglesToolbar()
    {
        SetCustomMinimumSize(new Vector2(35, 35));
    }

    [Signal]
    public delegate void ToolChangedEventHandler(int toolIndex);

    public MarchingTrianglesToolbox ToolBox = new();

    private readonly ButtonGroup _toolboxButtonGroup = new();

    public Dictionary<int, Button> ToolboxButtons { get; } = new();

    /// <summary>
    /// Position of the horizontal separators in the toolbar.
    /// </summary>
    private readonly int[] _separatorIndexes = [0, 4, 6, 9];

    private readonly Func<int[], Queue<int>> _queueCreator = ints => new Queue<int>(ints);

    public override void _Ready()
    {
        _toolboxButtonGroup.Pressed += OnToolSelected;
        AddTools();
    }

    private void OnToolSelected(BaseButton toolAttributes)
    {
        EmitSignal(nameof(ToolChanged), toolAttributes.GetMeta("Index"));
    }

    private void AddTools()
    {
        if (ToolBox == null)
        {
            GD.PrintErr("Cannot instantiate MTT plugin's toolbar. The toolbox was null.");
            return;
        }

        Alignment = AlignmentMode.Center;
        for (int i = 0; i < MarchingTrianglesToolbox.Tools.Length; i++)
        {
            var queue = _queueCreator.Invoke(_separatorIndexes);
            if (i == queue.Peek())
            {
                queue.Dequeue();
                AddChild(new HSeparator());
            }

            var tool = MarchingTrianglesToolbox.Tools[i];
            Button button = new();

            button.SetName(tool.Label);
            button.SetTooltipText(tool.Tooltip);
            button.SetButtonIcon(tool.Icon);
            button.SetMeta("Index", i);
            button.SetFlat(true);
            button.SetToggleMode(true);
            float scale = EditorInterface.Singleton.GetEditorScale();
            button.CustomMinimumSize = new Vector2(30f, 30f) * scale;
            button.ExpandIcon = true;
            button.SetButtonGroup(_toolboxButtonGroup);

            var centeringContainer = new CenterContainer();
            centeringContainer.CustomMinimumSize = new Vector2(35, 35);
            centeringContainer.AddChild(button, true);
            AddChild(centeringContainer);
            ToolboxButtons[i] = button;
        }

        AddChild(new HSeparator());
    }
}

public class MarchingTrianglesToolbox
{
    //Landscaping tools
    private static readonly MarchingTrianglesTool BrushTool =
        new(GD.Load<Texture2D>("res://addons/marchingTriangles/editor/icons/brush_tool.svg"),
            "Brush",
            "Brush Tool\n" +
            "\n" +
            "Used to elevate or lower terrain.\n" +
            "\n" +
            "[SHORTCUTS]\n" +
            "• Shift+LMB+Drag: Add cells to the current draw selection.\n" +
            "• Shift+MWU/MWD: Increase or decrease the brush size..\n" +
            "• Alt/RMB/Esc: Reset the current draw selection.\n" +
            "These shortcuts apply to all brush related tools.",
            new MarchingTrianglesToolAttributeSettings(
                brushType: true, size: true, flatten: true, falloff: true,quickPaintSelection: true));

    private static readonly MarchingTrianglesTool LevelTool =
        new(GD.Load<Texture2D>("res://addons/marchingTriangles/editor/icons/level_tool.svg"),
            "Level", 
            "Level Tool\n" +
                 "\n" +
                 "Used to level terrain to a certain height.\n" +
                 "\n" +
                 "[SHORTCUTS]\n" +
                 "• Ctrl+LMB: Set the terrain level height to the hovered cell's Y value.",
            new MarchingTrianglesToolAttributeSettings(
                brushType: true, size: true, height: true, falloff: true,quickPaintSelection: true));

    private static readonly MarchingTrianglesTool SmoothTool =
        new(GD.Load<Texture2D>("res://addons/marchingTriangles/editor/icons/smooth_tool.svg"),
            "Smooth", "Smooth Tool\n" +
                      "\n" +
                      "Used to smooth neighbouring terrain to their average height.",
            new MarchingTrianglesToolAttributeSettings());

    private static readonly MarchingTrianglesTool BridgeTool =
        new(GD.Load<Texture2D>("res://addons/marchingTriangles/editor/icons/bridge_tool.svg"),
            "2", "Bridge Tool\n" +
                 "\n" +
                 "Used to create a bridge between two points.\n" +
                 "\n" +
                 "[INFO]\n" +
                 "The bridge curve falloff can be set via the \"ease value\" attribute. \n" +
                 "For reference see the ease value cheatsheet in the documentation+ folder.",
            new MarchingTrianglesToolAttributeSettings(brushType:true,size:true,easeValue:true,quickPaintSelection:true)
            );

    //Terrain visuals tools
    private static readonly MarchingTrianglesTool GrassMaskTool =
        new(GD.Load<Texture2D>("res://addons/marchingTriangles/editor/icons/grass_mask_tool.svg"),
            "Grass Mask", "[INACTIVE]\n" +
                          "Grass Mask Tool\n" +
                          "\n" +
                          "Used to control where grass gets placed.", 
            new MarchingTrianglesToolAttributeSettings()
            //new MarchingTrianglesToolAttributeSettings(brushType:true,size:true,maskMode:true)
            );

    private static readonly MarchingTrianglesTool VertexPaintTool =
        new(GD.Load<Texture2D>("res://addons/marchingTriangles/editor/icons/vertex_paint_tool.svg"),
            "Vector Paining", "[INACTIVE]\n" +
                              "Vertex Paint Tool\n" +
                              "\n" +
                              "Used to paint textures onto the terrain.\n" +
                              "\n" +
                              "[INFO]\n" +
                              "• There are 16 available textures.\n" +
                              "    • 1 through 6 have grass sprites attached to them.\n" +
                              "    • 16 is a void material making terrain invisible.\n" +
                              "• Texture presets can be used to quickly swap between texture pallets.\n" +
                              "    • They can be exported via the right hand panel at the bottom.\n" +
                              "• \"Quick Paints\" are a way to quickly set textures while modelling the terrain.\n" +
                              "    • You can make global or texture preset specific ones. \n" +
                              "        • → Create a MarchingTriangleQuickPaint resource in their dedicated folders in the parent plugin folder.",
            // new MarchingTrianglesToolAttributeSettings(brushType:true,size:true,material:true,textureName:true,texturePreset:true,paintWalls:true)
            new MarchingTrianglesToolAttributeSettings()
            );

    // General tools
    private static readonly MarchingTrianglesTool DebugBrushTool =
        new(GD.Load<Texture2D>("res://addons/marchingTriangles/editor/icons/toolicon.svg"),
            "Debug", "Debug Brush Tool\n" +
                     "\n" +
                     "Used to print data about selected cells.\n" +
                     "\n" +
                     "[DEBUG INFO]\n" +
                     "• Global position\n" +
                     "• Color ID values (two Vector4's)\n" +
                     "• Normal",
            new MarchingTrianglesToolAttributeSettings(brushType:true,size:true));

    private static readonly MarchingTrianglesTool ChunkManagerTool =
        new(GD.Load<Texture2D>("res://addons/marchingTriangles/editor/icons/chunk_manager_tool.svg"),
            "2", "Chunk Management Tool\n" +
                 "\n" +
                 "Used to create, delete and change chunk settings.\n" +
                 "\n" +
                 "[INFO]\n" +
                 "Chunk cell merge modes:\n" +
                 "• CUBIC: The most blocky of all the modes. Has minimal smoothing between cells.\n" +
                 "• POLYHEDRON: Blocky terrain with slight cell smoothing (DEFAULT CHUNK STATE).\n" +
                 "• ROUNDED_POLYHEDRON: A good 50/50 mix between having smooth and blocky terrain.\n" +
                 "• SEMI_ROUND: Mostly smooth terrain with the occasional blocky cells.\n" +
                 "• SPHERICAL: All the terrain will become smooth. This mode looks the most unnatural for the algorithm.\n" +
                 "\n" +
                 "[SHORTCUTS]\n" +
                 "• CTRL+LMB: Change the currently selected chunk to the hovered chunk.",
            new MarchingTrianglesToolAttributeSettings(chunkManagement:true));

    private static readonly MarchingTrianglesTool TerrainSettingsTool =
        new(GD.Load<Texture2D>("res://addons/marchingTriangles/editor/icons/terrain_settings_tool.png"),
            "2", "Terrain Settings Tool\n" +
                 "\n" +
                 "Used to tweak global terrain settings.\n" +
                 "\n" +
                 "[INFO]\n" +
                 "• The \"Blend Mode\" dropdown menu allows you to set the terrain's texture blending mode to suit your liking.\n" +
                 "• Setting a \"Noise Hmap\" makes the base chunk height generation procedural instead of flat. \n" +
                 "• Setting the \"Animation Fps\" value to more than 0 makes the grass sprites move with limited fps.\n " +
                 "   • Keeping it at 0 gives the grass a smooth wind based effect.\n" +
                 "• \"Ridge Threshold\" controls how close grass sprites get spawned to lowering terrain (cliffs).\n" +
                 "• \"Ledge Threshold\" controls how close grass sprites get spawned to elevating terrain (walls).",
            new MarchingTrianglesToolAttributeSettings(terrainSettings:true));

    public static MarchingTrianglesTool[] Tools =>
    [
        BrushTool,
        LevelTool,
        SmoothTool,
        BridgeTool, 
        GrassMaskTool,
        VertexPaintTool,
        DebugBrushTool,
        ChunkManagerTool,
        TerrainSettingsTool
    ];
}

public partial class MarchingTrianglesToolAttributeSettings : Resource
{
    internal MarchingTrianglesToolAttributeSettings(
        bool brushType = false,
        bool size = false,
        bool easeValue = false,
        bool height = false,
        bool strength = false,
        bool flatten = false,
        bool falloff = false,
        bool maskMode = false,
        bool material = false,
        bool textureName = false,
        bool texturePreset = false,
        bool quickPaintSelection = false,
        bool paintWalls = false,
        bool chunkManagement = false,
        bool terrainSettings = false)
    {
        BrushType = brushType;
        Size = size;
        EaseValue = easeValue;
        Height = height;
        Strength = strength;
        Flatten = flatten;
        Falloff = falloff;
        MaskMode = maskMode;
        Material = material;
        TextureName = textureName;
        TexturePreset = texturePreset;
        QuickPaintSelection = quickPaintSelection;
        PaintWalls = paintWalls;
        ChunkManagement = chunkManagement;
        TerrainSettings = terrainSettings;
    }

// General brush attributes
    [Export] public bool BrushType = false;
    [Export] public bool Size = false;
    [Export] public bool EaseValue = false;
    [Export] public bool Height = false;
    [Export] public bool Strength = false;
    [Export] public bool Flatten = false;
    [Export] public bool Falloff = false;

// Brush specific attributes
    [Export] public bool MaskMode = false;
    [Export] public bool Material = false;
    [Export] public bool TextureName = false;

// Vertex painting-related special attributes
    [Export] public bool TexturePreset = false;
    [Export] public bool QuickPaintSelection = false;
    [Export] public bool PaintWalls = false;

// Non-brush attributes
    [Export] public bool ChunkManagement = false;
    [Export] public bool TerrainSettings = false;

    public List<Tuple<string, bool>> GetPropertiesFlagList()
    {
        // Todo : maybe use reflection to programatically build the list ?
        var res = new List<Tuple<string, bool>>();
        res.Add(new Tuple<string, bool>("BrushType", BrushType));
        res.Add(new Tuple<string, bool>("Size", Size));
        res.Add(new Tuple<string, bool>("EaseValue", EaseValue));
        res.Add(new Tuple<string, bool>("Height", Height));
        res.Add(new Tuple<string, bool>("Strength", Strength));
        res.Add(new Tuple<string, bool>("Flatten", Flatten));
        res.Add(new Tuple<string, bool>("Falloff", Falloff));
        res.Add(new Tuple<string, bool>("MaskMode", MaskMode));
        res.Add(new Tuple<string, bool>("Material", Material));
        res.Add(new Tuple<string, bool>("TextureName", TextureName));
        res.Add(new Tuple<string, bool>("TexturePreset", TexturePreset));
        res.Add(new Tuple<string, bool>("QuickPaintSelection", QuickPaintSelection));
        res.Add(new Tuple<string, bool>("PaintWalls", PaintWalls));
        res.Add(new Tuple<string, bool>("ChunkManagement", ChunkManagement));
        res.Add(new Tuple<string, bool>("TerrainSettings", TerrainSettings));
        return res;
    }
}