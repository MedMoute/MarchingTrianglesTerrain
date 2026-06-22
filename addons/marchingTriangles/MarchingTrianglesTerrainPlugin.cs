using Godot;
using Godot.Collections;
using Localproto.addons.marchingTriangles.utils;
using MarchingTrianglesTerrainUi = Localproto.addons.marchingTriangles.ui.MarchingTrianglesTerrainUi;

namespace Localproto.addons.marchingTriangles;

[Tool]
public partial class MarchingTrianglesTerrainPlugin : EditorPlugin
{
    /// <summary>
    /// Singleton plugin instance.
    /// </summary>
    public static MarchingTrianglesTerrainPlugin Instance;

    private BrushPatternCalculator _patternCalculator = new();

    /// <summary>
    /// Gizmo Plugin instance.
    ///
    /// This plugin relies on the EditorNode3DGizmoPlugin class to facilitate Gizmo-specific tasks.
    /// </summary>
    public editor.gizmo.MarchingTrianglesGizmoPlugin GizmoPlugin { get; private set; } = new();

    /// <summary>
    /// Component storing the plugin's Editor UI subcomponents. 
    /// </summary>
    public MarchingTrianglesTerrainUi Ui { get; private set; }

    /// <summary>
    /// Component for computing physics-related information (raycast etc...
    /// </summary>
    private readonly MarchingTrianglesPhysicsDelegate _physicsDelegate = new();

    /// <summary>
    /// The terrain currently being edited by this plugin.
    /// </summary>
    public MarchingTrianglesTerrain CurTerrainNode { get; private set; }

    /// <summary>
    /// Global plugin initialization flag.
    /// </summary>
    private static bool _init;

    /// <summary>
    /// Initialization Error hint.
    /// </summary>
    private string _initError;

    private TerrainToolMode _selectedMode = TerrainToolMode.Brush;

    public TerrainToolAttributes ToolAttributes { get; } = new();

    public TerrainToolPluginHelper PluginHelper { get; private set; }

    public TerrainToolMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            _selectedMode = value;
            PluginHelper.ClearDrawPattern();
            if (_selectedMode == TerrainToolMode.VertexPainting)
            {
                ToolAttributes.Flatten = false;
                Ui.BrushMaterial.SetShaderParameter("FalloffVisible", false);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _physicsDelegate.DoDelegatedPhysicsComputations();
    }

    public override void _EnterTree()
    {
        Instance = this;
        Ui = new MarchingTrianglesTerrainUi(Instance);
        PluginHelper = new TerrainToolPluginHelper(_physicsDelegate, ToolAttributes, GizmoPlugin, Ui, Instance);
        if (CurTerrainNode != null)
        {
            ToolAttributes.TextureUpdated += CurTerrainNode.ForceRebuildTerrain;
        }

        CallDeferred(nameof(DeferredEnterTree));
        GD.PrintRich(
            "Welcome to [color=MEDIUM_ORCHID]Yūgen[/color]'s Marching [s]Squares[/s] Triangles Terrain Authoring Toolkit - [color=ORANGE]HerrMyth[/color]'s Edition\n" +
            "This plugin is under MIT license.");
    }

    public override bool _Handles(GodotObject @object)
    {
        if (!_init)
        {
            GD.PushError("Plugin not yet initialized, calling _safe_initialize() as failsafe");
            if (!SafeInit())
            {
                GD.PushError("Failed to initialize plugin for editing");
                return false;
            }
        }

        return @object is MarchingTrianglesTerrain;
    }

    public override void _Edit(GodotObject @object)
    {
        if (@object is MarchingTrianglesTerrain terrain)
        {
            if (Ui != null)
            {
                CurTerrainNode = terrain;
                Ui.SetVisible(true);

                CurTerrainNode.TerrainSettings.ChunkDimensionsChanged += OnChunkDimensionsChanged;

                //Sync plugin's preset from the selected terrain's saved preset
                //This ensures each terrain keeps its own preset on selection/reload
                ToolAttributes.SyncFromTerrain = true;
                ToolAttributes.CurrentTexturePreset = terrain.CurrentTexturePreset;
                ToolAttributes.SyncFromTerrain = false;
            }
        }
        else
        {
            if (Ui != null)
            {
                Ui.SetVisible(false);
                PluginHelper.CurrentDrawPattern.Clear();
                PluginHelper.Drawing = false;
                PluginHelper.HeightSet = false;
                GizmoPlugin.Clear();
            }
        }
    }

    private void DeferredEnterTree()
    {
        var success = SafeInit();
        if (!success)
        {
            GD.PushError("Failed to initialize the plugin." + _initError);
        }
        else
        {
            GD.Print("[MarchingTrianglesTerrainPlugin] initialized successfully!");
        }
    }


    private bool SafeInit()
    {
        if (_init) return true;
        if (!Engine.IsEditorHint())
        {
            _initError = "Plugin was initialized during game runtime";
            return false;
        }

        if (EditorInterface.Singleton == null)
        {
            _initError = "No EditorInterface was detected while initializing";
            return false;
        }

        if (GetTree() == null)
        {
            _initError = "No SceneTree was detected while initializing";
            return false;
        }

        var terrainScript = GD.Load("res://addons/marchingTriangles/MarchingTrianglesTerrain.cs") as Script;
        var chunkScript = GD.Load("res://addons/marchingTriangles/HexTerrainCell.cs") as Script;

        if (terrainScript != null && chunkScript != null)
        {
            var terrainIcon =
                GD.Load("res://addons/marchingTriangles/editor/icons/Marching_Squares_Terrain_Icon.svg") as Texture2D;
            var chunkIcon =
                GD.Load("res://addons/marchingTriangles/editor/icons/Marching_Squares_Terrain_Chunk_Icon.svg") as
                    Texture2D;
            AddCustomType(nameof(MarchingTrianglesTerrain), nameof(Node3D), terrainScript, terrainIcon);
            AddCustomType(nameof(GdPluginHexTerrainChunk), nameof(Node3D), chunkScript, chunkIcon);
        }
        else
        {
            _initError = "Failed to load algorithm scripts";
            return false;
        }

        if (GizmoPlugin != null)
        {
            AddNode3DGizmoPlugin(GizmoPlugin);
        }
        else
        {
            _initError = "Failed to create gizmo plugin";
            return false;
        }

        if (Ui != null)
        {
            Ui.Plugin = Instance;
            AddChild(Ui);
        }
        else
        {
            _initError = "Failed to create gizmo plugin";
            return false;
        }

        _init = true;
        return _init;
    }

    private void OnChunkDimensionsChanged(int height, Vector2I value)
    {
        //Keep the ratio same I guess ?
        //ToolAttributes.BrushSize *= (value.X / 33d + value.Y / 33d) / 2.0;
    }

    public override void _ExitTree()
    {
        if (Ui != null)
        {
            Ui.QueueFree();
            Ui = null;
        }

        RemoveCustomType(nameof(MarchingTrianglesTerrain));
        RemoveCustomType(nameof(GdPluginHexTerrainChunk));
        if (GizmoPlugin != null)
        {
            RemoveNode3DGizmoPlugin(GizmoPlugin);
            GizmoPlugin.Dispose();
            GizmoPlugin = null;
        }

        _init = false;
        _initError = "";
    }

    public override void _Ready()
    {
        // To counteract the default setter I think, seems weird.
        ToolAttributes.Flatten = false;
        Ui.BrushMaterial.SetShaderParameter("FalloffVisible", ToolAttributes.Falloff);
    }

    /// <summary>
    /// Handles the mouse clicks / handling in 3D viewport.
    /// Input processing is delegated through a TerrainToolHelper.
    /// </summary>
    public override int _Forward3DGuiInput(Camera3D viewportCamera, InputEvent @event)
    {
        if (!_init)
            return (int)AfterGuiInput.Pass;
        var selected = EditorInterface.Singleton.GetSelection().GetSelectedNodes();
        if (selected == null)
        {
            return (int)AfterGuiInput.Pass;
        }

        if (selected.Count > 1)
        {
            GD.PrintErr("Selection was aborted due to more than one Editable node being selected.\n" +
                        "If editing terrain with MTT, make sure you only have one terrain selected.");
            return (int)AfterGuiInput.Pass;
        }

        if (@event is InputEventMouseButton || @event is InputEventMouseMotion)
        {
            return PluginHelper.HandleMouseEvent(viewportCamera, selected[0] as Node3D, @event, _selectedMode,
                GetUndoRedo());
        }

        return (int)AfterGuiInput.Pass;
    }

    public void DelegateCompositePatternAction(MarchingTrianglesTerrain terrain,
        Godot.Collections.Dictionary<
            string,
            Godot.Collections.Dictionary<
                Vector2I,
                Godot.Collections.Dictionary<
                    Vector3I,
                    Variant>>> patternActionData)
    {
        PluginHelper.ApplyCompositePatternAction(terrain, patternActionData);
    }
}

internal class MarchingTrianglesPhysicsDelegate
{
    private bool _raycastQueued = false;
    private Vector3 _rayOrigin;
    private Vector3 _rayDir;
    private Camera3D _rayCamera;

    public Dictionary QueuedRayResult { get; private set; } = new();

    public void QueueRaycast(Vector3 origin, Vector3 direction, Camera3D rayCamera)
    {
        _rayOrigin = origin;
        _rayDir = direction;
        _rayCamera = rayCamera;
        _raycastQueued = true;
    }

    internal void DoDelegatedPhysicsComputations()
    {
        //TODO : Fixme => only intersect terrain
        if (!_raycastQueued)
            return;
        _raycastQueued = false;

        World3D world = _rayCamera.GetWorld3D();
        var spaceState = PhysicsServer3D.SpaceGetDirectState(world.Space);

        float rayLength = 10_000f;
        var endOOfRay = _rayOrigin + _rayDir * rayLength;
        var query = PhysicsRayQueryParameters3D.Create(
            _rayOrigin,
            endOOfRay);
        QueuedRayResult = spaceState.IntersectRay(query);
    }
}

public enum TerrainToolMode
{
    Brush = 0,
    Level = 1,
    Smooth = 2,
    Bridge = 3,
    GrassMask = 4,
    VertexPainting = 5,
    DebugBrush = 6,
    ChunkManagement = 7,
    TerrainSettings = 8
}