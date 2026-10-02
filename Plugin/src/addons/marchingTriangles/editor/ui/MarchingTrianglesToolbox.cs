using System.Collections.Generic;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.ui;

public class MarchingTrianglesToolbox
{
    /// <summary>
    /// Static instance of the tools used by the plugin, as well as their ordering. 
    /// </summary>
    public static IReadOnlyDictionary<TerrainToolMode, MarchingTrianglesTool> Tools =>
        new Dictionary<TerrainToolMode, MarchingTrianglesTool>
        {
            [TerrainToolMode.Brush] = BrushTool,
            [TerrainToolMode.Level] = LevelTool,
            [TerrainToolMode.Smooth] = SmoothTool,
            [TerrainToolMode.Bridge] = BridgeTool,
            [TerrainToolMode.DebugBrush] = DebugBrushTool,
            [TerrainToolMode.ChunkManagement] = ChunkManagerTool,
            [TerrainToolMode.TerrainSettings] = TerrainSettingsTool,
            [TerrainToolMode.GeometryEdit] = GeometryEditorTool
        };

    public Dictionary<int, Button> Buttons { get; } = new();

    internal readonly ButtonGroup ButtonGroup = new();

    //Landscaping tools
    private static readonly MarchingTrianglesTool BrushTool =
        new("res://addons/marchingTriangles/editor/icons/brush_tool.svg",
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
                brushType: true,
                size: true,
                flatten: true,
                falloff: true));

    private static readonly MarchingTrianglesTool LevelTool =
        new("res://addons/marchingTriangles/editor/icons/level_tool.svg",
            "Level",
            "Level Tool\n" +
            "\n" +
            "Used to level terrain to a certain height.\n" +
            "\n" +
            "[SHORTCUTS]\n" +
            "• Ctrl+LMB: Set the terrain level height to the hovered cell's Y value.",
            new MarchingTrianglesToolAttributeSettings(
                brushType: true,
                size: true,
                height: true,
                falloff: true));

    private static readonly MarchingTrianglesTool SmoothTool =
        new("res://addons/marchingTriangles/editor/icons/smooth_tool.svg",
            "Smooth", "Smooth Tool\n" +
                      "\n" +
                      "Used to smooth neighbouring terrain to their average height.",
            new MarchingTrianglesToolAttributeSettings());

    private static readonly MarchingTrianglesTool BridgeTool =
        new("res://addons/marchingTriangles/editor/icons/bridge_tool.svg",
            "2", "Bridge Tool\n" +
                 "\n" +
                 "Used to create a bridge between two points.\n" +
                 "\n" +
                 "[INFO]\n" +
                 "The bridge curve falloff can be set via the \"ease value\" attribute. \n" +
                 "For reference see the ease value cheatsheet in the documentation+ folder.",
            new MarchingTrianglesToolAttributeSettings(brushType: true, size: true, easeValue: true)
        );

    // General tools
    private static readonly MarchingTrianglesTool DebugBrushTool =
        new("res://addons/marchingTriangles/editor/icons/toolicon.svg",
            "Debug",
            "Debug Brush Tool\n" +
            "\n" +
            "Used to print data about selected cells.\n" +
            "\n" +
            "[DEBUG INFO]\n" +
            "• Global position\n" +
            "• Color ID values (two Vector4's)\n" +
            "• Normal",
            new MarchingTrianglesToolAttributeSettings(brushType: true, size: true));

    private static readonly MarchingTrianglesTool ChunkManagerTool =
        new("res://addons/marchingTriangles/editor/icons/chunk_manager_tool.svg",
            "Chunk Management Tool",
            "Chunk Management Tool\n" +
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
            new MarchingTrianglesToolAttributeSettings(chunkManagement: true));

    private static readonly MarchingTrianglesTool TerrainSettingsTool =
        new("res://addons/marchingTriangles/editor/icons/terrain_settings_tool.png",
            "Terrain Settings Tool",
            "Terrain Settings Tool\n" +
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
            new MarchingTrianglesToolAttributeSettings(terrainSettings: true));

    private static readonly MarchingTrianglesTool GeometryEditorTool =
        new("res://addons/marchingTriangles/editor/icons/geometry_editor_tool.svg",
            "Geometry Editor Tool", "Geometry Edition Tool\n" +
                                    "\n" +
                                    "Used to tweak the terrain geometry's behavior when facing height changes.\n" +
                                    "\n" +
                                    "[INFO]\n" +
                                    "• Todo (cf. Issue in github)\n",
            //TODO : fill the tooltip
            new MarchingTrianglesToolAttributeSettings(size: true,geometryMode:true,geometryModeParameterEditors:true));
}