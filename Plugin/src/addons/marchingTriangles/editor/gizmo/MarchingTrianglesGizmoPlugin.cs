using System.Collections.Generic;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.data;


namespace MarchingTrianglesTerrain.addons.marchingTriangles.editor.gizmo;

public partial class MarchingTrianglesGizmoPlugin : EditorNode3DGizmoPlugin
{
    private readonly Dictionary<Node, MarchingTriangleTerrainChunkGizmo> _chunkGizmos = new();
    private readonly Dictionary<Node, MarchingTrianglesTerrainGizmo> _terrainGizmos = new();

    public static readonly PlaneMesh BrushMesh = FileUtils.Load<PlaneMesh>("res://addons/marchingTriangles/editor/resources/plugin_materials/brush_visual.tres");

    public static Color HighlightColor = Colors.Blue;
    
    public MarchingTrianglesGizmoPlugin()
    {
            CreateMaterial(nameof(BrushMesh), Colors.White, false, true);
            CreateMaterial(nameof(MarchingTrianglesTerrain.RemoveChunk), Colors.Red, false, true);
            CreateMaterial(nameof(MarchingTrianglesTerrain.AddChunk), Colors.Green, false, true);
            CreateMaterial(nameof(HighlightColor), HighlightColor, false, true);
    }

    public override EditorNode3DGizmo _CreateGizmo(Node3D node3D)
    {
        switch (node3D)
        {
            // Chunk gizmo
            case GdPluginHexTerrainChunk when !_chunkGizmos.ContainsKey(node3D):
            {
                node3D.TreeExited += () => _chunkGizmos.Remove(node3D);
                var res = new MarchingTriangleTerrainChunkGizmo();
                _chunkGizmos.Add(node3D, res);
                return res;
            }
            // Terrain gizmo
            case MarchingTrianglesTerrain when !_terrainGizmos.ContainsKey(node3D):
            {
                node3D.TreeExited += () => _chunkGizmos.Remove(node3D);
                var res = new MarchingTrianglesTerrainGizmo();
                _terrainGizmos.Add(node3D, res);
                return res;
            }
            default:
                return null!;
        }
    }

    public void TriggerRedraw(Node? node)
    {
        switch (node)
        {
            case MarchingTrianglesTerrain when _terrainGizmos.TryGetValue(node, out var terrainGizmo):
                terrainGizmo._Redraw();
                break;
            case GdPluginHexTerrainChunk when _chunkGizmos.TryGetValue(node, out var chunkGizmo):
                chunkGizmo._Redraw();
                break;
        }
    }

    public void Clear()
    {
        _chunkGizmos.Clear();
        _terrainGizmos.Clear();
    }

    public override string _GetGizmoName()
    {
        return "Marching Triangles Terrain";
    }
}