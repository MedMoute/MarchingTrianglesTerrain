using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.gizmo;
using MarchingTrianglesTerrain.addons.marchingTriangles.ui;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using MathNet.Spatial.Euclidean;
using Plane = Godot.Plane;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

public class TerrainToolPluginHelper
{
    private readonly MarchingTrianglesPhysicsDelegate _physicsDelegate;

    public TerrainToolAttributes _toolAttributes;

    private readonly MarchingTrianglesGizmoPlugin _gizmoPlugin;

    private readonly MarchingTrianglesTerrainUi _ui;

    private readonly Dictionary<Vector2I, Dictionary<Vector3I, float>> _curDrawPattern = new();

    public Dictionary<Vector2I, Dictionary<Vector3I, float>> CurrentDrawPattern => _curDrawPattern;

    public Vector3 BrushPosition { get; set; }

    public bool TerrainHovered { get; set; }
    public bool ChunkPlaneHovered { get; set; }

    public Vector2I CurrentHoveredChunk { get; set; }

    private GdPluginHexTerrainChunk? _currentSelectedChunk;

    /// <summary>
    /// The chunk currently selected by the UI.
    /// </summary>
    public GdPluginHexTerrainChunk? CurrentSelectedChunk
    {
        get => _currentSelectedChunk;
        set
        {
            _currentSelectedChunk = value;
            _ui.UiToolAttributes.SelectedChunk = value;
            _toolAttributes.SelectedChunk = value == null ? Vector2I.Zero : value.Underlying.Coordinates;
        }
    }


    // True if the mouse is currently held down to draw
    public bool Drawing { get; set; }

    // When the brush draws, if the gizmo sees the draw height is not set, it will set the draw height
    public bool HeightSet { get; set; }

    // Height of the current pattern that is being drawn at for the brush tool
    public float DrawHeight { get; set; }

    // True when the user clicks on a tile that is part of the current draw pattern, will enter heightdrag setting mode
    public bool HeightDragging { get; set; }

    // True when the mouse is dragged in ToolMode.Bridge
    public bool BridgeBuilding { get; set; }

    //True when vertex painting the walls.
    public bool WallPainting { get; set; }

    /// <summary>
    /// The parent plugin of this helper class
    /// </summary>
    private readonly MarchingTrianglesTerrainPlugin _parent;

    internal TerrainToolPluginHelper(
        MarchingTrianglesPhysicsDelegate physicsDelegate,
        TerrainToolAttributes attributes,
        MarchingTrianglesGizmoPlugin gizmoPlugin,
        MarchingTrianglesTerrainUi ui,
        MarchingTrianglesTerrainPlugin parent)
    {
        _physicsDelegate = physicsDelegate;
        _toolAttributes = attributes;
        _gizmoPlugin = gizmoPlugin;
        _ui = ui;
        _parent = parent;
    }

    public void ClearDrawPattern()
    {
        _curDrawPattern.Clear();
    }

    private void HandleLeftClickEvent(Node3D node3D,
        InputEvent inputEvent,
        TerrainToolMode terrainToolMode,
        bool drawAreaHovered,
        Vector3? drawPosition,
        EditorUndoRedoManager? redoManager)
    {
        if (redoManager == null)
        {
            return;
        }

        if (inputEvent.IsPressed())
        {
            if (terrainToolMode == TerrainToolMode.ChunkManagement && node3D is MarchingTrianglesTerrain terrain)
            {
                if (Input.IsKeyPressed(Key.Ctrl)) // Multiple selection w/ CTRL
                {
                    terrain.Chunks.TryGetValue(CurrentHoveredChunk, out var selectedChunk);

                    CurrentSelectedChunk = selectedChunk;

                    _ui.UiToolAttributes.DisplayToolAttributes((int)terrainToolMode);
                    _ui.UiToolAttributes.SelectedChunk = selectedChunk;
                }
                else
                {
                    terrain.Chunks.TryGetValue(CurrentHoveredChunk, out var selectedChunk);
                    if (selectedChunk != null) // Left-click on an already existing chunk => removal
                    {
                        redoManager.CreateAction("Remove Chunk");
                        redoManager.AddDoMethod(terrain, MarchingTrianglesTerrain.MethodName.RemoveChunkFromTree,
                            CurrentHoveredChunk, _parent);
                        redoManager.AddUndoMethod(terrain, MarchingTrianglesTerrain.MethodName.AddChunk,
                            CurrentHoveredChunk, _parent);
                        redoManager.CommitAction();
                    }
                    else if (terrain.CanAddEmptyChunk(CurrentHoveredChunk))
                    {
                        redoManager.CreateAction("Add chunk");
                        redoManager.AddDoMethod(terrain, MarchingTrianglesTerrain.MethodName.AddNewChunk,
                            CurrentHoveredChunk, _parent);
                        redoManager.AddUndoMethod(terrain, MarchingTrianglesTerrain.MethodName.RemoveChunk,
                            CurrentHoveredChunk, _parent);
                        redoManager.CommitAction();
                    }
                }
            }

            if (drawAreaHovered)
            {
                HeightSet = false;
                // Prepare bridge building
                if (terrainToolMode == TerrainToolMode.Bridge && !BridgeBuilding)
                {
                    _toolAttributes.Flatten = false;
                    BridgeBuilding = true;
                    _toolAttributes.BridgeStartPos = BrushPosition;
                }

                // Forcing Falloff values when needed
                if (terrainToolMode == TerrainToolMode.Smooth && !_toolAttributes.Falloff)
                {
                    // Force falloff when smoothing
                    _toolAttributes.Falloff = true;
                }

                if (terrainToolMode == TerrainToolMode.DebugBrush && _toolAttributes.Falloff)
                {
                    // Disable falloff when debugging (Apply to GrassMask as well)
                    _toolAttributes.Falloff = false;
                }

                // Forcing Flatten values when needed
                if (terrainToolMode is TerrainToolMode.DebugBrush
                    && _toolAttributes.Flatten)
                {
                    // Disable flatten when painting vertices or debugging
                    _toolAttributes.Flatten = false;
                }

                if (terrainToolMode is TerrainToolMode.Level && Input.IsKeyPressed(Key.Ctrl))
                {
                    //Custom height set
                    DrawHeight = BrushPosition.Y;
                }
                else if (Input.IsKeyPressed(Key.Shift))
                {
                    Drawing = true;
                    if (drawPosition != null)
                    {
                        BrushPosition = drawPosition.Value;
                    }
                }
                else
                {
                    HeightDragging = true;
                    if (!_toolAttributes.Flatten)
                    {
                        if (drawPosition != null)
                        {
                            DrawHeight = drawPosition.Value.Y;
                        }
                    }
                }
            }
        }

        else if (inputEvent.IsReleased())
        {
            if (BridgeBuilding)
            {
                BridgeBuilding = false;
            }

            if (Drawing)
            {
                Drawing = false;
                if (terrainToolMode
                    is TerrainToolMode.Level
                    or TerrainToolMode.Bridge
                    or TerrainToolMode.DebugBrush
                    or TerrainToolMode.GeometryEdit)
                {
                    // Draw the complete pattern before selection 
                    CommitDrawnPattern(node3D);
                    _curDrawPattern.Clear();
                }

                if (terrainToolMode is TerrainToolMode.Smooth)
                {
                    //Smoothing or Vertex painting don't need to draw on release since the job is already done
                    _curDrawPattern.Clear();
                }
            }

            if (HeightDragging)
            {
                HeightDragging = false;
                CommitDrawnPattern(node3D);
                if (Input.IsKeyPressed(Key.Shift))
                {
                    // Shift-Pressing on release allows for selection height reset;
                    DrawHeight = BrushPosition.Y;
                }
                else
                {
                    CurrentDrawPattern.Clear();
                }
            }
        }

        _gizmoPlugin.TriggerRedraw(node3D);
    }

    // Brush scaling on Shift Left
    private void HandleShiftClickEvent(Node3D _0, InputEvent _1, TerrainToolMode _2,
        bool _3,
        Vector3? _4)
    {
        // TerrainPlugin.gd => ll.431 - 447
        throw new NotImplementedException();
    }

    /// <summary>
    /// Processes the UI input events each frame and register changes
    /// </summary>
    public int HandleMouseEvent(Camera3D camera,
        Node3D terrainNode,
        InputEvent @event,
        TerrainToolMode mode,
        EditorUndoRedoManager undoRedoManager)
    {
        int FindUserSelectedOrHoveredChunk(Vector3 vector3, Vector3 mouseRayNormal1)
        {
            Plane plane;
            // In chunk settings/creation mode : 
            // Check for hovering over/clicking a new chunk

            plane = new Plane(Vector3.Up, Vector3.Zero);
            Vector3? intersection = plane.IntersectsRay(vector3, mouseRayNormal1);

            if (intersection.HasValue)
            {
                Vector2I chunksCoords =
                    MarchingTrianglesTerrain.GetChunkCoordsFromCartesian(
                        new Vector2D(intersection.Value.X, intersection.Value.Z),
                        (terrainNode as MarchingTrianglesTerrain)!.TerrainSettings.ChunkDimensions);


                if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left })
                {
                    HandleLeftClickEvent(terrainNode, @event, mode, false, intersection, undoRedoManager);
                }

                if (CurrentHoveredChunk != chunksCoords)
                {
                    CurrentHoveredChunk = chunksCoords;
                    ChunkPlaneHovered = true;
                    _gizmoPlugin.TriggerRedraw(terrainNode);
                }
            }
            else
            {
                ChunkPlaneHovered = false;
            }

            // Consume clicks but allow other click / mouse motion types to reach the gui, for camera movement, etc
            // => why here but not on the other  ?
            if (@event is InputEventMouseButton mouseEvent && @event.IsPressed() &&
                mouseEvent.ButtonIndex == MouseButton.Left)
                return (int)EditorPlugin.AfterGuiInput.Stop;

            return (int)EditorPlugin.AfterGuiInput.Pass;
        }

        TerrainHovered = false;

        Vector2 mousePosition = camera.GetViewport().GetMousePosition();

        Vector3 mouseRayOrigin = camera.ProjectRayOrigin(mousePosition);
        Vector3 mouseRayNormal = camera.ProjectRayNormal(mousePosition);

        Plane chunkPlane;

        switch (mode)
        {
            case TerrainToolMode.DebugBrush:
                return GetAndDebugHoverPoint();
            case TerrainToolMode.ChunkManagement:
            case TerrainToolMode.TerrainSettings:
                return FindUserSelectedOrHoveredChunk(mouseRayOrigin, mouseRayNormal);
            case TerrainToolMode.Brush:
            case TerrainToolMode.Bridge:
            case TerrainToolMode.Level:
            case TerrainToolMode.Smooth:
            case TerrainToolMode.GeometryEdit:
                return PerformRaycastAndProcessHoverPoint();
            default:
                throw new NotSupportedException($"Mouse Events are not handled for {mode} mode.");
        }

        int GetAndDebugHoverPoint()
        {
            Plane setPlane = new Plane(Vector3.Up,
                Vector3.Zero);
            Vector3? setPosition = setPlane.IntersectsRay(terrainNode.ToLocal(mouseRayOrigin), mouseRayNormal);
            if (setPosition.HasValue)
            {
                PrintDebugInfoOnHoverPoint(setPosition.Value);
            }

            return (int)EditorPlugin.AfterGuiInput.Pass;
        }

        int PerformRaycastAndProcessHoverPoint()
        {
            Vector3? drawPosition = null;
            bool drawAreaHovered = false;
            if (HeightDragging && HeightSet)
            {
                var localRayNormal = mouseRayNormal * terrainNode.Transform;
                Plane setPlane = new Plane(new Vector3(localRayNormal.X, 0, localRayNormal.Z),
                    _toolAttributes.DragBasePosition);
                Vector3? setPosition = setPlane.IntersectsRay(terrainNode.ToLocal(mouseRayOrigin), localRayNormal);
                if (setPosition.HasValue)
                {
                    BrushPosition = setPosition.Value;
                }
            }
            // If the pattern is currently not empty and flatten mode ENABLED
            else if (_toolAttributes.Flatten && CurrentDrawPattern.Count > 0)
            {
                chunkPlane = new Plane(Vector3.Up, new Vector3(0, DrawHeight, 0));
                drawPosition = chunkPlane.IntersectsRay(mouseRayOrigin, mouseRayNormal);
                if (drawPosition.HasValue)
                {
                    drawPosition = terrainNode.ToLocal(drawPosition.Value);
                    drawAreaHovered = true;
                }
            }
            else // Perform the raycast to check for intersection with a physics body (terrain)
            {
                _physicsDelegate.QueueRaycast(mouseRayOrigin, mouseRayNormal, camera);
                if (_physicsDelegate.QueuedRayResult is { Count: > 0 } &&
                    _physicsDelegate.QueuedRayResult.TryGetValue("position", out var value))
                {
                    drawPosition = terrainNode.ToLocal(value.AsVector3());
                    drawAreaHovered = true;
                }
                else
                    // Fallback case :
                    //If we didn't hit a chunk, project onto a virtual plane at draw_height
                    // This allows painting onto chunks while the mouse is in "negative space"
                {
                    float fallbackHeight = 0.0f;
                    if (Drawing || HeightDragging || CurrentDrawPattern.Count != 0)
                    {
                        Plane virtualPlane = new Plane(Vector3.Up, new Vector3(0, fallbackHeight, 0));
                        Vector3? planePosition = virtualPlane.IntersectsRay(mouseRayOrigin, mouseRayNormal);
                        if (planePosition.HasValue)
                        {
                            drawPosition = terrainNode.ToLocal(planePosition.Value);
                            drawAreaHovered = true;
                        }
                    }
                }
            }

            //ALT or Right Click to clear the current draw pattern. Don't clear while dragging height
            bool rightClicked = @event is InputEventMouseButton mouseEvent &&
                                mouseEvent.ButtonIndex == MouseButton.Right &&
                                mouseEvent.IsPressed();
            if (!HeightDragging)
                if (rightClicked || Input.IsKeyPressed(Key.Alt))
                    _curDrawPattern.Clear();

            //Check for terrain collisions
            if (drawAreaHovered)
            {
                TerrainHovered = true;
                Vector2I chunkCoords = GetChunkCoords(terrainNode, drawPosition);
                ChunkPlaneHovered = true;
                CurrentHoveredChunk = chunkCoords;
            }

            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left })
            {
                HandleLeftClickEvent(terrainNode, @event, mode, drawAreaHovered, drawPosition, undoRedoManager);
                return (int)EditorPlugin.AfterGuiInput.Stop;
            }

            if (@event is InputEventMouseButton && Input.IsKeyPressed(Key.Shift))
            {
                HandleShiftClickEvent(terrainNode, @event, mode, drawAreaHovered, drawPosition);
            }

            if (drawAreaHovered && @event is InputEventMouseMotion)
            {
                if (drawPosition != null)
                {
                    BrushPosition = drawPosition.Value;
                }

                if (Drawing && mode is TerrainToolMode.Smooth)
                {
                    CommitDrawnPattern(terrainNode);
                    _curDrawPattern.Clear();
                }
            }

            _gizmoPlugin.TriggerRedraw(terrainNode);
            return (int)EditorPlugin.AfterGuiInput.Pass;
        }
    }

    public static string FormatVector2(Vector2D vec)
    {
        var sb = new StringBuilder();
        sb.Append('{').Append($"{vec.X:0.00}").Append(',').Append($"{vec.Y:0.00}").Append('}');
        return sb.ToString();
    }

    public static string FormatVector3(Vector3 vec)
    {
        var sb = new StringBuilder();
        sb.Append('{').Append($"{vec.X:0.00}").Append(',').Append($"{vec.Y:0.00}").Append(',').Append($"{vec.Z:0.00}")
            .Append('}');
        return sb.ToString();
    }

    private static void PrintDebugInfoOnHoverPoint(Vector3 drawPosition)
    {
        if (MarchingTrianglesTerrainPlugin.Instance == null ||
            MarchingTrianglesTerrainPlugin.Instance.PluginHelper == null ||
            MarchingTrianglesTerrainPlugin.Instance.CurTerrainNode == null)
        {
            throw new Exception();
        }

        // DEBUG Statement
        var sb = new StringBuilder();

        sb.Append("Cursor debug :");
        var xzPos = new Vector2D(drawPosition.X, drawPosition.Z);
        sb.Append(FormatVector2(xzPos)).Append(' ');

        var triSystem = TerrainSettings.OrientationSystem;

        var hoveredChk = MarchingTrianglesTerrainPlugin.Instance.PluginHelper.CurrentHoveredChunk;
        sb.Append(" | [Plugin] H. chunk :")
            .Append(hoveredChk);

        var chunkScale = new Vector2D(
                             MarchingTrianglesTerrainPlugin.Instance.CurTerrainNode.TerrainSettings.ChunkDimensions.X,
                             MarchingTrianglesTerrainPlugin.Instance.CurTerrainNode.TerrainSettings.ChunkDimensions.Y) *
                         MarchingTrianglesTerrainPlugin.Instance.CurTerrainNode.TerrainSettings.CellScale;
        var trueTriCell = triSystem.CartesianToLocal.Invoke(xzPos);
        sb.Append(" | [Exp. fr pos] H. cell :")
            .Append(trueTriCell);
        if (MarchingTrianglesTerrainPlugin.Instance.CurTerrainNode
            .Chunks.TryGetValue(
                new Vector2I(
                    Mathf.FloorToInt(trueTriCell.X / chunkScale.X),
                    Mathf.FloorToInt(trueTriCell.Y / chunkScale.Y)),
                out var chk))
        {
            sb.Append(" | [Exp. fr pos] H. chunk : :");
            var chunkIdx = chk.Underlying.Coordinates;
            sb.Append(chunkIdx);
        }
        else
        {
            sb.Append(" | Chunk not created.");
        }

        GD.Print(sb);
        //End Debug statement
    }

    /// <summary>
    /// Commits the action planed by the drawn pattern to Godot's undo/redo manager.
    /// </summary>
    /// <param name="node"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void CommitDrawnPattern(Node3D node)
    {
        if (MarchingTrianglesTerrainPlugin.Instance == null)
        {
            return;
        }

        var undoRedoManager = MarchingTrianglesTerrainPlugin.Instance.GetUndoRedo();
        // WARNING : We're relying on godot's dictionaries from now on
        // This is due to the fact that the Undo-Redo manager needs Variant compatible types (Godot's dictionary is one)
        var pattern = new Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Variant>>();
        var patternCellCoords =
            new Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Vector2I>>();
        var restorePattern =
            new Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Variant>>();
        var restorePatternCellCoords =
            new Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Vector2I>>();
        
        
        if (node is MarchingTrianglesTerrain terrain)
        {
            // Process the pattern entries
            foreach (var chunkCoord in CurrentDrawPattern.Keys)
            {
                pattern[chunkCoord] = new();
                patternCellCoords[chunkCoord] = new();
                restorePattern[chunkCoord] = new();
                restorePatternCellCoords[chunkCoord] = new();

                var hasData = _curDrawPattern.TryGetValue(chunkCoord, out var chunkDataDrawn);
                if (hasData && chunkDataDrawn != null)
                {
                    var chunk = terrain.Chunks[chunkCoord];
                    foreach (var drawCellCoords in chunkDataDrawn.Keys)
                    {
                        var cellCoord = new Vector2I(drawCellCoords.X, drawCellCoords.Y);
                        var polyIdx = drawCellCoords.Z;
                        var pos = chunk.Underlying.DataGrid.OrientationSystem.GetCellCentroid(cellCoord, polyIdx);

                        float sample = Mathf.Clamp(chunkDataDrawn[drawCellCoords], 0.001f, 0.999f); // why not 0 - 1 ?
                        Variant? drawValue = 0f;
                        Variant? restoreValue = 0f;

                        switch (_parent.SelectedMode)
                        {
                            case TerrainToolMode.Level:
                                restoreValue = chunk.Underlying.GetHeightFromCartesianCoords(pos);
                                drawValue = Mathf.Lerp(restoreValue.Value.AsSingle(), DrawHeight, sample);
                                break;
                            case TerrainToolMode.Smooth:
                            case TerrainToolMode.Bridge:
                            case TerrainToolMode.DebugBrush:

                                break; // TODO
                            case TerrainToolMode.Brush:
                                restoreValue = chunk.Underlying.GetHeightFromTriCellCoords(drawCellCoords);
                                if (_parent.ToolAttributes.Flatten)
                                {
                                    drawValue = Mathf.Lerp(restoreValue.Value.AsSingle(), BrushPosition.Y, sample);
                                }
                                else
                                {
                                    float heightDiff = BrushPosition.Y - DrawHeight;
                                    drawValue = Mathf.Lerp(restoreValue.Value.AsSingle(),
                                        restoreValue.Value.AsSingle() + heightDiff,
                                        sample);
                                }

                                break;
                            case TerrainToolMode.ChunkManagement:
                            case TerrainToolMode.TerrainSettings:
                                break;
                            case TerrainToolMode.GeometryEdit:
                                var cell = chunk.Underlying.TerrainDualGrid.CompleteCells
                                    .FirstOrDefault(c =>
                                        c.Visits.ContainsKey(new Vector3I(cellCoord.X, cellCoord.Y, 0)));
                                if (cell is null)
                                {
                                    restoreValue = null;
                                    drawValue = null;
                                    break;
                                }

                                var cellValue = cell.GeometryModesOverride;
                                restoreValue = cellValue is not null
                                    ? new Vector2I((int)cellValue.Item1, (int)cellValue.Item2)
                                    : new Vector2I((int)chunk.Underlying.DefaultGeometryModes!.Item1,
                                        (int)chunk.Underlying.DefaultGeometryModes!.Item2);
                                drawValue = _parent.ToolAttributes.GeometryModes;
                                break;
                            default:
                                throw new NotSupportedException($"Behavior not implemented for {_parent.SelectedMode}");
                        }

                        if (restoreValue != null && drawValue != null)
                        {
                            restorePattern[chunkCoord][drawCellCoords] = restoreValue.Value;
                            pattern[chunkCoord][drawCellCoords] = drawValue.Value;
                        }
                    }
                }
            }

            bool isQuickPaint = false; //TODO Not supported
            var doPatternVariant =
                new Godot.Collections.Dictionary<string, Godot.Collections.Dictionary<Vector2I,
                    Godot.Collections.Dictionary<Vector3I, Variant>>>();
            var undoPatternVariant =
                new Godot.Collections.Dictionary<string, Godot.Collections.Dictionary<Vector2I,
                    Godot.Collections.Dictionary<Vector3I, Variant>>>();

            if (isQuickPaint)
            {
                ProcessQuickPaintBrushPattern(
                    node,
                    pattern, restorePattern, out var doPattern, out var undoPattern);
                doPatternVariant = doPattern;
                undoPatternVariant = undoPattern;
            }
            else
            {
                ProcessBrushPattern(
                    _parent.SelectedMode,
                    pattern,
                    restorePattern,
                    out var doPattern,
                    out var undoPattern);
                doPatternVariant = doPattern;
                undoPatternVariant = undoPattern;
            }

            // Use delegate method since "this" helper is not a Godot object (but the plugin itself is)
            undoRedoManager.CreateAction("Terrain height draw" + (isQuickPaint ? " with quick paint brush" : ""));
            undoRedoManager.AddDoMethod(_parent, nameof(
                    MarchingTrianglesTerrainPlugin.DelegateCompositePatternAction),
                terrain,
                doPatternVariant);
            undoRedoManager.AddUndoMethod(_parent, nameof(
                    MarchingTrianglesTerrainPlugin.DelegateCompositePatternAction),
                terrain,
                undoPatternVariant);
            undoRedoManager.CommitAction();
        }
    }


    private void ProcessQuickPaintBrushPattern(
        Node3D node,
        Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Variant>> pattern,
        Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Variant>> restorePattern,
        out Godot.Collections.Dictionary<
            string,
            Godot.Collections.Dictionary<
                Vector2I,
                Godot.Collections.Dictionary<
                    Vector3I,
                    Variant>>> doPattern,
        out Godot.Collections.Dictionary<
            string,
            Godot.Collections.Dictionary<
                Vector2I,
                Godot.Collections.Dictionary<
                    Vector3I,
                    Variant>>> undoPattern)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// NON-QUICK PAINT MODE: Apply height + default wall texture
    /// Use the terrain's default_wall_texture for wall colors
    /// </summary>
    private void ProcessBrushPattern(
        TerrainToolMode operationMode,
        Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Variant>> pattern,
        Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Variant>> restorePattern,
        out Godot.Collections.Dictionary<
            string,
            Godot.Collections.Dictionary<
                Vector2I,
                Godot.Collections.Dictionary<
                    Vector3I,
                    Variant>>> doPattern,
        out Godot.Collections.Dictionary<
            string,
            Godot.Collections.Dictionary<
                Vector2I,
                Godot.Collections.Dictionary<
                    Vector3I,
                    Variant>>> undoPattern)
    {
        doPattern = new();
        undoPattern = new();

        switch (operationMode)
        {
            case TerrainToolMode.GeometryEdit:
                doPattern["geometryMode"] = pattern;
                undoPattern["geometryMode"] = restorePattern;
                break;
            default:
                doPattern["height"] = pattern;
                undoPattern["height"] = restorePattern;
                break;
        }
    }

    public void ApplyCompositePatternAction(MarchingTrianglesTerrain terrain,
        Godot.Collections.Dictionary<
            string,
            Godot.Collections.Dictionary<
                Vector2I,
                Godot.Collections.Dictionary<
                    Vector3I,
                    Variant>>> patternActionData)
    {
        var affectedChunks = new Dictionary<Vector2I, GdPluginHexTerrainChunk>();

        var compositeDisabled = false;
        if (_parent.SelectedMode == TerrainToolMode.Smooth)
        {
            compositeDisabled = true;
        }


        // map each string to a method and a type in an Ordered dico
        OrderedDictionary<string, Action<TerrainColorMaps, Vector3I, Variant>> actions = new()
        {
            ["geometryMode"] = (input, v, data) => input.DrawNewGeometryMode(v, data.AsVector2I()),
            // Apply wall colors before height changes that can create ridge vertices
            ["wall_color_0"] = (input, v, data) => input.DrawWallColor0(v, data.AsColor()),
            ["wall_color_1"] = (input, v, data) => input.DrawWallColor1(v, data.AsColor()),
            ["height"] = (input, v, data) => input.DrawHeight(v, data.AsSingle()),
            //Apply ground colors LAST
            ["color_0"] = (input, v, data) => input.DrawGroundColor0(v, data.AsColor()),
            ["color_1"] = (input, v, data) => input.DrawGroundColor1(v, data.AsColor())
        };

        foreach (var stringActionPair in actions)
        {
            if (!compositeDisabled && patternActionData.TryGetValue(stringActionPair.Key, out var colorData))
            {
                foreach (var kvp in colorData)
                {
                    terrain.Chunks.TryGetValue(kvp.Key, out var chunk);
                    if (chunk != null)
                    {
                        affectedChunks[chunk.Underlying.Coordinates] = chunk;
                        foreach (var data in colorData[chunk.Underlying.Coordinates])
                        {
                            stringActionPair.Value.Invoke(
                                chunk.Underlying.ColorMaps ??
                                throw new Exception(
                                    "Cannot apply change to underlying data as the data container is null"),
                                data.Key,
                                data.Value);
                        }
                    }
                }
            }
        }
        //TODO : Re-freeze the cell;

        //Regenerate mesh ONCE for each affected chunk
        foreach (var hexTerrainChunk in affectedChunks.Values)
        {
            if (hexTerrainChunk.GetActiveMaterial(0) is ShaderMaterial mat)
            {
                hexTerrainChunk.GenerateTerrainMesh(false, mat.Shader.ResourcePath);
            }
            else
            {
                hexTerrainChunk.GenerateTerrainMesh(false);
            }
        }
    }

    private Vector2I GetChunkCoords(Node3D terrainNode, Vector3? drawPosition)
    {
        if (terrainNode is MarchingTrianglesTerrain terrain && drawPosition.HasValue)
        {
            Vector2I? val =
                MarchingTrianglesTerrain.GetChunkCoordsFromCartesian(
                    new Vector2D(drawPosition.Value.X, drawPosition.Value.Z),
                    terrain.TerrainSettings.ChunkDimensions);
            return val.Value;
        }

        throw new System.NotImplementedException();
    }
}