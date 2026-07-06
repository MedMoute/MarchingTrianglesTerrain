using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.ui;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.editor.gizmo;

public partial class MarchingTrianglesTerrainGizmo : EditorNode3DGizmo
{
    /// <summary>
    /// Flag for debug print.
    /// </summary>
    private bool _verbose = false;

    private readonly List<Vector3> _lines = new();

    private Material BrushMaterial => GetPlugin().GetMaterial(nameof(MarchingTrianglesGizmoPlugin.BrushMesh));

    private Dictionary<StringName, Material> _chunkActionMaterials = new();

    private readonly MarchingTrianglesTerrainPlugin _terrainPlugin = MarchingTrianglesTerrainPlugin.Instance;

    public MarchingTrianglesTerrainGizmo()
    {
        CallDeferred(nameof(BuildMaterials));
    }

    public bool BuildMaterials()
    {
        if (GetPlugin() == null)
        {
            return false;
        }

        _chunkActionMaterials = new Dictionary<StringName, Material>
        {
            {
                nameof(MarchingTrianglesTerrain.RemoveChunk),
                GetPlugin().GetMaterial(nameof(MarchingTrianglesTerrain.RemoveChunk), this)
            },
            {
                nameof(MarchingTrianglesTerrain.AddChunk),
                GetPlugin().GetMaterial(nameof(MarchingTrianglesTerrain.AddChunk), this)
            },
            {
                nameof(MarchingTrianglesGizmoPlugin.BrushMesh),
                GetPlugin().GetMaterial(nameof(MarchingTrianglesGizmoPlugin.BrushMesh), this)
            },
            {
                nameof(MarchingTrianglesGizmoPlugin.HighlightColor),
                GetPlugin().GetMaterial(nameof(MarchingTrianglesGizmoPlugin.HighlightColor), this)
            }
        };
        return _chunkActionMaterials.Count != 0;
    }

    private Material FetchMaterial(StringName name)
    {
        var material = _chunkActionMaterials.GetValueOrDefault(name, null);
        if (material == null)
        {
            if (GetPlugin() == null)
            {
                GD.PushError("Gizmo plugin not available during gizmo _Redraw(). Aborting the draw.");
                return null;
            }

            material = GetPlugin().GetMaterial(name);
            if (material == null)
            {
                GD.PushError("Material not available");
                return null;
            }
        }

        return material;
    }


    public override void _Redraw()
    {
        _lines.Clear();
        Clear();
        var sb = new StringBuilder();
        sb.Append("[Gizmo Redraw Debug]");

        var terrain = _terrainPlugin.CurTerrainNode;

        if (_terrainPlugin.SelectedMode == TerrainToolMode.ChunkManagement)
        {
            ProcessGizmoChunkLines(sb, terrain);
        }

        var pos = ProcessBrushAndPattern(terrain, sb);

        //The size of the brush cell mesh is adjusted dynamically before drawing :
        if (MarchingTrianglesGizmoPlugin.BrushMesh != null && _terrainPlugin.CurTerrainNode!=null)
        {
            MarchingTrianglesGizmoPlugin.BrushMesh.Size =
                Vector2.One * _terrainPlugin.CurTerrainNode.TerrainSettings.CellScale / 2;
        }

        if (_terrainPlugin.PluginHelper.TerrainHovered)
        {
            DrawBrush(pos, terrain, sb);
        }

        var patternDrawCalls = DrawPattern(terrain);
        // Debug statements
        if (_verbose)
        {
            AddDebugStatementAboutDrawnPattern(patternDrawCalls, sb);
            GD.Print(sb.ToString());
        }

    }

    private static void AddDebugStatementAboutDrawnPattern(Dictionary<Vector2I, int> patternDrawCalls, StringBuilder sb)
    {
        if (patternDrawCalls.Sum(d => d.Value) > 0)
        {
            sb.Append(" | Pattern drawn over ")
                .Append(patternDrawCalls.Count)
                .Append(" chunks | Drawn ")
                .Append(patternDrawCalls.Sum(d => d.Value)).Append(" cells for the pattern");
        }
        else
        {
            sb.Append(" | Drawn Empty pattern ");
        }
    }

    /// <summary>
    /// Method responsible for drawing the Gizmo lines representing the chunks 
    /// </summary>
    /// <param name="sb">debug string</param>
    /// <param name="terrain">the terrain edited via the gizmo</param>
    private void ProcessGizmoChunkLines(StringBuilder sb, MarchingTrianglesTerrain terrain)
    {
        Material addChunkMat = FetchMaterial(nameof(MarchingTrianglesTerrain.AddChunk));
        Material removeChunkMat = FetchMaterial(nameof(MarchingTrianglesTerrain.RemoveChunk));
        Material highlightChunkMat = FetchMaterial(nameof(MarchingTrianglesGizmoPlugin.HighlightColor));


        if (terrain == null ||
            EditorInterface.Singleton.GetSelection().GetSelectedNodes().Count != 1 ||
            EditorInterface.Singleton.GetSelection().GetSelectedNodes()[0] != terrain)
        {
            // DEBUG Statement
            //TODO
            GD.PushError(
                "Either no terrain to draw on OR more than one selected node OR selected node not being the terrain. Aborting the gizmo draw.");
        }

        // Draw the selected chunk's boundaries as Gizmo lines
        if (_terrainPlugin.PluginHelper.CurrentSelectedChunk != null)
        {
            var pluginSelectedChunkCoords = _terrainPlugin.PluginHelper.CurrentSelectedChunk.Underlying.Coordinates;
            var selectedChunkFoundByName =
                _terrainPlugin.CurTerrainNode.FindChild("Chunk " + pluginSelectedChunkCoords);
            if (selectedChunkFoundByName != null &&
                selectedChunkFoundByName == _terrainPlugin.PluginHelper.CurrentSelectedChunk)
            {
                AddChunkLines(
                    terrain,
                    pluginSelectedChunkCoords,
                    highlightChunkMat);
            }
        }

        // Draw the hovered chunk's boundaries as Gizmo lines when there is no other chunk
        if (terrain.Chunks.Count == 0)
        {
            if (_verbose) sb.Append("- Hover active ? " + _terrainPlugin.PluginHelper.ChunkPlaneHovered);
            if (_verbose && _terrainPlugin.PluginHelper.ChunkPlaneHovered)
                sb.Append("- Hovered Chunk #" + _terrainPlugin.PluginHelper.CurrentHoveredChunk);

            if (_terrainPlugin.PluginHelper.ChunkPlaneHovered)
            {
                AddChunkLines(
                    terrain,
                    _terrainPlugin.PluginHelper.CurrentHoveredChunk,
                    addChunkMat);
            }
        }
        else
        {
            // If the hovered chunk exist, we can delete it
            if (_terrainPlugin.PluginHelper.ChunkPlaneHovered
                && terrain.Chunks.ContainsKey(_terrainPlugin.PluginHelper.CurrentHoveredChunk))
            {
                AddChunkLines(
                    _terrainPlugin.CurTerrainNode,
                    _terrainPlugin.PluginHelper.CurrentHoveredChunk,
                    removeChunkMat);
            }
            // Otherwise we process the neighbor coordinates of the hovered chunk, it there is a chunk, we can add it
            else
            {
                var success = ProcessNeighborChunksForGizmoLines(_terrainPlugin.PluginHelper.CurrentHoveredChunk);
                if (success)
                {
                    AddChunkLines(
                        _terrainPlugin.CurTerrainNode,
                        _terrainPlugin.PluginHelper.CurrentHoveredChunk,
                        addChunkMat);
                }
            }
        }
    }

    private Vector3 ProcessBrushAndPattern(MarchingTrianglesTerrain terrain, StringBuilder sb)
    {
        // Brush & brush pattern processing
        Vector3 pos = _terrainPlugin.PluginHelper.BrushPosition;
        var cursorChunkCoords = new Vector2I();
        var cursorCellCoords = new Vector3I();

        if (_terrainPlugin.PluginHelper.HeightDragging && !_terrainPlugin.PluginHelper.HeightSet)
        {
            _terrainPlugin.PluginHelper.HeightSet = true;

            var pos2D = new Vector2D(pos.X, pos.Z);
            cursorChunkCoords = MarchingTrianglesTerrain.GetChunkCoordsFromCartesian(
                pos2D, terrain.TerrainSettings.ChunkDimensions);

            var chunkExists = terrain.Chunks.TryGetValue(cursorChunkCoords, out var chunk);
            if (!chunkExists) // Early exit
            {
                return pos;
            }

            var tmpCursorCellCoords = chunk.Underlying.DataGrid.OrientationSystem.GetCell(new Vector2D(pos.X, pos.Z));
            int polygonIdx = chunk.Underlying.DataGrid.OrientationSystem.GetPolygonIndexFromCartesian(
                pos2D,
                new Vector2I(tmpCursorCellCoords.X, tmpCursorCellCoords.Y));

            cursorCellCoords.X = tmpCursorCellCoords.X;
            cursorCellCoords.Y = tmpCursorCellCoords.Y;
            cursorCellCoords.Z = polygonIdx;

            //When dragging the height, if there is no pattern and alt not held, go to draw mode at the cursor position
            bool hasPattern = _terrainPlugin.PluginHelper.CurrentDrawPattern.Count > 0;
            if (!hasPattern && !Input.IsKeyPressed(Key.Alt))
            {
                _terrainPlugin.PluginHelper.CurrentDrawPattern.Clear();
                _terrainPlugin.PluginHelper.HeightDragging = false;
                _terrainPlugin.PluginHelper.Drawing = true;
                _terrainPlugin.PluginHelper.DrawHeight = pos.Y;
            }
            // Else drag the pattern height
            else
            {
                if (Input.IsKeyPressed(Key.Alt))
                {
                    // If Alt is held, only drag the currently selected cell 
                    _terrainPlugin.PluginHelper.CurrentDrawPattern.Clear();
                    _terrainPlugin.PluginHelper.CurrentDrawPattern[cursorChunkCoords] =
                        new Dictionary<Vector3I, float>();
                    var heightValue = chunk.Underlying.DataGrid.Data[cursorCellCoords];
                    _terrainPlugin.PluginHelper.CurrentDrawPattern[cursorChunkCoords][cursorCellCoords] = heightValue;
                    _terrainPlugin.PluginHelper.DrawHeight = heightValue;
                }

                _terrainPlugin.ToolAttributes.DragBasePosition = pos;
            }
        }

        if (_terrainPlugin.PluginHelper.Drawing && !_terrainPlugin.PluginHelper.HeightSet)
        {
            _terrainPlugin.PluginHelper.DrawHeight = pos.Y;
            _terrainPlugin.PluginHelper.HeightSet = true;
        }

        return pos;
    }

    private Dictionary<Vector2I, int> DrawPattern(MarchingTrianglesTerrain terrain)
    {
        Dictionary<Vector2I, int> res = new();
        // Check if we're in wall painting mode
        var isWallPainting = _terrainPlugin.PluginHelper.WallPainting &&
                             _terrainPlugin.SelectedMode == TerrainToolMode.VertexPainting;
        Material brushMat = FetchMaterial(nameof(MarchingTrianglesGizmoPlugin.BrushMesh));
        float heightDiff = 0;
        if (_terrainPlugin.PluginHelper.HeightDragging && _terrainPlugin.PluginHelper.HeightSet)
        {
            heightDiff = _terrainPlugin.PluginHelper.BrushPosition.Y - _terrainPlugin.PluginHelper.DrawHeight;
        }

        //Draw the pattern
        if (_terrainPlugin.PluginHelper.CurrentDrawPattern.Count > 0)
        {
            foreach (var chunkPatterns in _terrainPlugin.PluginHelper.CurrentDrawPattern)
            {
                int drawnCellCount = 0;

                GdPluginHexTerrainChunk chunk = terrain.Chunks[chunkPatterns.Key];
                var drawChunkData = chunkPatterns.Value;

                foreach (var cellsData in drawChunkData)
                {
                    var cellTriPos = chunk.Underlying.DataGrid.OrientationSystem.GetCellCentroid(cellsData.Key);
                    var success = chunk.Underlying.DataGrid.Data.TryGetValue(cellsData.Key, out var yVal);
                    if (!success)
                    {
                        break;
                    }

                    var sample = drawChunkData[cellsData.Key];

                    // if height dragging, also show a square at the height currently set (hence the + heighdiff)
                    Vector3 drawPos1 = new Vector3(
                        (float)cellTriPos.X,
                        yVal + heightDiff,
                        (float)cellTriPos.Y);
                    var drawTransform = new Transform3D(
                        Vector3.Right * sample,
                        Vector3.Up * sample,
                        Vector3.Back * sample, drawPos1);
                    if (!isWallPainting)
                    {
                        AddMesh(MarchingTrianglesGizmoPlugin.BrushMesh, brushMat, drawTransform);
                    }
                }

                res[chunkPatterns.Key] = drawnCellCount;
            }
        }

        return res;
    }

    private void DrawBrush(Vector3 pos, MarchingTrianglesTerrain terrain,
        StringBuilder sb)
    {
        // Check if we're in wall painting mode
        var isWallPainting = _terrainPlugin.PluginHelper.WallPainting &&
                             _terrainPlugin.SelectedMode == TerrainToolMode.VertexPainting;

        Material brushMat = FetchMaterial(nameof(MarchingTrianglesGizmoPlugin.BrushMesh));

        // Step 1 : Visualization of the brush radius
        var brushTransform = new Transform3D(
            Vector3.Right * (float)_terrainPlugin.ToolAttributes.BrushSize,
            Vector3.Up,
            Vector3.Back * (float)_terrainPlugin.ToolAttributes.BrushSize, pos);

        if (isWallPainting)
        {
            //Try to determine the 3D world position of the intersection the mouse's ray 
            var viewport = EditorInterface.Singleton.GetEditorViewport3D();
            var editorCamera = viewport.GetCamera3D();
            Vector2 mousePosition = viewport.GetMousePosition();

            Vector3 mouseRayOrigin = editorCamera.ProjectRayOrigin(mousePosition);
            Vector3 mouseRayNormal = editorCamera.ProjectRayNormal(mousePosition);

            var space = editorCamera.GetWorld3D().DirectSpaceState;

            var rayLength = 10_000f;
            var endOOfRay = mouseRayOrigin + rayLength * mouseRayNormal;

            var query = PhysicsRayQueryParameters3D.Create(
                mouseRayOrigin,
                endOOfRay);
            query.CollideWithAreas = false;
            query.CollideWithBodies = false;
            var hitResult = space.IntersectRay(query);
            var wallNormal = hitResult != null ? hitResult["normal"].AsVector3() : Vector3.Back;

            var basis = _CreateBrushBasis(wallNormal, (float)_terrainPlugin.ToolAttributes.BrushSize);

            if (wallNormal.Y > 0.5) // TODO : why this threshold??
            {
                basis.Z = Vector3.Zero;
            }

            brushTransform = new Transform3D(basis, pos);
        }

        if (_terrainPlugin.SelectedMode == TerrainToolMode.VertexPainting)
        {
            if (_terrainPlugin.PluginHelper.WallPainting)
            {
                sb.Append(" | Brush radius drawn at pos : " + brushTransform.Origin);
                AddMesh(MarchingTrianglesTerrainUi.BrushData[_terrainPlugin.ToolAttributes.BrushIndex].Item1, null, brushTransform);
            }
        }
        else if (_terrainPlugin.SelectedMode != TerrainToolMode.Smooth &&
                 _terrainPlugin.SelectedMode != TerrainToolMode.GrassMask &&
                 _terrainPlugin.SelectedMode != TerrainToolMode.DebugBrush)
        {
            sb.Append(" | Brush rad. drawn at: " + TerrainToolPluginHelper.FormatVector3(brushTransform.Origin));
            sb.Append(" [G. tri Cell] : " +
                      TerrainSettings.OrientationSystem.GetCell(new Vector2D(brushTransform.Origin.X,
                          brushTransform.Origin.Z)));

            AddMesh(MarchingTrianglesTerrainUi.BrushData[_terrainPlugin.ToolAttributes.BrushIndex].Item1, null, brushTransform);
        }


        //Believe in the plugin's brush position
        pos = _terrainPlugin.PluginHelper.BrushPosition;

        var brushBounds = BrushPatternCalculator.CalculateBounds(
            pos,
            (float)_terrainPlugin.ToolAttributes.BrushSize,
            _terrainPlugin.CurTerrainNode);
        sb.Append(" | B. chk BBOX : Min="
                  + brushBounds.ChunkAABB.Item1 + " Max="
                  + brushBounds.ChunkAABB.Item2 +
                  " | B. cel BBOX : Min="
                  + brushBounds.CellAABB.Item1 + " Max="
                  + brushBounds.CellAABB.Item2);

        var maxSqDistance = BrushPatternCalculator.CalculateMaxSqDistance(
            (float)_terrainPlugin.ToolAttributes.BrushSize,
            _terrainPlugin.ToolAttributes.BrushIndex);

        var allowedDimensions = new Vector3I(
            terrain.TerrainSettings.ChunkDimensions.X,
            terrain.TerrainSettings.ChunkDimensions.Y,
            TerrainSettings.OrientationSystem.PolygonCount);

        // Step 2 : Visualization of the cells affected by the brush

        Vector2 brushPos = new Vector2(pos.X, pos.Z);
        int cellCount = 0;
        for (int chunkZ = brushBounds.ChunkAABB.Item1.Y; chunkZ <= brushBounds.ChunkAABB.Item2.Y; chunkZ++)
        {
            for (int chunkX = brushBounds.ChunkAABB.Item1.X; chunkX <= brushBounds.ChunkAABB.Item2.X; chunkX++)
            {
                Vector2I chunkCoords = new Vector2I(chunkX, chunkZ);
                if (!terrain.Chunks.TryGetValue(chunkCoords, out var chunk))
                {
                    continue;
                }

                Tuple<Vector2I, Vector2I> cellRange =
                    BrushPatternCalculator.GetCellRangeForChunk(chunkCoords, brushBounds, terrain);

                for (int z = cellRange.Item1.Y; z <= cellRange.Item2.Y; z++)
                {
                    for (int x = cellRange.Item1.X; x <= cellRange.Item2.X; x++)
                    {
                        Vector2I cellCoords = new(x, z);

                        for (int i = 0; i < chunk.Underlying.DataGrid.OrientationSystem.PolygonCount; i++)
                        {
                            Vector3I fullCoords = new(x, z, i);
                            TerrainColorMaps.EnsureRange(fullCoords, allowedDimensions);

                            Vector2D cellPositionInWorld =
                                chunk.Underlying.DataGrid.OrientationSystem.GetCellCentroid(cellCoords, i);
                            Vector2 cellFloatPos =
                                new Vector2((float)cellPositionInWorld.X, (float)cellPositionInWorld.Y);
                            float sample = BrushPatternCalculator.CalculateFalloffSample(
                                cellFloatPos,
                                brushPos,
                                _terrainPlugin.ToolAttributes.BrushSize,
                                _terrainPlugin.ToolAttributes.BrushIndex,
                                maxSqDistance,
                                _terrainPlugin.ToolAttributes.Falloff,
                                _terrainPlugin.ToolAttributes.FalloffCurve);

                            if (sample < 0)
                            {
                                // Brush has no effect
                                break;
                            }

                            float y =
                                _terrainPlugin.PluginHelper.CurrentDrawPattern.Count > 0 &&
                                _terrainPlugin.ToolAttributes.Flatten
                                    ? _terrainPlugin.PluginHelper.DrawHeight
                                    : chunk.Underlying.GetHeightFromTriCellCoords(fullCoords);

                            Vector3 drawPos = new Vector3(cellFloatPos.X, y, cellFloatPos.Y);

                            Transform3D drawTransform = new(
                                Vector3.Right * sample,
                                Vector3.Up * sample,
                                Vector3.Back * sample, drawPos);
                            if (!isWallPainting)
                            {
                                // Only draw cell Brush if NOT in wall painting mode{
                                AddMesh(MarchingTrianglesGizmoPlugin.BrushMesh, brushMat, drawTransform);
                            }

                            //Save to the currently drawn pattern
                            if (_terrainPlugin.PluginHelper.Drawing)
                            {
                                cellCount++;
                                if (!_terrainPlugin.PluginHelper.CurrentDrawPattern.ContainsKey(chunkCoords))
                                {
                                    _terrainPlugin.PluginHelper.CurrentDrawPattern.Add(chunkCoords,
                                        new Dictionary<Vector3I, float>());
                                }

                                if (_terrainPlugin.PluginHelper
                                    .CurrentDrawPattern[chunkCoords].ContainsKey(fullCoords))
                                {
                                    var previousSample =
                                        _terrainPlugin.PluginHelper
                                            .CurrentDrawPattern[chunkCoords][fullCoords];
                                    if (sample > previousSample)
                                    {
                                        _terrainPlugin.PluginHelper
                                            .CurrentDrawPattern[chunkCoords][fullCoords] = sample;
                                    }
                                }
                                else
                                {
                                    _terrainPlugin.PluginHelper
                                        .CurrentDrawPattern[chunkCoords][fullCoords] = sample;
                                }
                            }
                        }
                    }
                }
            }
        }

        sb.Append(" | Brush mesh drawn  : " + cellCount + " Cells");
    }

    private static Basis _CreateBrushBasis(Vector3 normal, float brushSize)
    {
        Vector3 n = normal.Normalized();

        Vector3 tangent = Vector3.Up.Cross(n);

        tangent = tangent.Normalized();
        Vector3 biTangent = n.Cross(tangent);

        tangent *= brushSize;
        biTangent *= brushSize;
        return new Basis(tangent, n, biTangent);
    }

    private bool ProcessNeighborChunksForGizmoLines(Vector2I hexTerrainChunk)
    {
        var v1 = DoesProvidedCoordinateChunkExist(new Vector2I(hexTerrainChunk.X - 1, hexTerrainChunk.Y));
        var v2 = DoesProvidedCoordinateChunkExist(new Vector2I(hexTerrainChunk.X + 1, hexTerrainChunk.Y));
        var v3 = DoesProvidedCoordinateChunkExist(new Vector2I(hexTerrainChunk.X, hexTerrainChunk.Y - 1));
        var v4 = DoesProvidedCoordinateChunkExist(new Vector2I(hexTerrainChunk.X, hexTerrainChunk.Y + 1));
        return v1 || v2 || v3 || v4;
    }

    private bool DoesProvidedCoordinateChunkExist(Vector2I coords)
    {
        if (Input.IsKeyPressed(Key.Ctrl))
        {
            return true;
        }

        // Draw chunk lines for addition
        if (_terrainPlugin.CurTerrainNode.Chunks.ContainsKey(coords))
        {
            return true;
        }

        return false;
    }

    private bool AddChunkLines(
        MarchingTrianglesTerrain terrain,
        Vector2I chunkCoords,
        Material material)
    {
        Clear();
        // // [DEBUG] Prints Gizmo drawings
        //GD.Print("Printing the chunk lines for : " + chunkCoords + " - Material :  " + material);

        _lines.Clear();
        List<Vector3> points = terrain.GetChunkLimits(chunkCoords);
        for (int i = 0; i < points.Count; i++)
        {
            //Draw edges
            _lines.Add(points[i]);
            _lines.Add(points[(i + 1) % points.Count]);

            //Draw half diagonals
            var p1X = Mathf.Lerp(points[i].X, points[(i + 2) % points.Count].X, 0.25f);
            var p1Y = Mathf.Lerp(points[i].Y, points[(i + 2) % points.Count].Y, 0.25f);
            var p1Z = Mathf.Lerp(points[i].Z, points[(i + 2) % points.Count].Z, 0.25f);
            var p1 = new Vector3(p1X, p1Y, p1Z);
            var p2X = Mathf.Lerp(points[i].X, points[(i + 2) % points.Count].X, 0.75f);
            var p2Y = Mathf.Lerp(points[i].Y, points[(i + 2) % points.Count].Y, 0.75f);
            var p2Z = Mathf.Lerp(points[i].Z, points[(i + 2) % points.Count].Z, 0.75f);
            var p2 = new Vector3(p2X, p2Y, p2Z);
            _lines.Add(p1);
            _lines.Add(p2);

            // // [DEBUG] Prints Gizmo drawings
            // GD.Print("Drawing segment between " + points[i] + " and " + points[(i + 1) % points.Count]);
        }

        try
        {
            AddLines(_lines.ToArray(), material);
        }
        catch (Exception e)
        {
            GD.PushError(e);
            return false;
        }

        return true;
    }
}