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
            Check(wkt, "POLYGON ((10 20, 20 20, 30 20, 30 10, 20 10, 10 10, 10 20))");
            Check(wkt, "POLYGON ((10 10, 10 20, 30 20, 30 10, 10 10))", true);
        }

        [Test]
        public void TestSimple3()
        {
            //<image url="$(ProjectDir)\DocumentImages\PolygonUnion01_before.png"/>
            const string wkt = "MULTIPOLYGON (((10 20, 20 20, 20 10, 10 10, 10 20)), ((30 10, 20 10, 20 20, 30 20, 30 10)), ((25 30, 30 30, 30 20, 25 20, 25 30)))";
            //<image url="$(ProjectDir)\DocumentImages\PolygonUnion01_after.png"/>
            Check(wkt, "POLYGON ((10 10, 10 20, 20 20, 25 20, 25 30, 30 30, 30 20, 30 10, 20 10, 10 10))");
            //<image url="$(ProjectDir)\DocumentImages\PolygonUnion01_afterSimplify.png"/>
            Check(wkt, "POLYGON ((10 10, 10 20, 25 20, 25 30, 30 30, 30 10, 10 10))",true);
        }

        [Test]
        public void TestDisjoint()
        {
            Check(
                "MULTIPOLYGON (((10 20, 20 20, 20 10, 10 10, 10 20)), ((30 20, 40 20, 40 10, 30 10, 30 20)))",
                "MULTIPOLYGON (((10 20, 20 20, 20 10, 10 10, 10 20)), ((30 20, 40 20, 40 10, 30 10, 30 20)))");
        }
        //<image url="$(ProjectDir)\DocumentImages\SegmentCollinearUnionError.png"/>
        [Test]
        public void TestSegmentCollinearUnion()
        {
            Check(
                "MULTIPOLYGON (((45.78178 42.07139, 46.52059 23.00464, 23.13876 32.82088, 36.9979 43.31064, 45.78178 42.07139)), ((69.92279 38.66554, 36.99791 43.31064, 45.48486 49.73426, 44.88126 65.31143, 69.92279 38.66554)))",
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
