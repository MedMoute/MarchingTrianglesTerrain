using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles;

namespace UnitTests;

public class TestTriangleProcessing
{
    [Test]
    // TODO Asserts
    public void TestBasicTriangleProcessing()
    {
        HexTerrainCell.a = (float)0.5;
        HexTerrainCell.theta = 0;
        // Test 1 :
        //       * B
        //   A=O *  * C

        Vector3 A = Vector3.Up;
        Vector3 B = Vector3.Back;
        Vector3 C = Vector3.Right;
        Vector3[] triangle = new Vector3[3];
        triangle[0] = A;
        triangle[1] = B;
        triangle[2] = C;
        //

        var res = HexTerrainCell.ProcessTriangle(triangle, 6);

        for (int i = 0; i < res.Count; i++)
        {
            var c = res[i];
            Console.WriteLine("Triangle {0}:  [{1},{2},{3}]", i,c.Points[0],c.Points[1],c.Points[2]);
            foreach (var tuple in c.GetBorderEdges())
            {
                Console.WriteLine("Edge :  [{0},{1}]", tuple.Item1, tuple.Item2);
            }

        }

        Console.WriteLine();
        foreach (var tuple in res.SelectMany(t => t.GetBorderEdges()))
        {
            Console.WriteLine("Edge :  [{0},{1}]", tuple.Item1, tuple.Item2);
        }
        
        // Test is manifold :  
        // Map all the Edges and get their count : should be 2 except for edges in that are in GetBorderEdges,
        // in chich case there should be 2

        var dico = new Dictionary<float[], int>(new FloatArrayComparer());
        var z = 0;
        foreach (var tInfo in res)
        {
            foreach (var edge in tInfo.GetEdges())
            {

                z++;
                var set = new SortedSet<Vector3>(new V3Comp());
                set.Add(edge.Item1);
                set.Add(edge.Item2);
                float[] zob = set.SelectMany(v=>Array.AsReadOnly([v.X, v.Y, v.Z])).ToArray();
                var exists = dico.TryGetValue(zob, out int val);
                if (exists)
                {
                    dico.Remove(zob);
                }
                dico.Add(zob, val + 1);
            }
        }
        Console.WriteLine(z);
        var borderEdgesAsSets = new List<float[]>();
        foreach (var edge in res.SelectMany(t => t.GetBorderEdges()))
        {
            var set = new SortedSet<Vector3>(new V3Comp());
            set.Add(edge.Item1);
            set.Add(edge.Item2);
            borderEdgesAsSets.Add(set.SelectMany(v=>Array.AsReadOnly([v.X, v.Y, v.Z])).ToArray());
        }

        foreach (var kvp in dico)
        {
            Assert.That( 
                kvp.Value == 2 || kvp.Value == 1 && borderEdgesAsSets.Any(o => o.SequenceEqual(kvp.Key)),Is.True);
        }
    }

    public class V3Comp : IComparer<Vector3>
    {
        public int Compare(Vector3 x, Vector3 y)
        {
            var xComparison = x.X.CompareTo(y.X);
            if (xComparison != 0) return xComparison;
            var yComparison = x.Y.CompareTo(y.Y);
            if (yComparison != 0) return yComparison;
            return x.Z.CompareTo(y.Z);
        }
    }
    
    class FloatArrayComparer : IEqualityComparer<float[]>
    {
        public bool Equals(float[]? x, float[]? y)
        {
            if (x?.Length != y?.Length)
                return false;
            for (int i = 0; i < x?.Length; i++)
            {
                if ( MathF.Abs(x[i] - y[i]) > 1e-5)
                    return false;
            }
            return true;
        }

        public int GetHashCode(float[] obj)
        {
            var v = 31;
            for (int i = 0; i < obj.Length; i++)
            {
                v = v*31 * HashCode.Combine(obj[i]) + 7;
            }
            return v;
        }
    }
}