using NetTopologySuite.Operation.Union;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Operation.Union
{
    public class SparsePolygonUnionTest : GeometryTestCase
    {

        [Test]
        public void TestSimple()
        {
            const string wkt = "MULTIPOLYGON (((10 20, 20 20, 20 10, 10 10, 10 20)), ((30 10, 20 10, 20 20, 30 20, 30 10)))";
            Check( wkt, "POLYGON ((10 20, 20 20, 30 20, 30 10, 20 10, 10 10, 10 20))");
            Check(wkt, "POLYGON ((10 10, 10 20, 30 20, 30 10, 10 10))", true);
        }

        [Test]
        public void TestSimple3()
        {
            Check(
                "MULTIPOLYGON (((10 20, 20 20, 20 10, 10 10, 10 20)), ((30 10, 20 10, 20 20, 30 20, 30 10)), ((25 30, 30 30, 30 20, 25 20, 25 30)))",
                "POLYGON ((10 10, 10 20, 20 20, 25 20, 25 30, 30 30, 30 20, 30 10, 20 10, 10 10))");
        }

        [Test]
        public void TestDisjoint()
        {
            Check(
                "MULTIPOLYGON (((10 20, 20 20, 20 10, 10 10, 10 20)), ((30 20, 40 20, 40 10, 30 10, 30 20)))",
                "MULTIPOLYGON (((10 20, 20 20, 20 10, 10 10, 10 20)), ((30 20, 40 20, 40 10, 30 10, 30 20)))");
        }

        private void Check(string wkt, string wktExpected, bool isSimplify = false)
        {
            var geom = Read(wkt);
            var result = SparsePolygonUnion.Union(geom, isSimplify);
            var expected = Read(wktExpected);
            CheckEqual(expected, result);
            TestContext.WriteLine(result);
        }
    }

}
