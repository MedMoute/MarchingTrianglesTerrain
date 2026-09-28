using System;
using System.Collections.Generic;
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

    private GdPluginHexTerrainChunk _currentSelectedChunk;

    public GdPluginHexTerrainChunk CurrentSelectedChunk
    {
        get => _currentSelectedChunk;
        set
        {
            _currentSelectedChunk = value;
            _toolAttributes.SelectedChunk = value.Underlying.Coordinates;
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
                        redoManager?.CreateAction("Remove Chunk");
                        redoManager.AddDoMethod(terrain, MarchingTrianglesTerrain.MethodName.RemoveChunkFromTree,
                            CurrentHoveredChunk, _parent);
                        redoManager.AddUndoMethod(terrain, MarchingTrianglesTerrain.MethodName.AddChunk,
                            CurrentHoveredChunk, _parent);
                        redoManager.CommitAction();
                    }
                    else if (terrain.CanAddEmptyChunk(CurrentHoveredChunk))
                    {
                        redoManager?.CreateAction("Add chunk");
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
                if (terrainToolMode is TerrainToolMode.VertexPainting or TerrainToolMode.DebugBrush
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
                    BrushPosition = drawPosition.Value;
                }
                else
                {
                    HeightDragging = true;
                    if (!_toolAttributes.Flatten)
                    {
                        DrawHeight = drawPosition.Value.Y;
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
                if (terrainToolMode is TerrainToolMode.Level || terrainToolMode is TerrainToolMode.Bridge ||
                    terrainToolMode is TerrainToolMode.DebugBrush)
                {
                    // Draw the complete pattern before selection 
                    CommitDrawnPattern(node3D);
                    _curDrawPattern.Clear();
                }

                if (terrainToolMode is TerrainToolMode.VertexPainting || terrainToolMode is TerrainToolMode.Smooth)
                {
                    //Smoothing or Vertex painting dont need to draw on release since the job is already done
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
    private void HandleShiftClickEvent(Node3D node3D, InputEvent inputEvent, TerrainToolMode terrainToolMode,
        bool drawAreaHovered,
        Vector3? drawPosition)
    {
        // TerrainPlugin.gd => ll.431 - 447
        throw new NotImplementedException();
    }

    public int HandleMouseEvent(Camera3D camera,
        Node3D terrainNode,
        InputEvent @event,
        TerrainToolMode mode,
        EditorUndoRedoManager undoRedoManager)
    {
        TerrainHovered = false;

        Vector2 mousePosition = camera.GetViewport().GetMousePosition();

        Vector3 mouseRayOrigin = camera.ProjectRayOrigin(mousePosition);
        Vector3 mouseRayNormal = camera.ProjectRayNormal(mousePosition);

        Plane chunkPlane;

        if (mode is TerrainToolMode.DebugBrush)
        {
            Plane setPlane = new Plane(Vector3.Up,
                Vector3.Zero);
            Vector3? setPosition = setPlane.IntersectsRay(terrainNode.ToLocal(mouseRayOrigin), mouseRayNormal);
            if (setPosition.HasValue)
            {
                PrintDebugInfoOnHoverPoint(setPosition.Value);
            }
        }

        // If not in settings menu, perform terrain raycast
        if (mode is not (TerrainToolMode.TerrainSettings or TerrainToolMode.ChunkManagement))
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
                BrushPosition = drawPosition.Value;
                if (Drawing && mode is TerrainToolMode.Smooth or TerrainToolMode.VertexPainting)
                {
                    CommitDrawnPattern(terrainNode);
                    _curDrawPattern.Clear();
                }
            }

            _gizmoPlugin.TriggerRedraw(terrainNode);
            return (int)EditorPlugin.AfterGuiInput.Pass;
        }

        // In chunk settings/creation mode : 
        // Check for hovering over/clicking a new chunk

        chunkPlane = new Plane(Vector3.Up, Vector3.Zero);
        Vector3? intersection = chunkPlane.IntersectsRay(mouseRayOrigin, mouseRayNormal);

        if (intersection.HasValue)
        {
            Vector2I? chunksCoords =
                MarchingTrianglesTerrain.GetChunkCoordsFromCartesian(
                    new Vector2D(intersection.Value.X, intersection.Value.Z),
                    (terrainNode as MarchingTrianglesTerrain)!.TerrainSettings.ChunkDimensions);

            if (chunksCoords.HasValue)
            {
                if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left })
                {
                    HandleLeftClickEvent(terrainNode, @event, mode, false, intersection, undoRedoManager);
                }

                if (CurrentHoveredChunk != chunksCoords.Value)
                {
                    CurrentHoveredChunk = chunksCoords.Value;
                    ChunkPlaneHovered = true;
                    _gizmoPlugin.TriggerRedraw(terrainNode);
                }
            }
        }
        else
        {
            ChunkPlaneHovered = false;
        }

        // Consume clicks but allow other click / mouse motion types to reach the gui, for camera movement, etc
        // => why here but not on the other  ?
        if (@event is InputEventMouseButton _mouseEvent && @event.IsPressed() &&
            _mouseEvent.ButtonIndex == MouseButton.Left)
            return (int)EditorPlugin.AfterGuiInput.Stop;

        return (int)EditorPlugin.AfterGuiInput.Pass;
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

    protected static void PrintDebugInfoOnHoverPoint(Vector3 drawPosition)
    {
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
    /// <param name="terrainNode"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void CommitDrawnPattern(Node3D terrainNode)
    {
        var undoRedoManager = MarchingTrianglesTerrainPlugin.Instance.GetUndoRedo();
        // WARNING : We're relying on godot's dictionaries from now on as the Undo-Redo manager needs
        // Variant compatible types (Godot's dico is one)
        var pattern = new Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Variant>>();
        var patternCellCoords =
            new Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Vector2I>>();
        var restorePattern =
            new Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Variant>>();
        var restorePatternCellCoords =
            new Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Vector2I>>();

        Vector2I firstChunkCoords = _parent.CurTerrainNode.GetTempChunkCoords();
        // Process the pattern entries
        foreach (var chunkCoord in CurrentDrawPattern.Keys)
        {
            if (firstChunkCoords == _parent.CurTerrainNode.GetTempChunkCoords())
            {
                firstChunkCoords = chunkCoord;
            }

            pattern[chunkCoord] = new();
            patternCellCoords[chunkCoord] = new();
            restorePattern[chunkCoord] = new();
            restorePatternCellCoords[chunkCoord] = new();

            var hasData = _curDrawPattern.TryGetValue(chunkCoord, out var chunkDataDrawn);
            if (hasData)
            {
                var chunk = (terrainNode as MarchingTrianglesTerrain)?.Chunks[chunkCoord];
                foreach (var drawCellCoords in chunkDataDrawn.Keys)
                {
                    var cellCoord = new Vector2I(drawCellCoords.X, drawCellCoords.Y);
                    var polyIdx = drawCellCoords.Z;
                    var pos = chunk.Underlying.DataGrid.OrientationSystem.GetCellCentroid(cellCoord, polyIdx);

                    float sample = Mathf.Clamp(chunkDataDrawn[drawCellCoords], 0.001f, 0.999f); // why not 0 - 1 ?
                    float drawValue = 0f;
                    float restoreValue = 0f;
                    Vector2I drawValueCC = Vector2I.Zero;
                    Vector2I restoreValueCC = Vector2I.Zero;

                    switch (_parent.SelectedMode)
                    {
                        case TerrainToolMode.Level:
                            restoreValue = chunk.Underlying.GetHeightFromCartesianCoords(pos);
                            drawValue = Mathf.Lerp(restoreValue, DrawHeight, sample);
                            break;
                        case TerrainToolMode.GrassMask:
                        case TerrainToolMode.Smooth:
                        case TerrainToolMode.Bridge:
                        case TerrainToolMode.VertexPainting:
                        case TerrainToolMode.DebugBrush:
                            break; // TODO
                        default:
                            restoreValue = chunk.Underlying.GetHeightFromTriCellCoords(drawCellCoords);
                            if (_parent.ToolAttributes.Flatten)
                            {
                                drawValue = Mathf.Lerp(restoreValue, BrushPosition.Y, sample);
                            }
                            else
                            {
                                float heightDiff = BrushPosition.Y - DrawHeight;
                                drawValue = Mathf.Lerp(restoreValue, restoreValue + heightDiff, sample);
                            }

                            break;
                    }

                    restorePattern[chunkCoord][drawCellCoords] = restoreValue;
                    pattern[chunkCoord][drawCellCoords] = drawValue;
                    if (_parent.SelectedMode == TerrainToolMode.VertexPainting)
                    {
                        restorePatternCellCoords[chunkCoord][drawCellCoords] = restoreValueCC;
                        patternCellCoords[chunkCoord][drawCellCoords] = drawValueCC;
                    }
                }
            }
        }

        //
        switch (_parent.SelectedMode)
        {
            case TerrainToolMode.VertexPainting:
            case TerrainToolMode.GrassMask:
                throw new NotImplementedException();
            default: // Brush-based mode
                bool isQuickPaint = _parent.ToolAttributes.CurrentQuickPaint != null;
                var doPatternVariant =
                    new Godot.Collections.Dictionary<string, Godot.Collections.Dictionary<Vector2I,
                        Godot.Collections.Dictionary<Vector3I, Variant>>>();
                var undoPatternVariant =
                    new Godot.Collections.Dictionary<string, Godot.Collections.Dictionary<Vector2I,
                        Godot.Collections.Dictionary<Vector3I, Variant>>>();

                if (isQuickPaint)
                {
                    ProcessQuickPaintBrushPattern(
                        terrainNode as MarchingTrianglesTerrain,
                        pattern, restorePattern, out var doPattern, out var undoPattern);
                    doPatternVariant = doPattern;
                    undoPatternVariant = undoPattern;
                }
                else
                {
                    ProcessBrushPattern(
                        terrainNode as MarchingTrianglesTerrain,
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
                    terrainNode as MarchingTrianglesTerrain,
                    doPatternVariant);
                undoRedoManager.AddUndoMethod(_parent, nameof(
                        MarchingTrianglesTerrainPlugin.DelegateCompositePatternAction),
                    terrainNode as MarchingTrianglesTerrain,
                    undoPatternVariant);
                undoRedoManager.CommitAction();
                break;
        }
    }

    /// <summary>
    /// -- DEPRECATED --
    /// Duplicates the data already available in the neighbors' entries
    /// that is bordering the provided chunk to the current chunk's entry.
    /// If there is no entries, just go on.
    /// </summary>
    /// <param name="chunk"></param>
    /// <param name="pattern"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void DuplicateNeighborChunksBorderData(HexagonalTerrainChunk chunk,
        Godot.Collections.Dictionary<Vector2I, Godot.Collections.Dictionary<Vector3I, Variant>> pattern)
    {
        HashSet<Vector2I> neighborsChunks =
        [
            chunk.Coordinates + Vector2I.Up,
            chunk.Coordinates + Vector2I.Up + Vector2I.Left,
            chunk.Coordinates + Vector2I.Up + Vector2I.Right,
            chunk.Coordinates + Vector2I.Down,
            chunk.Coordinates + Vector2I.Down + Vector2I.Left,
            chunk.Coordinates + Vector2I.Down + Vector2I.Right,
            chunk.Coordinates + Vector2I.Left,
            chunk.Coordinates + Vector2I.Right
        ];
        foreach (var neighbor in neighborsChunks)
        {
            if (pattern.TryGetValue(neighbor, out var neighborDataDic))
            {
                var offset = (neighbor - chunk.Coordinates) * _parent.CurTerrainNode.TerrainSettings.ChunkDimensions;
                var offset3D = new Vector3I(offset.X, offset.Y, 0);

                var triCells = chunk.GetTriCellsTouchingNeighbour(neighbor);
                foreach (var triCell in triCells)
                {
                    if (neighborDataDic.TryGetValue(triCell - offset3D, out var newCellData))
                    {
                        pattern[chunk.Coordinates][triCell] = newCellData;
                    }
                }
            }
        }
    }

    private void ProcessQuickPaintBrushPattern(
        MarchingTrianglesTerrain terrainNode,
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
    /// <param name="pattern"></param>
    /// <param name="restorePattern"></param>
    private void ProcessBrushPattern(
        MarchingTrianglesTerrain terrainNode,
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
        //TODO wall Coloring
        doPattern = new();
        undoPattern = new();

        doPattern["height"] = pattern;
        undoPattern["height"] = restorePattern;
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
        if (_parent.SelectedMode == TerrainToolMode.Smooth && _parent.ToolAttributes.CurrentQuickPaint == null)
        {
            compositeDisabled = true;
        }
        

        // map each string to a method and a type in an Ordered dico
        OrderedDictionary<string, Action<TerrainColorMaps, Vector3I, Variant>> actions = new();
        // Apply wall colors before height changes that can create ridge vertices
        actions["wall_color_0"] = (input, v, data) => input.DrawWallColor0(v, data.AsColor());
        actions["wall_color_1"] = (input, v, data) => input.DrawWallColor1(v, data.AsColor());
        actions["height"] = (input, v, data) => input.DrawHeight(v, data.AsSingle());
        //Apply ground colors LAST
        actions["color_0"] = (input, v, data) => input.DrawGroundColor0(v, data.AsColor());
        actions["color_1"] = (input, v, data) => input.DrawGroundColor1(v, data.AsColor());

        foreach (var stringActionPair in actions)
        {
            if (!compositeDisabled && patternActionData.TryGetValue(stringActionPair.Key, out var colorData))
            {
                foreach (var kvp in colorData)
                {
                    terrain.Chunks.TryGetValue(kvp.Key, out GdPluginHexTerrainChunk chunk);
                    if (chunk != null)
                    {
                        affectedChunks[chunk.Underlying.Coordinates] = chunk;
                        foreach (var data in colorData[chunk.Underlying.Coordinates])
                        {
                            stringActionPair.Value.Invoke(chunk.Underlying.ColorMaps, data.Key, data.Value);
                        }
                    }
                }
            }
        }
        //TODO : Re-freeze the cell;

        //Regenerate mesh ONCE for each affected chunk
        foreach (var hexTerrainChunk in affectedChunks.Values)
        {
            hexTerrainChunk.GenerateTerrain(false);
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