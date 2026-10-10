using System.Collections;
using Godot;
using MarchingTrianglesTerrain.addons.marchingTriangles.@internal;

namespace UnitTests.test.utils;

public class TestUnorderedTupleComparers
{
    
    public static (Vector3I,Vector3I) v3Zero = (Vector3I.Zero, Vector3I.Zero) ;
    public static (Vector3I,Vector3I) v3ZeroOne = (Vector3I.Zero, Vector3I.One) ;
    public static (Vector3I,Vector3I) v3OneZero = (Vector3I.Zero, Vector3I.One) ;
    public static (Vector3I,Vector3I) v3One = (Vector3I.One, Vector3I.One) ;
        
    public static (Vector3I,Vector3I) v3UZero = (Vector3I.Up, Vector3I.Zero) ;
    public static (Vector3I,Vector3I) v3RZero = (Vector3I.Right, Vector3I.Zero) ;
    public static (Vector3I,Vector3I) v3FZero = (Vector3I.Forward, Vector3I.Zero) ;
        
    public static (Vector3I,Vector3I) v3UOne = (Vector3I.Up, Vector3I.One) ;
    public static (Vector3I,Vector3I) v3ROne = (Vector3I.Right, Vector3I.One) ;
    public static (Vector3I,Vector3I) v3FOne = (Vector3I.Forward, Vector3I.One) ;
        
    public static (Vector3I,Vector3I) v3ZeroU = (Vector3I.Zero, Vector3I.Up) ;
    public static (Vector3I,Vector3I) v3ZeroR = (Vector3I.Zero, Vector3I.Right) ;
    public static (Vector3I,Vector3I) v3ZeroF = (Vector3I.Zero, Vector3I.Forward) ;
        
    public static (Vector3I,Vector3I) v3OneU = (Vector3I.One, Vector3I.Up) ;
    public static (Vector3I,Vector3I) v3OneR = (Vector3I.One, Vector3I.Right) ;
    public static (Vector3I,Vector3I) v3OneF = (Vector3I.One, Vector3I.Forward) ;
    
    public static (int,int) vZero = (0, 0) ;
    public static (int,int) vOne = (1, 1) ;
    public static (int,int) vZeroOne = (0, 1) ;
    public static (int,int) vOneZero = (1, 0) ;
    public static (int,int) vMOneZero = (-1, 0) ;
    public static (int,int) vZeroMOne = (0, -1) ;
    public static (int,int) vMOneOne = (-1, 1) ;
    public static (int,int) vOneMOne = (1, -1) ;
    
    [TestCase]
    public void TestUnorderedIntTupleComparerEquals()
    {
        var comparer = UnorderedValueTupleComparer.Instance;

        Assert.That(comparer.Equals(vZero,vZero), Is.True);
        Assert.That(comparer.Equals(vZero,vOne), Is.False);
        Assert.That(comparer.Equals(vZero,vZeroOne), Is.False);
        Assert.That(comparer.Equals(vZero,vOneZero), Is.False);
        Assert.That(comparer.Equals(vZero,vMOneZero), Is.False);
        Assert.That(comparer.Equals(vZero,vZeroMOne), Is.False);
        Assert.That(comparer.Equals(vZero,vMOneOne), Is.False);
        Assert.That(comparer.Equals(vZero,vOneMOne), Is.False);
        
        Assert.That(comparer.Equals(vOne,vZero), Is.False);
        Assert.That(comparer.Equals(vOne,vOne), Is.True);
        Assert.That(comparer.Equals(vOne,vZeroOne), Is.False);
        Assert.That(comparer.Equals(vOne,vOneZero), Is.False);
        Assert.That(comparer.Equals(vOne,vMOneZero), Is.False);
        Assert.That(comparer.Equals(vOne,vZeroMOne), Is.False);
        Assert.That(comparer.Equals(vOne,vMOneOne), Is.False);
        Assert.That(comparer.Equals(vOne,vOneMOne), Is.False);
        
        Assert.That(comparer.Equals(vZeroOne,vZero), Is.False);
        Assert.That(comparer.Equals(vZeroOne,vOne), Is.False);
        Assert.That(comparer.Equals(vZeroOne,vZeroOne), Is.True);
        Assert.That(comparer.Equals(vZeroOne,vOneZero), Is.True);
        Assert.That(comparer.Equals(vZeroOne,vMOneZero), Is.False);
        Assert.That(comparer.Equals(vZeroOne,vZeroMOne), Is.False);
        Assert.That(comparer.Equals(vZeroOne,vMOneOne), Is.False);
        Assert.That(comparer.Equals(vZeroOne,vOneMOne), Is.False);
        
        Assert.That(comparer.Equals(vOneZero,vZero), Is.False);
        Assert.That(comparer.Equals(vOneZero,vOne), Is.False);
        Assert.That(comparer.Equals(vOneZero,vZeroOne), Is.True);
        Assert.That(comparer.Equals(vOneZero,vOneZero), Is.True);
        Assert.That(comparer.Equals(vOneZero,vMOneZero), Is.False);
        Assert.That(comparer.Equals(vOneZero,vZeroMOne), Is.False);
        Assert.That(comparer.Equals(vOneZero,vMOneOne), Is.False);
        Assert.That(comparer.Equals(vOneZero,vOneMOne), Is.False);
        
        Assert.That(comparer.Equals(vMOneZero,vZero), Is.False);
        Assert.That(comparer.Equals(vMOneZero,vOne), Is.False);
        Assert.That(comparer.Equals(vMOneZero,vZeroOne), Is.False);
        Assert.That(comparer.Equals(vMOneZero,vOneZero), Is.False);
        Assert.That(comparer.Equals(vMOneZero,vMOneZero), Is.True);
        Assert.That(comparer.Equals(vMOneZero,vZeroMOne), Is.True);
        Assert.That(comparer.Equals(vMOneZero,vMOneOne), Is.False);
        Assert.That(comparer.Equals(vMOneZero,vOneMOne), Is.False);
        
        Assert.That(comparer.Equals(vZeroMOne,vZero), Is.False);
        Assert.That(comparer.Equals(vZeroMOne,vOne), Is.False);
        Assert.That(comparer.Equals(vZeroMOne,vZeroOne), Is.False);
        Assert.That(comparer.Equals(vZeroMOne,vOneZero), Is.False);
        Assert.That(comparer.Equals(vZeroMOne,vMOneZero), Is.True);
        Assert.That(comparer.Equals(vZeroMOne,vZeroMOne), Is.True);
        Assert.That(comparer.Equals(vZeroMOne,vMOneOne), Is.False);
        Assert.That(comparer.Equals(vZeroMOne,vOneMOne), Is.False);
        
        Assert.That(comparer.Equals(vMOneOne,vZero), Is.False);
        Assert.That(comparer.Equals(vMOneOne,vOne), Is.False);
        Assert.That(comparer.Equals(vMOneOne,vZeroOne), Is.False);
        Assert.That(comparer.Equals(vMOneOne,vOneZero), Is.False);
        Assert.That(comparer.Equals(vMOneOne,vMOneZero), Is.False);
        Assert.That(comparer.Equals(vMOneOne,vZeroMOne), Is.False);
        Assert.That(comparer.Equals(vMOneOne,vMOneOne), Is.True);
        Assert.That(comparer.Equals(vMOneOne,vOneMOne), Is.True);
        
        Assert.That(comparer.Equals(vOneMOne,vZero), Is.False);
        Assert.That(comparer.Equals(vOneMOne,vOne), Is.False);
        Assert.That(comparer.Equals(vOneMOne,vZeroOne), Is.False);
        Assert.That(comparer.Equals(vOneMOne,vOneZero), Is.False);
        Assert.That(comparer.Equals(vOneMOne,vMOneZero), Is.False);
        Assert.That(comparer.Equals(vOneMOne,vZeroMOne), Is.False);
        Assert.That(comparer.Equals(vOneMOne,vMOneOne), Is.True);
        Assert.That(comparer.Equals(vOneMOne,vOneMOne), Is.True);
    }
    
    [TestCase]
    public void TestUnorderedIntTupleComparerCompare()
    {
        var comparer = UnorderedValueTupleComparer.Instance;

        Assert.That(comparer.Compare(vZero,vZero), Is.Zero);
        Assert.That(comparer.Compare(vZero,vOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vZero,vZeroOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vZero,vOneZero), Is.LessThan(0));
        Assert.That(comparer.Compare(vZero,vMOneZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vZero,vZeroMOne), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vZero,vMOneOne), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vZero,vOneMOne), Is.GreaterThan(0));
        
        Assert.That(comparer.Compare(vOne,vZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vOne,vOne), Is.Zero);
        Assert.That(comparer.Compare(vOne,vZeroOne), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vOne,vOneZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vOne,vMOneZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vOne,vZeroMOne), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vOne,vMOneOne), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vOne,vOneMOne), Is.GreaterThan(0));
        
        Assert.That(comparer.Compare(vZeroOne,vZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vZeroOne,vOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vZeroOne,vZeroOne), Is.Zero);
        Assert.That(comparer.Compare(vZeroOne,vOneZero), Is.Zero);
        Assert.That(comparer.Compare(vZeroOne,vMOneZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vZeroOne,vZeroMOne), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vZeroOne,vMOneOne), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vZeroOne,vOneMOne), Is.GreaterThan(0));

        Assert.That(comparer.Compare(vOneZero,vZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vOneZero,vOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vOneZero,vZeroOne), Is.Zero);
        Assert.That(comparer.Compare(vOneZero,vOneZero), Is.Zero);
        Assert.That(comparer.Compare(vOneZero,vMOneZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vOneZero,vZeroMOne), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vOneZero,vMOneOne), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vOneZero,vOneMOne), Is.GreaterThan(0));
        
                
        Assert.That(comparer.Compare(vMOneZero,vZero), Is.LessThan(0));
        Assert.That(comparer.Compare(vMOneZero,vOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vMOneZero,vZeroOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vMOneZero,vOneZero), Is.LessThan(0));
        Assert.That(comparer.Compare(vMOneZero,vMOneZero), Is.Zero);
        Assert.That(comparer.Compare(vMOneZero,vZeroMOne), Is.Zero);
        Assert.That(comparer.Compare(vMOneZero,vMOneOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vMOneZero,vOneMOne), Is.LessThan(0));
        
        Assert.That(comparer.Compare(vZeroMOne,vZero), Is.LessThan(0));
        Assert.That(comparer.Compare(vZeroMOne,vOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vZeroMOne,vZeroOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vZeroMOne,vOneZero), Is.LessThan(0));
        Assert.That(comparer.Compare(vZeroMOne,vMOneZero), Is.Zero);
        Assert.That(comparer.Compare(vZeroMOne,vZeroMOne), Is.Zero);
        Assert.That(comparer.Compare(vZeroMOne,vMOneOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vZeroMOne,vOneMOne), Is.LessThan(0));

        Assert.That(comparer.Compare(vMOneOne,vZero), Is.LessThan(0));
        Assert.That(comparer.Compare(vMOneOne,vOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vMOneOne,vZeroOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vMOneOne,vOneZero), Is.LessThan(0));
        Assert.That(comparer.Compare(vMOneOne,vMOneZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vMOneOne,vZeroMOne), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vMOneOne,vMOneOne), Is.Zero);
        Assert.That(comparer.Compare(vMOneOne,vOneMOne), Is.Zero);
        
        Assert.That(comparer.Compare(vOneMOne,vZero), Is.LessThan(0));
        Assert.That(comparer.Compare(vOneMOne,vOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vOneMOne,vZeroOne), Is.LessThan(0));
        Assert.That(comparer.Compare(vOneMOne,vOneZero), Is.LessThan(0));
        Assert.That(comparer.Compare(vOneMOne,vMOneZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vOneMOne,vZeroMOne), Is.GreaterThan(0));
        Assert.That(comparer.Compare(vOneMOne,vMOneOne), Is.Zero);
        Assert.That(comparer.Compare(vOneMOne,vOneMOne), Is.Zero);
    }

    [TestCase]
    public void TestUnorderedV3TupleComparerEquals()
    {
        var comparer = UnorderedV3TupleComparer.Instance;
        
        Assert.That(comparer.Equals(v3Zero,v3Zero), Is.True);
        Assert.That(comparer.Equals(v3Zero,v3One), Is.False);
        Assert.That(comparer.Equals(v3Zero,v3ZeroOne), Is.False);
        Assert.That(comparer.Equals(v3Zero,v3OneZero), Is.False);
        
        Assert.That(comparer.Equals(v3One,v3Zero), Is.False);
        Assert.That(comparer.Equals(v3One,v3One), Is.True);
        Assert.That(comparer.Equals(v3One,v3ZeroOne), Is.False);
        Assert.That(comparer.Equals(v3One,v3OneZero), Is.False);
        
        Assert.That(comparer.Equals(v3ZeroOne,v3Zero), Is.False);
        Assert.That(comparer.Equals(v3ZeroOne,v3One), Is.False);
        Assert.That(comparer.Equals(v3ZeroOne,v3ZeroOne), Is.True);
        Assert.That(comparer.Equals(v3ZeroOne,v3OneZero), Is.True); 
        
        Assert.That(comparer.Equals(v3OneZero,v3Zero), Is.False);
        Assert.That(comparer.Equals(v3OneZero,v3One), Is.False);
        Assert.That(comparer.Equals(v3OneZero,v3ZeroOne), Is.True);
        Assert.That(comparer.Equals(v3OneZero,v3OneZero), Is.True);  
        
        //Component per component check of separation
        Assert.That(comparer.Equals(v3FZero,v3FZero), Is.True);
        Assert.That(comparer.Equals(v3FZero,v3RZero), Is.False);
        Assert.That(comparer.Equals(v3FZero,v3UZero), Is.False);
        Assert.That(comparer.Equals(v3FZero,v3ZeroF), Is.True);
        Assert.That(comparer.Equals(v3FZero,v3ZeroR), Is.False);
        Assert.That(comparer.Equals(v3FZero,v3ZeroU), Is.False);
        Assert.That(comparer.Equals(v3FZero,v3FOne), Is.False);
        Assert.That(comparer.Equals(v3FZero,v3ROne), Is.False);
        Assert.That(comparer.Equals(v3FZero,v3UOne), Is.False);
        Assert.That(comparer.Equals(v3FZero,v3OneF), Is.False);
        Assert.That(comparer.Equals(v3FZero,v3OneR), Is.False);
        Assert.That(comparer.Equals(v3FZero,v3OneU), Is.False);
        
        Assert.That(comparer.Equals(v3RZero,v3FZero), Is.False);
        Assert.That(comparer.Equals(v3RZero,v3RZero), Is.True);
        Assert.That(comparer.Equals(v3RZero,v3UZero), Is.False);
        Assert.That(comparer.Equals(v3RZero,v3ZeroF), Is.False);
        Assert.That(comparer.Equals(v3RZero,v3ZeroR), Is.True);
        Assert.That(comparer.Equals(v3RZero,v3ZeroU), Is.False);
        Assert.That(comparer.Equals(v3RZero,v3FOne), Is.False);
        Assert.That(comparer.Equals(v3RZero,v3ROne), Is.False);
        Assert.That(comparer.Equals(v3RZero,v3UOne), Is.False);
        Assert.That(comparer.Equals(v3RZero,v3OneF), Is.False);
        Assert.That(comparer.Equals(v3RZero,v3OneR), Is.False);
        Assert.That(comparer.Equals(v3RZero,v3OneU), Is.False);
        
        Assert.That(comparer.Equals(v3UZero,v3FZero), Is.False);
        Assert.That(comparer.Equals(v3UZero,v3RZero), Is.False);
        Assert.That(comparer.Equals(v3UZero,v3UZero), Is.True);
        Assert.That(comparer.Equals(v3UZero,v3ZeroF), Is.False);
        Assert.That(comparer.Equals(v3UZero,v3ZeroR), Is.False);
        Assert.That(comparer.Equals(v3UZero,v3ZeroU), Is.True);
        Assert.That(comparer.Equals(v3UZero,v3FOne), Is.False);
        Assert.That(comparer.Equals(v3UZero,v3ROne), Is.False);
        Assert.That(comparer.Equals(v3UZero,v3UOne), Is.False);
        Assert.That(comparer.Equals(v3UZero,v3OneF), Is.False);
        Assert.That(comparer.Equals(v3UZero,v3OneR), Is.False);
        Assert.That(comparer.Equals(v3UZero,v3OneU), Is.False);
    }
    
        [TestCase]
    public void TestUnorderedV3TupleComparerCompare()
    {
        var comparer = UnorderedV3TupleComparer.Instance;


        
        Assert.That(comparer.Compare(v3Zero,v3Zero), Is.Zero);
        Assert.That(comparer.Compare(v3Zero,v3One), Is.LessThan(0));
        Assert.That(comparer.Compare(v3Zero,v3ZeroOne), Is.LessThan(0));
        Assert.That(comparer.Compare(v3Zero,v3OneZero), Is.LessThan(0));
        
        Assert.That(comparer.Compare(v3One,v3Zero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(v3One,v3One), Is.Zero);
        Assert.That(comparer.Compare(v3One,v3ZeroOne), Is.GreaterThan(0));
        Assert.That(comparer.Compare(v3One,v3OneZero), Is.GreaterThan(0));
        
        Assert.That(comparer.Compare(v3ZeroOne,v3Zero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(v3ZeroOne,v3One), Is.LessThan(0));
        Assert.That(comparer.Compare(v3ZeroOne,v3ZeroOne), Is.Zero);
        Assert.That(comparer.Compare(v3ZeroOne,v3OneZero), Is.Zero); 
        
        Assert.That(comparer.Compare(v3OneZero,v3Zero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(v3OneZero,v3One), Is.LessThan(0));
        Assert.That(comparer.Compare(v3OneZero,v3ZeroOne), Is.Zero);
        Assert.That(comparer.Compare(v3OneZero,v3OneZero), Is.Zero);  
        
        //Component per component check of separation
        Assert.That(comparer.Compare(v3FZero,v3FZero), Is.Zero);
        Assert.That(comparer.Compare(v3FZero,v3RZero), Is.LessThan(0));
        Assert.That(comparer.Compare(v3FZero,v3UZero), Is.LessThan(0));
        Assert.That(comparer.Compare(v3FZero,v3ZeroF), Is.Zero);
        Assert.That(comparer.Compare(v3FZero,v3ZeroR), Is.LessThan(0));
        Assert.That(comparer.Compare(v3FZero,v3ZeroU), Is.LessThan(0));
        Assert.That(comparer.Compare(v3FZero,v3FOne), Is.LessThan(0));
        Assert.That(comparer.Compare(v3FZero,v3ROne), Is.LessThan(0));
        Assert.That(comparer.Compare(v3FZero,v3UOne), Is.LessThan(0));
        Assert.That(comparer.Compare(v3FZero,v3OneF), Is.LessThan(0));
        Assert.That(comparer.Compare(v3FZero,v3OneR), Is.LessThan(0));
        Assert.That(comparer.Compare(v3FZero,v3OneU), Is.LessThan(0));
        
        Assert.That(comparer.Compare(v3RZero,v3FZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(v3RZero,v3RZero), Is.Zero);
        Assert.That(comparer.Compare(v3RZero,v3UZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(v3RZero,v3ZeroF), Is.GreaterThan(0));
        Assert.That(comparer.Compare(v3RZero,v3ZeroR), Is.Zero);
        Assert.That(comparer.Compare(v3RZero,v3ZeroU), Is.GreaterThan(0));
        Assert.That(comparer.Compare(v3RZero,v3FOne), Is.LessThan(0));
        Assert.That(comparer.Compare(v3RZero,v3ROne), Is.LessThan(0));
        Assert.That(comparer.Compare(v3RZero,v3UOne), Is.LessThan(0));
        Assert.That(comparer.Compare(v3RZero,v3OneF), Is.LessThan(0));
        Assert.That(comparer.Compare(v3RZero,v3OneR), Is.LessThan(0));
        Assert.That(comparer.Compare(v3RZero,v3OneU), Is.LessThan(0));
        
        Assert.That(comparer.Compare(v3UZero,v3FZero), Is.GreaterThan(0));
        Assert.That(comparer.Compare(v3UZero,v3RZero), Is.LessThan(0));
        Assert.That(comparer.Compare(v3UZero,v3UZero), Is.Zero);
        Assert.That(comparer.Compare(v3UZero,v3ZeroF), Is.GreaterThan(0));
        Assert.That(comparer.Compare(v3UZero,v3ZeroR), Is.LessThan(0));
        Assert.That(comparer.Compare(v3UZero,v3ZeroU), Is.Zero);
        Assert.That(comparer.Compare(v3UZero,v3FOne), Is.LessThan(0));
        Assert.That(comparer.Compare(v3UZero,v3ROne), Is.LessThan(0));
        Assert.That(comparer.Compare(v3UZero,v3UOne), Is.LessThan(0));
        Assert.That(comparer.Compare(v3UZero,v3OneF), Is.LessThan(0));
        Assert.That(comparer.Compare(v3UZero,v3OneR), Is.LessThan(0));
        Assert.That(comparer.Compare(v3UZero,v3OneU), Is.LessThan(0));

    }
}