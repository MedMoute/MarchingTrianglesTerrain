using System;
using System.Collections.Generic;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.tiling;
using MathNet.Spatial.Euclidean;

namespace MarchingTrianglesTerrain.addons.marchingTriangles;

/// <summary>
/// Triangular tiling of a plan.
/// Each cell is made of two equilateral triangles. 
/// </summary>
public class TriangleGrid
{
    private readonly Dictionary<Vector3I, float> _data;

    public RegularUniformFrame OrientationSystem { get;}

    //TODO : move from float to object[] or smtgh
    public Dictionary<Vector3I, float>.KeyCollection Points => _data.Keys;

    public Dictionary<Vector3I, float> Data => _data;


    private TriangleGrid(RegularUniformFrame os)
    {
        if (os is not DoubleDeltaTileOrientationSystem)
        {
            throw new ArgumentException("orientationSystem is not a DoubleDeltaTileOrientationSystem");
        }

        OrientationSystem = os;
        _data = new Dictionary<Vector3I, float>();
    }

    /// <summary>
    /// Fills the cells of the grid with values provided by two two-dimensional arrays.
    /// The provided data will be put in the cells corresponding to the numbering of the data.
    /// </summary>
    /// <param name="dataT1"></param>
    /// <param name="dataT2"></param>
    private void FillWithDataFromTwoArray(float[][] dataT1, float[][] dataT2)
    {
        //Before filling the data, make sure the Arrays are of the same size
        int xSize = dataT1.Length;
        int? ySize = null;
        if (dataT2.Length != xSize)
        {
            throw new ArgumentException("The provided array do not have the same size.");
        }

        for (int i = 0; i < xSize; i++)
        {
            if (ySize == null) // 
            {
                if (dataT1[i] != null ^ dataT2[i] != null)
                {
                    // STRICTLY One of the two is not null
                    ySize = dataT1[i] != null ? dataT1[i].Length : dataT2[i].Length;
                }
                else if (dataT1[i] != null && dataT2[i] != null)
                {
                    if (dataT1[i].Length != dataT2[i].Length)
                    {
                        throw new ArgumentException(
                            "The provided arrays do not have the same size at the sub-array #[" + i + "].");
                    }
                }
            }
            else
            {
                if (dataT1[i].Length != ySize || dataT2[i].Length != ySize)
                {
                    throw new ArgumentException("The provided arrays do not have the same size at the sub-array #[" +
                                                i + "].");
                }
            }
        }

        ySize ??= xSize;

        for (var i = 0; i < xSize ; i++)
        {
            var emptyArrayT1 = dataT1[i] == null || dataT1[i].Length == 0;
            var emptyArrayT2 = dataT2[i] == null || dataT2[i].Length == 0;

            for (var j = 0; j < ySize; j++)
            {
                for (var k = 0; k < OrientationSystem.PolygonCount; k++)
                {
                 _data.Add(
                     new Vector3I(i, j, k),
                         k==0 ? (emptyArrayT1 ? 0f : dataT1[i][j]) : (emptyArrayT2 ? 0f : dataT2[i][j]));                   
                }

            }
        }
    }

    public int Size => _data.Count;

    /// <summary>
    /// Builds a triangle grid based on the data packed in a grid array. 
    /// </summary>
    /// <param name="data"></param>
    /// <param name="lowerTri"></param>
    /// <returns></returns>
    public static TriangleGrid BuildFrom(float[][] dataT1, float[][] dataT2,RegularUniformFrame tilingSystem)
    {
        var grid =new TriangleGrid(tilingSystem);
        grid.FillWithDataFromTwoArray(dataT1, dataT2);
        return grid;
    }

    public Vector2D GetCartesianOriginForCellIndex(Vector2I cellIdx)
    {
        return OrientationSystem.LocalToCartesian(new Vector2D(cellIdx.X,cellIdx.Y));
    }

    public Vector3I GetCellCoordFromCartesian(Vector2D cartesianPos)
    {
        if (OrientationSystem == null)
        {
            throw new Exception("No determined Triangle Grid orientation system.");
        }

        var xy = OrientationSystem.GetCell(cartesianPos);
        return new Vector3I(xy.X,xy.Y,OrientationSystem.GetPolygonIndexFromCartesian(cartesianPos,xy));
    }

    public void PrintGridData(bool printCartesian = false,bool InGodotConsole = false)
    {
        Action<String> stringAction = InGodotConsole ? GD.Print : Console.WriteLine;
        
        foreach (KeyValuePair<Vector3I, float> kvp in _data)
        {
            if (printCartesian)
            {
                stringAction.Invoke(string.Format("Key = {0}[0] {2}, Value = {1}", kvp.Key, kvp.Value,
                    TerrainToolPluginHelper.FormatVector2(OrientationSystem.GetCellCentroid(kvp.Key))));
            }
            else
            {
                stringAction.Invoke(string.Format("Key = {0}[0], Value = {1}", kvp.Key, kvp.Value));
                stringAction.Invoke(string.Format("Key = {0}[1], Value = {1}", kvp.Key, kvp.Value));
            }
        }
    }
    
}


