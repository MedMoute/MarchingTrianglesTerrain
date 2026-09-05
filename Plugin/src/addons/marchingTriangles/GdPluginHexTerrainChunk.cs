using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.utils;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// TODO : split local computation and mesh / plugin stuff
[GlobalClass]
public partial class GdPluginHexTerrainChunk : MeshInstance3D
{
    public HexagonalTerrainChunk Underlying { get; private set; }

    /// <summary>
    /// The default collision layer used for plugin editing. 
    /// </summary>
    public static readonly uint DefaultCollisionLayer = 17;

    internal ConcavePolygonShape3D _tempCollisionShape;

    private SurfaceTool _st = new();

    public bool SkipSaveOnExit { get; set; } = false; // Set to true when chunk is removed temporarily (undo/redo)

    public enum Mode
    {
        MODE_1 = 1,
        MODE_2 = 2
    }


    /// <summary>
    /// Public constructor.
    /// </summary>
    /// <param name="chunkIndex">Position of the chunk in the overall terrain</param>
    /// <param name="dimension">Vertex brush data of the chunk</param>
    /// <param name="dataSource">Initialized vertex brush data of the chunk</param>
    /// <param name="dataSource2"></param>
    /// <param name="neighboringChunkDataHandle">Neighbor-only aware chunk provider.</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public GdPluginHexTerrainChunk(Vector2I chunkIndex,
        Vector2I dimension,
        Func<Vector2I, HexagonalTerrainChunk> neighboringChunkDataHandle,
        float[][] dataSource = null,
        float[][] dataSource2 = null)
    {
        Underlying = new HexagonalTerrainChunk(
            chunkIndex,
            dimension,
            neighboringChunkDataHandle,
            dataSource,
            dataSource2);
    }

    public void InitializeTerrain(bool regenerateMesh = true)
    {
        // Initially all cells will need to be updated to show the newly loaded height
        for (int x = 0; x < Underlying.Dimensions2D.X; x++)
        {
            for (int y = 0; y < Underlying.Dimensions2D.Y; y++)
            {
                for (int z = 0; z < Underlying.DataGrid.OrientationSystem.PolygonCount; z++)
                {
                    Underlying.NeedUpdate[new Vector3I(x, y, z)] = true;
                }
            }
        }

        if (Underlying.ColorMaps == null)
        {
            Underlying.InitializeColorMaps();
        }

        if (regenerateMesh)
        {
            GenerateTerrain(true);
        }
        if (Mesh != null && GetParent() is MarchingTrianglesTerrain terrain)
        {
            Mesh.SurfaceSetMaterial(0, terrain.TerrainSettings.ShaderMaterial);
        }

        _tempCollisionShape = CreateAndGetCollision();
        ProcessCollisionShape();
        if (!Engine.IsEditorHint() && false) // No runtime baking atm.
        {
            throw new NotImplementedException();
        }
    }

    public void GenerateTerrain(bool forceFullRebuild = true)
    {
        if (Mesh != null && !forceFullRebuild)
        {
            lock (_st)
            {
                _st.CreateFrom(Mesh, 0);
            }
        }

        GenerateSurfaces(forceFullRebuild);
        _tempCollisionShape = CreateAndGetCollision();
        ProcessCollisionShape();
    }

    private ConcavePolygonShape3D CreateAndGetCollision()
    {
        foreach (var child in GetChildren())
        {
            if (child is StaticBody3D)
            {
                child.Free();
            }
        }

        ConcavePolygonShape3D shape = null;
        CreateTrimeshCollision();
        foreach (var child in GetChildren())
        {
            if (child is StaticBody3D)
            {
                foreach (var grandChild in child.GetChildren())
                {
                    if (grandChild is CollisionShape3D collisionShape)
                    {
                        shape = collisionShape.Shape as ConcavePolygonShape3D;
                    }
                }
            }
        }

        return shape;
    }


    private void BakeMesh()
    {
        throw new NotImplementedException();
    }

    private void ProcessCollisionShape()
    {
        if (Engine.Singleton != null && Engine.IsEditorHint())
        {
            if (!IsInsideTree())
            {
                GD.PushError(string.Format("Chunk {0} is not inside tree. Aborting collision shape creation.",
                    Underlying.Coordinates));
                return;
            }

            if (_tempCollisionShape == null)
            {
                GD.PushError(string.Format("Chunk {0} has no pending shape. Aborting collision shape creation.",
                    Underlying.Coordinates));
                return;
            }

            foreach (var child in GetChildren())
            {
                if (child is StaticBody3D)
                {
                    child.Free();
                }
            }

            var body = new StaticBody3D();
            body.Name = Name + "_col";
            body.CollisionLayer = DefaultCollisionLayer;
            if (GetParent() != null && GetParent() is MarchingTrianglesTerrain)
            {
                var terrain = GetParent() as MarchingTrianglesTerrain;
                body.SetCollisionLayerValue(terrain.TerrainSettings.CollisionLayer, true);
            }

            var colShape = new CollisionShape3D();
            colShape.Name = "CollisionShape3D";
            colShape.Shape = _tempCollisionShape;
            colShape.Visible = false;
            body.AddChild(colShape);
            AddChild(body);

            // Owner set for visibility
            var sceneRoot = EngineUtils.GetRootNode(this);
            if (sceneRoot != null)
            {
                body.Owner = sceneRoot;
                colShape.Owner = sceneRoot;
            }

            foreach (StringName group in GetGroups())
            {
                if (group.ToString().StartsWith("navmesh_"))
                {
                    body.AddToGroup(group);
                }
            }
        }
    }

    private void GenerateSurfaces(bool forceRegeneration = false)
    {
        lock (_st)
        {
            _st.Begin(Mesh.PrimitiveType.Triangles);
            _st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);
            _st.SetCustomFormat(1, SurfaceTool.CustomFormat.RgbaFloat);
            _st.SetCustomFormat(2, SurfaceTool.CustomFormat.RgbaFloat);
        }

        //Free the lock so the thread workers can take it
        ProcessCells(forceRegeneration);
        lock (_st)
        {
            _st.GenerateNormals();
            _st.GenerateTangents();
            _st.Index();
            Mesh = _st.Commit();
            if (GetParent() != null && GetParent() is MarchingTrianglesTerrain)
            {
                var terrain = GetParent() as MarchingTrianglesTerrain;
                Mesh.SurfaceSetMaterial(0,terrain.TerrainSettings.ShaderMaterial);
            }
        }
    }


    public void ProcessCells(bool forceRebuild = false)
    {
        var tasks = new HashSet<Action>();
        // Only process the complete hexagonal cells.
        foreach (var hexagonCell in Underlying._terrainDualGrid.CompleteCells)
        {
            var reprocessHex = forceRebuild || Underlying.NeedUpdate.Any(
                kvp => kvp.Value && 
                       hexagonCell.DualCellsMapping.ContainsValue(kvp.Key));


            tasks.Add(reprocessHex ? hexagonCell.PlanCellProcessing(this) : hexagonCell.CopyCellDataToPending(this));
        }

        // TODO batch processing with reuse of threads (FJP) otherwise its more costly to do in //
        //Parallel.Invoke(tasks.ToArray());

        foreach (var action in tasks)
        { 
            action.Invoke();
        }

        Underlying.NeedUpdate.Clear();
    }

    public void ProcessPointsIntoMeshTriangles(List<Tuple<Vector3[], bool>> trianglesWithWallEdges, HexTerrainCell cell)
    {
        CellDataArrays dataArray = cell.TempDataArrays;
        dataArray.EnsureProcessable();

        lock (_st)
        {
            bool floorMode = true;
            for (int i = 0; i < dataArray.Pt.Count; i++)
            {
                if (floorMode && !dataArray.Floor[i])
                {
                    floorMode = false;
                    _st.SetSmoothGroup(UInt32.MaxValue);
                }
                else if (!floorMode && dataArray.Floor[i])
                {
                    _st.SetSmoothGroup(0);
                }

                _st.SetColor(dataArray.Color0[i]);
                _st.SetCustom(0, dataArray.Color1[i]);
                _st.SetCustom(1, dataArray.Custom1Value[i]);
                _st.SetCustom(2, dataArray.MatBlend[i]);
                _st.SetUV(dataArray.Uv[i]);
                _st.SetUV2(dataArray.Uv2[i]);
                _st.AddVertex(dataArray.Pt[i]);
            }
        }
    }

    public void AddPoint(Vector3 p, Vector2 _uv, HexTerrainCell cell)
    {
        //UV - used for ledge detection. X = closeness to top terrace, Y = closeness to bottom of terrace
        //Walls will always have UV of 1, 1
        Vector2 uv = cell.FloorMode ? _uv : Vector2.One;

        Vector2 uv2 = cell.FloorMode
            ? new Vector2(p.X, p.Z) / 1f / MathF.Sqrt(3)
            : new Vector2(p.X, p.Y) + new Vector2(p.Z, p.Y);

        var data = cell.TempDataArrays;

        data.Pt.Add(p);
        data.Uv.Add(uv);
        data.Uv2.Add(uv2);
        var colors = Underlying.BlendColors(this, cell, p, uv, true);
        data.Custom1Value.Add(colors["custom_1_value"]);
        data.Color0.Add(colors["color_0"]);
        data.Color1.Add(colors["color_1"]);
        data.MatBlend.Add(colors["mat_blend"]);
        data.Floor.Add(cell.FloorMode);
        
    }
}

/// <summary>
/// Terrain blend options to allow for smooth color and height blend influence at transitions and at different heights
/// </summary>
record TerrainBlendOptions(float LowerThreshold, float UpperThreshold, float BlendSensitivity)
{
    public float GetBlendBand()
    {
        return UpperThreshold - LowerThreshold;
    }
}