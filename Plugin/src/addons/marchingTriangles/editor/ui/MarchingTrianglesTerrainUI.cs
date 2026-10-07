using System;
using System.Collections.Generic;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.data;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.editor.ui;

/// <summary>
/// Component handling the Godot Editor's display of the plugin.
/// </summary>
/// <param name="plugin"></param>
public partial class MarchingTrianglesTerrainUi(MarchingTrianglesTerrainPlugin plugin) : Node
{
    public ShaderMaterial BrushMaterial => BrushData[Plugin.ToolAttributes.BrushIndex].Item2;

    public MarchingTrianglesToolUiAttributes UiToolAttributes { get; private set; } = new(plugin);

    public MarchingTrianglesToolbar? Toolbar { get; private set; }

    public MarchingTrianglesTerrainPlugin Plugin { get; set; } = plugin;

    private bool _isVisible;

    private int _activeTool;

    public static readonly Dictionary<int, Tuple<Mesh, ShaderMaterial>> BrushData = new()
    {
        {
            0,
            new Tuple<Mesh, ShaderMaterial>(
                FileUtils.Load<Mesh>(
                    "res://addons/marchingTriangles/editor/resources/plugin_materials/round_brush_radius_visual.tres"),
                FileUtils.Load<ShaderMaterial>(
                    "res://addons/marchingTriangles/editor/resources/plugin_materials/round_brush_radius_material.tres"))
        },
        {
            1,
            new Tuple<Mesh, ShaderMaterial>(
                FileUtils.Load<Mesh>(
                    "res://addons/marchingTriangles/editor/resources/plugin_materials/square_brush_radius_visual.tres"),
                FileUtils.Load<ShaderMaterial>(
                    "res://addons/marchingTriangles/editor/resources/plugin_materials/square_brush_radius_material.tres"))
        }
    };

    public override void _EnterTree()
    {
        CallDeferred(nameof(DeferredEnterTree));
    }

    public void DeferredEnterTree()
    {
        if (!Engine.IsEditorHint())
        {
            GD.PushError("Attempting to load the plugin UI during game runtime.");
            return;
        }

        Toolbar = new MarchingTrianglesToolbar();
        Plugin.ToolAttributes.UiReloadRequested += OnToolChanged;
        Toolbar.ToolChanged += OnToolChanged;
        Toolbar.Hide();

        UiToolAttributes.PluginSettingChanged += OnPluginSettingChanged;
        UiToolAttributes.TerrainSettingChanged += OnTerrainSettingChanged;
        UiToolAttributes.Hide();

        Plugin.AddControlToContainer(EditorPlugin.CustomControlContainer.SpatialEditorSideLeft, Toolbar);
        Plugin.AddControlToContainer(EditorPlugin.CustomControlContainer.SpatialEditorBottom, UiToolAttributes);
    }

    public override void _ExitTree()
    {
        Plugin.RemoveControlFromContainer(EditorPlugin.CustomControlContainer.SpatialEditorSideLeft, Toolbar);
        Plugin.RemoveControlFromContainer(EditorPlugin.CustomControlContainer.SpatialEditorBottom, UiToolAttributes);

        Toolbar?.QueueFree();
        UiToolAttributes.QueueFree();
    }

    public void SetVisible(bool isVisible)
    {
        _isVisible = isVisible;
        Toolbar?.SetVisible(isVisible);
        UiToolAttributes.SetVisible((isVisible));

        if (isVisible)
        {
            // looks like some kind of Band-Aid to avoid multiple calls to set visible =>
            //		await get_tree().create_timer(.01).timeout
            if (Toolbar != null && Toolbar.ToolBox.Buttons.ContainsKey(_activeTool))
            {
                //Automatically trigger the tool button press to construct the 
                // tool settings via the Observers on the button press.
                Toolbar.ToolBox.Buttons[_activeTool].SetPressed(true);
            }

            UiToolAttributes.Show();
        }
    }

    private void OnTerrainSettingChanged(string setting, Variant variant)
    {
        GD.Print("Terrain Settings Changed !");
    }

    private void OnPluginSettingChanged(string setting, Variant value)
    {
        GD.Print("Plugin Settings Changed !");

        UiToolAttributes.SetPluginAttributeValue(setting, value);
    }

    //TODO : do not restart tool if same. also, this could be cleaner
    private void OnToolChanged(int toolIndex)
    {
        if ((TerrainToolMode)toolIndex == TerrainToolMode.GeometryEdit)
        {
            Plugin.CurTerrainNode?.RebuildTerrain("res://addons/marchingTriangles/editor/resources/shaders/geometryBehaviour.gdshader");
        }  else
        {
            Plugin.CurTerrainNode?.RebuildTerrain();
        }
        _activeTool = toolIndex;

        if (toolIndex
            is (int)TerrainToolMode.Bridge // BridgeTool
            or (int)TerrainToolMode.GeometryEdit) 
        {
            // FIXME => Should probably be to in the Settings itself 
            Plugin.ToolAttributes.Falloff = false;
            BrushMaterial.SetShaderParameter("falloffVisible", false);
        }

        Plugin.SelectedMode = (TerrainToolMode)toolIndex;
        Plugin.ToolAttributes.VertexColorIndex = 0; //  Set to the first material on start (Temp workaround ?)
        UiToolAttributes.ShowToolAttributes(toolIndex);

        //Grey out all tool attributes inn the terrain settings if there is at least onne chunk
        if ((TerrainToolMode)toolIndex == TerrainToolMode.TerrainSettings
            && Plugin.CurTerrainNode != null
            && Plugin.CurTerrainNode.Chunks.Count > 0)
        {
            // Since we cannot delete a chunk from this tool,the condition will remain true 
            UiToolAttributes.DisableEditToolAttributes();
        }


    }
}