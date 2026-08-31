using System;
using System.Collections.Generic;
using Godot;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.ui;

public partial class MarchingTrianglesTerrainUi(MarchingTrianglesTerrainPlugin plugin) : Node
{
    public ShaderMaterial BrushMaterial => BrushData[plugin.ToolAttributes.BrushIndex].Item2;

    public MarchingTrianglesToolUiAttributes UiToolAttributes { get; private set; } = new(plugin);

    public MarchingTrianglesTextureSettings TextureSettings { get; private set; } = new(plugin);

    public MarchingTrianglesToolbar Toolbar { get; set; }

    public MarchingTrianglesTerrainPlugin Plugin { get; set; } = plugin;

    private bool _isVisible = false;

    private int _activeTool = 0;

    public static Dictionary<int, Tuple<Mesh, ShaderMaterial>> BrushData = new()
    {
        {
            0,
            new Tuple<Mesh, ShaderMaterial>(
                GD.Load<Mesh>(
                    "res://addons/marchingTriangles/editor/resources/plugin_materials/round_brush_radius_visual.tres"),
                GD.Load<ShaderMaterial>(
                    "res://addons/marchingTriangles/editor/resources/plugin_materials/round_brush_radius_material.tres"))
        },
        {
            1,
            new Tuple<Mesh, ShaderMaterial>(
                GD.Load<Mesh>(
                    "res://addons/marchingTriangles/editor/resources/plugin_materials/square_brush_radius_visual.tres"),
                GD.Load<ShaderMaterial>(
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

        if (Plugin == null)
        {
            GD.PushError("Plugin is not available.");
            return;
        }

        Toolbar = new MarchingTrianglesToolbar();
        Toolbar.ToolChanged += OnToolChanged;
        Toolbar.Hide();
        
        UiToolAttributes.PluginSettingChanged += OnPluginSettingChanged;
        UiToolAttributes.TerrainSettingChanged += OnTerrainSettingChanged;
        UiToolAttributes.Hide();

        TextureSettings = new MarchingTrianglesTextureSettings(Plugin);
        TextureSettings.TextureSettingChanged += OnTextureSettingChanged;
        TextureSettings.Hide();

        Plugin.AddControlToContainer(EditorPlugin.CustomControlContainer.SpatialEditorSideLeft, Toolbar);
        Plugin.AddControlToContainer(EditorPlugin.CustomControlContainer.SpatialEditorSideRight, TextureSettings);
        Plugin.AddControlToContainer(EditorPlugin.CustomControlContainer.SpatialEditorBottom, UiToolAttributes);
    }

    public override void _ExitTree()
    {
        Plugin.RemoveControlFromContainer(EditorPlugin.CustomControlContainer.SpatialEditorSideLeft, Toolbar);
        Plugin.RemoveControlFromContainer(EditorPlugin.CustomControlContainer.SpatialEditorSideRight, TextureSettings);
        Plugin.RemoveControlFromContainer(EditorPlugin.CustomControlContainer.SpatialEditorBottom, UiToolAttributes);

        Toolbar.QueueFree();
        UiToolAttributes.QueueFree();
        TextureSettings.QueueFree();
    }

    public void SetVisible(bool isVisible)
    {
        _isVisible = isVisible;
        Toolbar.SetVisible(isVisible);
        UiToolAttributes.SetVisible((isVisible));
        TextureSettings.SetVisible(isVisible);

        if (isVisible)
        {
            // looks like some kind of Band-Aid =>
            //		await get_tree().create_timer(.01).timeout
            if (Toolbar != null && Toolbar.ToolboxButtons.ContainsKey(_activeTool))
            {
                //Automatically trigger the tool button press to construct the 
                // tool settings via the Observers on the button press.
                Toolbar.ToolboxButtons[_activeTool].SetPressed(true);
            }

            UiToolAttributes.Show();
        }
    }

    private void OnTextureSettingChanged(string setting, Variant value)
    {
       GD.Print("Texture Settings Changed !");
    }

    private void OnTerrainSettingChanged(string setting, Variant variant)
    {
        GD.Print("Terrain Settings Changed !");
    }

    private void OnPluginSettingChanged(string setting, Variant value)
    {
        UiToolAttributes.SetPluginAttributeValue(setting,value);
    }

    //TODO : do not restart tool if same
    private void OnToolChanged(int toolIndex)
    {
        _activeTool = toolIndex;

        if ((TerrainToolMode)toolIndex == TerrainToolMode.VertexPainting) // VertexPainting
        {
            // FIXME => Should probably be to in the Settings itself 
            TextureSettings.Show();
            TextureSettings.AddTextureSettings();
        }
        else
        {
            // FIXME => Should probably be to in the Settings itself 
            TextureSettings.Hide();
        }

        if ((TerrainToolMode)toolIndex == TerrainToolMode.Bridge) // BridgeTool
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
            && plugin.CurTerrainNode!=null
            && plugin.CurTerrainNode.Chunks.Count>0)
        {
            // Since we cannot delete a chunk from this tool,the condition will remain true 
            UiToolAttributes.DisableEditToolAttributes();
        }
    }
}

public partial class MarchingTrianglesTextureSettings(MarchingTrianglesTerrainPlugin plugin) : ScrollContainer
{
    [Signal]
    public delegate void TextureSettingChangedEventHandler(string setting, Variant value);

    private readonly List<Dictionary<String, Variant>> VarNames = [[]];

    public void AddTextureSettings()
    {
        throw new NotImplementedException("TextureSettings.gd => ll.99-> 280");
    }

    public override void _Ready()
    {
        SetCustomMinimumSize(new Vector2(195, 0));
        AddThemeConstantOverride("separation", 5);
        AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        HorizontalScrollMode = ScrollMode.ShowNever;
    }

    public void OnTextureSettingChanged(string settingName, Variant value)
    {
        EmitSignal(nameof(TextureSettingChanged), settingName, value);
    }

    public void OnSliderDragEnded(bool ended)
    {
        foreach (var chunk in plugin.CurTerrainNode.Chunks.Values)
        {
            // chunk.GrassPlanter.RegenerateAll();
        }
    }
    
}