using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.editor.utils;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles.triangulation;

public class ChunkConformalEditor
{
    /// <summary>
    /// The chunk on which the editor works
    /// </summary>
    private readonly HexagonalTerrainChunk _chunk;

    private readonly TerrainChunkEdgeGraph _graph;

    private readonly Dictionary<Vector2I, Triangulation?[]?> _triangulationsPerCell;

    private readonly Dictionary<Vector2I, HexTerrainCell> _cells;

    private readonly Func<Vector2I, Vector2I, HexagonalTerrainChunk?>? _neighborChunkProvider;

    ///
    /// Dictionaries of the vertex editors indexed by the graph node.
    ///
    public Dictionary<EdgeExtremity, GeometryEditionRequest> VertexRequests { get; } = new();

    private readonly Dictionary<(EdgeExtremity, Triangulation), VertexGeometryEditor> _vertexGeometryEditors = new();

    private readonly Dictionary<EdgeGraphNode, GeometryEditionRequest> _vertexProcessedRequests = new();

    ///
    /// Dictionaries of the vertex editors indexed by the graph edge.
    ///
    public Dictionary<SubCellBorder, GeometryEditionRequest> EdgeRequests { get; } = new();

    private readonly Dictionary<SubCellBorder, VertexGeometryEditor> _edgeGeometryEditors = new();

    private readonly Dictionary<ChunkTerrainEdge, GeometryEditionRequest> _edgeProcessedRequests = new();


    public ChunkConformalEditor(TerrainChunkEdgeGraph graph)
    {
        _chunk = graph.Chunk;
        _graph = graph;

        _cells = _chunk.TerrainDualGrid.CompleteCells;
        _triangulationsPerCell = graph.GetTriangulationsMap();
    }

    public HexagonalTerrainChunk Chunk => _chunk;


    /// <summary>
    /// Returns the dictionary of mesh triangles, indexed by the cell it's originating from.  
    /// </summary>
    /// <returns></returns>
    public Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> GetTriangleInfos(
        List<Vector2I>? cellIndices = null)
    {
        cellIndices ??= [.. _triangulationsPerCell.Keys];

        Dictionary<HexTerrainCell, List<HexTerrainCell.TriangleInfo>> output = new();
        foreach (var cellIndex in cellIndices)
        {
            var cellTriangulations = _triangulationsPerCell[cellIndex];
            if (cellTriangulations == null)
            {
                continue;
            }

            // Find the cell
            var cell = _cells[cellIndex];
            output.TryAdd(cell, []);
            foreach (var tuple in cellTriangulations)
            {
                if (tuple == null)
                {
                    continue;
                }

                //Process the triangulation and fetch the triangles
                output[cell].AddRange(tuple.ToTriangleInfoList());
            }
        }

        return output;
    }

    public void ApplyGeometryOperations()
    {
        foreach (var geometryEdit in _vertexGeometryEditors.Values)
        {
            geometryEdit.ApplyAllTransformations();
        }

        foreach (var geometryEdit in _edgeGeometryEditors.Values)
        {
            geometryEdit.ApplyAllTransformations();
        }
    }

    //Collects the geometry behaviour of all the vertices of the full cells of the chunk 
    public void CollectAllOperationsRequests()
    {
        CollectVertexOperationRequests();
        CollectEdgeOperationRequests();
    }

    private void CollectEdgeOperationRequests()
    {
        foreach (var graphEdge in _graph.Edges)
        {
            var borders = graphEdge.GetSubCellBorders();

            foreach (var border in borders)
            {
                ComputeAction(border, out var mode, out var p1, out var p2);
                EdgeRequests.Add(border, new GeometryEditionRequest(mode, p1, p2));
            }
        }
    }

    private void CollectVertexOperationRequests()
    {
        foreach (var edgeGraphNode in _graph.Vertices)
        {
            foreach (var nodeEdgeExtremities in edgeGraphNode.RelatedEdgeExtremities)
            {
                ComputeAction(nodeEdgeExtremities.Value, out var mode, out var p1, out var p2);
                VertexRequests.Add(nodeEdgeExtremities.Value, new GeometryEditionRequest(mode, p1, p2));
            }
        }
    }

    private void ComputeAction(EdgeExtremity edgeExtremity, out GeometryMode mode, out float param1, out float param2)
    {
        var pos = new Vector3[2];

        var cellIdx = edgeExtremity.CellIndex;
        var vertexIdx = edgeExtremity.CellSummitIndex;
        var cell = _cells[cellIdx];

        GetPosAndValue(out var pos2D, out var value);
        pos[0] = new Vector3((float)pos2D.X,  value,(float)pos2D.Y);
        vertexIdx = edgeExtremity.OtherCellSummitIndex;

        GetPosAndValue(out pos2D, out value);
        pos[1] = new Vector3((float)pos2D.X, value, (float)pos2D.Y);

        var isOverThreshold =
            Chunk.DefaultThreshold.Item2.IsOverThreshold(Chunk.DefaultThreshold.Item1, pos[0], pos[1]);

        var modePair = cell.GeometryModesOverride ??
                       _chunk.DefaultGeometryModes ??
                       throw new InvalidOperationException();

        var paramPair = cell.ParametersOverride ??
                        _chunk.GeometryModeParameters ??
                        throw new InvalidOperationException();

        mode = isOverThreshold ? modePair.Item2 : modePair.Item1;
        var parameters = isOverThreshold ? paramPair.Item2 : paramPair.Item1;
        param1 = parameters.Item1;
        param2 = parameters.Item2;
        return;

        void GetPosAndValue(out Vector2D vector2D, out float f)
        {
            if (vertexIdx == -1)
            {
                vector2D = cell.CenterPosition;
                f = cell.AverageHeight;
            }
            else
            {
                vector2D = cell.VertexPositionsInPlane[vertexIdx];
                f = cell.GetVertexData!(vertexIdx);
            }
        }
    }


    private void ComputeAction(SubCellBorder border, out GeometryMode mode, out float param1, out float param2)
    {
        var pos = new Vector3[2];

        pos[0] = border.Triangulation.SourceTriangle[border.TriEdgeIdx];
        pos[1] = border.Triangulation.SourceTriangle[(border.TriEdgeIdx + 1) % 3];

        var isOverThreshold =
            Chunk.DefaultThreshold.Item2.IsOverThreshold(Chunk.DefaultThreshold.Item1, pos[0], pos[1]);

        var modePair = border.ParentCell.GeometryModesOverride ??
                       _chunk.DefaultGeometryModes ??
                       throw new InvalidOperationException();

        var paramPair = border.ParentCell.ParametersOverride ??
                        _chunk.GeometryModeParameters ??
                        throw new InvalidOperationException();

        mode = isOverThreshold ? modePair.Item2 : modePair.Item1;
        var parameters = isOverThreshold ? paramPair.Item2 : paramPair.Item1;
        param1 = parameters.Item1;
        param2 = parameters.Item2;
    }


    public void CollectBorderOperationRequests()
    {
        //throw new NotImplementedException();
    }

    /// <summary>
    /// Registers all the geometry operations based of the processed edge requests. 
    /// </summary>
    public void RegisterOperations()
    {
        RegisterVertexOperations();
        RegisterEdgeOperations();
    }

    private void RegisterVertexOperations()
    {
        var vertexEditionGroups = VertexRequests.GroupBy(kvp => kvp.Key.Node);
        var comparer = new UnorderedValueTupleComparer<SubCellBorder?>();
        foreach (var group in vertexEditionGroups)
        {
            HashSet<(SubCellBorder, SubCellBorder)> consolidatedRes = new(comparer);
            var vertexNode = group.Key;
            var requests = group.Select(g => g.Value).ToHashSet();
            var sources = group.Select(g => g.Key).ToList();

            GeometryEditionRequest processedRequest = MergeRequests(vertexNode, requests);
            _vertexProcessedRequests.Add(vertexNode, processedRequest);

            var adjCells = vertexNode.RelatedEdgeExtremities.Keys
                .Select(v => _cells[new Vector2I(v.X, v.Y)])
                .Distinct()
                .ToList();
            var adjEdges = _graph.AdjacentEdges(vertexNode).SelectMany(n => n.GetSubCellBorders());

            var pairwiseList = sources.SelectMany(e1 =>
                    sources
                        .Where(e2 => e1 != e2 && e1.CellIndex == e2.CellIndex)
                        .Select(e2 => (e1, e2)))
                .ToList();
            //We do a pairwise check on the implicit edges of
            // each EdgeExtremity to find Triangulations that exist in one of the subcellborders, for both of the edges in the pair.  
            foreach (var pair in pairwiseList)
            {
                var oExtrem1 = pair.Item1.OtherCellSummitIndex;
                var oExtrem2 = pair.Item2.OtherCellSummitIndex;

                //We restrict the SubCellBorders to the ones within the same cell 
                var subCellBordersOfEdge1 = adjEdges.Where(scb =>
                    scb.ParentCell.CellCoordsImplicit == pair.Item1.CellIndex &&
                    ( //Select subcellborders that match the first EdgeExtremity's implicit edges
                        //Checks against edge1 of the pair
                        scb.CellSummitIndices ==
                        (pair.Item1.CellSummitIndex, pair.Item1.OtherCellSummitIndex) ||
                        scb.CellSummitIndices ==
                        (pair.Item1.OtherCellSummitIndex, pair.Item1.CellSummitIndex)
                    )).ToList();
                var subCellBordersOfEdge2 = adjEdges.Where(scb =>
                    scb.ParentCell.CellCoordsImplicit == pair.Item1.CellIndex &&
                    (
                        //Checks against edge2 of the pair
                        scb.CellSummitIndices ==
                        (pair.Item2.CellSummitIndex, pair.Item2.OtherCellSummitIndex) ||
                        scb.CellSummitIndices ==
                        (pair.Item2.OtherCellSummitIndex, pair.Item2.CellSummitIndex)
                    )).ToList();


                foreach (var scbE1 in subCellBordersOfEdge1)
                {
                    bool found = false;
                    foreach (var scbE2 in subCellBordersOfEdge2)
                    {
                        if (scbE1.Triangulation != scbE2.Triangulation) continue;
                        consolidatedRes.Add((scbE1, scbE2));
                        found = true;
                        break;
                    }

                    if (found) break;
                }
            }

            foreach (var tuple in consolidatedRes)
            {
                var sc1 = tuple.Item1;
                var sc2 = tuple.Item2;
                List<int> sc1Indices = [sc1.CellSummitIndices.Item1, sc1.CellSummitIndices.Item2];
                List<int> sc2Indices = [sc2.CellSummitIndices.Item1, sc2.CellSummitIndices.Item2];
                var common = sc1Indices.Intersect(sc2Indices).ToList();
                if (common.Count != 1)
                {
                    throw new InvalidOperationException();
                }

                var commonValue = common.First();
                var editor = new VertexGeometryEditor();

                var edges = sources.Where(ee => ee.CellIndex == sc1.ParentCell.CellCoordsImplicit
                                                && (ee.CellSummitIndex == sc1.CellSummitIndices.Item1 &&
                                                    ee.OtherCellSummitIndex == sc1.CellSummitIndices.Item2)
                                                ||
                                                (ee.OtherCellSummitIndex == sc1.CellSummitIndices.Item1 &&
                                                 ee.CellSummitIndex == sc1.CellSummitIndices.Item2)
                                                && ee.CellSummitIndex == commonValue).ToList();

                if (edges.Count != 1)
                {
                    throw new InvalidOperationException();
                }

                var request = (processedRequest.mode, (processedRequest.param1, processedRequest.param2));
                //Console.WriteLine($"<V>T{sc1.Triangulation.Id};({GetVertexIndexInTriangulation(sc1.TriEdgeIdx, sc2.TriEdgeIdx)}) => {request}");
                _vertexGeometryEditors.Add((edges[0], sc1.Triangulation), editor);
                editor.RegisterLocalVertexAction(
                    sc1.ParentCell,
                    commonValue,
                    sc1.Triangulation,
                    GetVertexIndexInTriangulation(sc1.TriEdgeIdx, sc2.TriEdgeIdx),
                    [.. adjCells.Where(c => c != sc1.ParentCell)],
                    request);
            }
        }

        int GetVertexIndexInTriangulation(int edgeIdx1, int edgeIdx2)
        {
            switch (edgeIdx1, edgeIdx2)
            {
                case (0, 1):
                case (1, 0):
                    return 0;
                case (1, 2):
                case (2, 1):
                    return 1;
                case (0, 2):
                case (2, 0):
                    return 2;
                default: throw new NotSupportedException();
            }
        }
    }

    private void RegisterEdgeOperations()
    {
        var edgeEditionGroups = EdgeRequests.GroupBy(kvp => kvp.Key.GetParentEdge());
        foreach (var group in edgeEditionGroups)
        {
            var edge = group.Key;
            var requests = group.Select(g => g.Value).ToHashSet();
            var sources = group.Select(g => g.Key).ToList();

            GeometryEditionRequest processedRequest = MergeRequests(edge, requests);
            _edgeProcessedRequests.Add(edge, processedRequest);
            foreach (var subCellBorder in sources)
            {
                var editor = new VertexGeometryEditor();
                _edgeGeometryEditors.Add(subCellBorder, editor);
                
                var request = (processedRequest.mode, (processedRequest.param1, processedRequest.param2));
                //Console.WriteLine($"<E>T{subCellBorder.Triangulation.Id};({subCellBorder.TriEdgeIdx}) => {request}");

                editor.RegisterLocalEdgeAction(
                    subCellBorder.ParentCell,
                    subCellBorder.CellSummitIndices,
                    subCellBorder.Triangulation,
                    subCellBorder.TriEdgeIdx,
                    [
                        .. edge.GetSubCellBorders().Select(scb => scb.ParentCell)
                            .Where(c => c != subCellBorder.ParentCell)
                    ],
                    (processedRequest.mode, (processedRequest.param1, processedRequest.param2)));
            }
        }
    }

    private GeometryEditionRequest MergeRequests(ChunkTerrainEdge edge, HashSet<GeometryEditionRequest> requests)
    {
        if (requests.Count == 1)
        {
            return requests.First();
        }
        else
        {
            var mode =  requests.Min(r=>r.mode);
            var p1 = requests.Average(r => r.param1);
            var p2 = requests.Average(r => r.param2);
            return new GeometryEditionRequest(mode, p1, p2);
            
        }
    }

    private GeometryEditionRequest MergeRequests(EdgeGraphNode vertex, HashSet<GeometryEditionRequest> requests)
    {
        if (requests.Count == 1)
        {
            return requests.First();
        }
        else
        {
            var mode =  requests.Min(r=>r.mode);
            var p1 = requests.Average(r => r.param1);
            var p2 = requests.Average(r => r.param2);
            return new GeometryEditionRequest(mode, p1, p2);
        }
    }


    /// <summary>
    /// Adds the missing triangles to make the chunk triangulation conformal if needed. 
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    private void StitchEdges()
    {


        //Now we're finally stitchin some stuff
        foreach (var distinctRequest in _edgeGeometryEditors)
        {
            var triIdx = distinctRequest.Key.TriEdgeIdx;

            if (triIdx == 0 ) //Easy case : inner cell triangulations
            {
                StitchInnerCellEdge(distinctRequest.Key);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }

    private void StitchInnerCellEdge(SubCellBorder scb)
    {

        var summitIdx = scb.CellSummitIndices.Item1 == -1
            ? scb.CellSummitIndices.Item2
            : scb.CellSummitIndices.Item1;
        int cellIdx;
        if (summitIdx == -1)
        {
            cellIdx = _triangulationsPerCell[scb.ParentCell.CellCoordsImplicit]?
                          .Index()
                          .Where(t => t.Item == scb.Triangulation)
                          .Select(t => t.Index).First()
                      ?? throw new ArgumentException(
                          "Triangulation cannot be found in the sub cells of the provided cell");
        }
        else
        {
            cellIdx = summitIdx;
        }

        //Fetch the neighbor cell index
        var neighborSubCellIndex = GetNeighborSubCellIndex(cellIdx);

        var neighborTri = _triangulationsPerCell[scb.ParentCell.CellCoordsImplicit]?[neighborSubCellIndex];
        if (neighborTri == null) return;
        var neighborEdgeIdx = GetNeighborEdgeIndex();

        var neighborEdge = ExplicitEdge(neighborTri.Edges[neighborEdgeIdx], neighborTri);
        var curEdge = ExplicitEdge(scb.Triangulation.Edges[scb.TriEdgeIdx], scb.Triangulation);

        //Ensure start point and end point are similar in the XoZ plane
        // If that is the case, the polygon to cover will be easier to obtain
        var invert = CheckExtremitiesInXoZPlane(neighborEdge, curEdge);
        if (!invert) throw new NotImplementedException(); //the edges go in the "same" order,
        //triangulating might create a flipped triangle
        //Check if the edges are identical.
        if (neighborEdge.Count == curEdge.Count)
        {
            bool equivalentEdges = true;
            for (int i = 0; i < neighborEdge.Count; i++)
            {
                if (!neighborEdge[^(i + 1)].Item1.IsEqualApprox(curEdge[i].Item1))
                {
                    equivalentEdges = false;
                    break;
                }
            }

            if (equivalentEdges) //The neighboring edges are equivalent
            {
                return;
            }
        }

        //We now reconstruct a mesh between the edges using the provided triangulation
        TriangleUtils.BuildPolygonTriangulation(
            new TriangleUtils.PolygonTriangulationBuilderData(
                _edgeGeometryEditors[scb],
                scb.Triangulation,
                curEdge,
                scb.TriEdgeIdx),
            new TriangleUtils.PolygonTriangulationBuilderData(
                _edgeGeometryEditors[scb],
                neighborTri,
                neighborEdge,
                neighborEdgeIdx));


        return;

        int GetNeighborSubCellIndex(int cellSummitIdx)
        {
            return scb.TriEdgeIdx switch
            {
                0 => EngineUtils.Mod(cellSummitIdx - 1, HexTerrainCell.VertexCount),
                2 => EngineUtils.Mod(cellSummitIdx + 1, HexTerrainCell.VertexCount),
                _ => throw new NotSupportedException()
            };
        }

        int GetNeighborEdgeIndex()
        {
            return scb.TriEdgeIdx switch
            {
                0 => 2,
                2 => 0,
                _ => throw new NotSupportedException()
            };
        }


        bool CheckExtremitiesInXoZPlane(List<(Vector3, int)> nEdge, List<(Vector3, int)> cEdge)
        {
            var nFirst = nEdge.First().Item1;
            var nLast = nEdge.Last().Item1;

            var nS = new Vector2(nFirst.X, nFirst.Z);
            var nE = new Vector2(nLast.X, nLast.Z);

            var cFirst = cEdge.First().Item1;
            var cLast = cEdge.Last().Item1;
            var cS = new Vector2(cFirst.X, cFirst.Z);
            var cE = new Vector2(cLast.X, cLast.Z);

            if (!(nS.IsEqualApprox(cE) ^ nS.IsEqualApprox(cS)) ||
                !(nE.IsEqualApprox(cE) ^ nE.IsEqualApprox(cS)))
            {
                throw new InvalidOperationException();
            }

            return nE.IsEqualApprox(cS);
        }
    }

    public static List<(Vector3, int Value)> ExplicitEdge(LinkedList<int> linkedList, Triangulation tri)
    {
        var edge = linkedList.Select(edge =>
        {
            var elem = tri.SubEdges.ElementAt(edge);
            return (tri.Vertices[elem.Key.Item1], elem.Value);
        }).ToList();
        var lastElement = tri.SubEdges.ElementAt(linkedList.Last!.Value);
        edge.Add((tri.Vertices[lastElement.Key.Item2], lastElement.Value));
        return edge;
    }

    
    /// <summary>
    /// Debug method that dumps the state of the chunk editor to the standard output.
    /// </summary>
    public void DebugDumpEditorState(bool forceMeshOutput = false)
    {
        StringBuilder sb = new();
        AppendHSeparator(sb);
        sb.Append($"Debug Chunk Editor state dump for chunk {_chunk.Coordinates}")
            .AppendLine()
            .Append($"Snapshot timestamp: {DateTime.Now.ToString(CultureInfo.InvariantCulture)}")
            .AppendLine();
        AppendHSeparator(sb);
        sb.Append("General chunk information").AppendLine();
        AppendHSeparator(sb);
        sb.Append($"Chunk coordinates: {_chunk.Coordinates}")
            .AppendLine()
            .Append($"Chunk dimensions :{_chunk.Dimensions2D}")
            .AppendLine()
            .Append($"Data points count: {_chunk.DataGrid.Size}")
            .AppendLine()
            .Append($"Full hexagonal cells count: {_chunk.TerrainDualGrid.CompleteCells.Count}")
            .AppendLine()
            .Append($"Partial hexagonal cells count: {_chunk.TerrainDualGrid.PendingCells.Count}")
            .AppendLine();
        AppendHSeparator(sb);
        sb.Append("Chunk Processing information").AppendLine();
        AppendHSeparator(sb);
        sb.Append($"Chunk threshold computation mode: {_chunk.DefaultThreshold.Item2}")
            .AppendLine()
            .Append($"Chunk threshold value: {_chunk.DefaultThreshold.Item1}")
            .AppendLine()
            .Append($"Default processing mode: {_chunk.DefaultGeometryModes?.Item1}")
            .AppendLine()
            .Append($"Over-threshold processing mode :{_chunk.DefaultGeometryModes?.Item2}")
            .AppendLine();
        AppendHSeparator(sb);
        sb.Append("Current chunk editor information")
            .AppendLine();
        AppendHSeparator(sb);
        sb.Append("Processing status: TODO")
            .AppendLine()
            .Append($"Edge edition request count: {
                EdgeRequests.Count}")
            .AppendLine()
            .Append($"Vertex edition request count: {
                EdgeRequests.Count}")
            .AppendLine()
            .Append($"Triangulation count: {_triangulationsPerCell.SelectMany(kvp => kvp.Value!).Count()}")
            .AppendLine();
        AppendHSeparator(sb);
        sb.Append("Per cell manifold data").AppendLine();
        AppendHSeparator(sb);
        foreach (var hexTerrainCell in _cells.Values)
        {
            DebugDumpCellState(hexTerrainCell, sb, forceMeshOutput);
            sb.AppendLine();
            AppendHSeparator(sb);
        }


        Console.WriteLine(sb);
    }

    private void DebugDumpCellState(HexTerrainCell cell, StringBuilder sb, bool forceStlOutput = false)
    {
        sb.Append('\t').Append(cell)
            .AppendLine()
            .Append('\t');

        sb.Append("Data Y values :");
        for (int i = 0; i < HexTerrainCell.VertexCount; i++)
        {
            sb.Append($"     {cell.GetVertexData!(i):0.00}     ");
        }

        sb.AppendLine().Append('\t');
        sb.Append($"Avg cell Y value: {cell.AverageHeight}")
            .AppendLine().Append('\t')
            .Append($"Overrides chunk geometry behavior: {cell.GeometryModesOverride != null}")
            .AppendLine().Append('\t');
        if (cell.GeometryModesOverride != null)
        {
            sb.Append($"Cell processing mode: {_chunk.DefaultGeometryModes?.Item1}")
                .AppendLine().Append('\t')
                .Append($"Cell over-threshold processing mode :{_chunk.DefaultGeometryModes?.Item2}")
                .AppendLine().Append('\t');
        }

        var tris = GetTriangleInfos([cell.CellCoordsImplicit])[cell];
        sb.Append($"Cell triangles count: {tris.Count}")
            .AppendLine().Append('\t');

        var (dico, borderEdgesAsSets, manifoldBorderEdgesAsSets) = TriangleUtils.ReprocessEdgeGeometry(tris, false);
        var isManifold = true &&
                         TriangleUtils.AssertIsClosedManifoldBorder(borderEdgesAsSets, false) &&
                         TriangleUtils.AssertIsClosedManifoldBorder(manifoldBorderEdgesAsSets, false);
        foreach (var _ in dico.Where(kvp => !(
                     kvp.Value == 2 ||
                     kvp.Value == 1 && borderEdgesAsSets.Contains(kvp.Key, new FloatArrayComparer(1e-5)))))
        {
            isManifold = false;
        }

        sb.Append($"Cell triangulation is manifold : {isManifold}").AppendLine();
        AppendHSeparator(sb);
        sb.Append('\t').Append('\t');
        if (!isManifold || forceStlOutput)
        {
            sb.Append("Dumping cell triangulation data as obj snippet because of non-manifold output :")
                .AppendLine();
            AppendHSeparator(sb);
            ToStlDump(tris, sb);
        }
    }

    private static void ToStlDump(List<HexTerrainCell.TriangleInfo> tris, StringBuilder sb)
    {
        sb.Append("solid mesh").AppendLine();

        foreach (var triangleInfo in tris)
        {
            var n = TriangleUtils.GetNormal(triangleInfo.Points);
            sb.Append($"facet normal {n.X} {n.Y} {n.Z}").AppendLine()
                .Append('\t').Append("outer loop").AppendLine();
            foreach (var p in triangleInfo.Points)
            {
                sb.Append('\t')
                    .Append('\t')
                    .Append($"vertex {p.X} {p.Y} {p.Z}")
                    .AppendLine();
            }

            sb.Append('\t').Append("end loop").AppendLine().Append("end facet").AppendLine();
        }
    }

    private static void AppendHSeparator(StringBuilder sb)
    {
        sb.Append("------------------------------------------------------------------------------------").AppendLine();
    }

    /// <summary>
    /// This method aims to fix issues with the chunk triangulation if the output is not cell-wise manifold 
    /// </summary>
    public void PostProcess()
    {

    }
}