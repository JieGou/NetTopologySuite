﻿using NetTopologySuite.Operation.OverlayNG;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Operation.OverlayNG
{
    [TestFixture]
    public class CoverageUnionTest : GeometryTestCase
    {
        [Test]
        public void TestPolygonsSimple()
        {
            CheckUnion("MULTIPOLYGON (((5 5, 1 5, 5 1, 5 5)), ((5 9, 1 5, 5 5, 5 9)), ((9 5, 5 5, 5 9, 9 5)), ((9 5, 5 1, 5 5, 9 5)))",
                "POLYGON ((1 5, 5 9, 9 5, 5 1, 1 5))");
        }

        [Test]
        public void TestPolygonsConcentricDonuts()
        {
            CheckUnion("MULTIPOLYGON (((1 9, 9 9, 9 1, 1 1, 1 9), (2 8, 8 8, 8 2, 2 2, 2 8)), ((3 7, 7 7, 7 3, 3 3, 3 7), (4 6, 6 6, 6 4, 4 4, 4 6)))",
                "MULTIPOLYGON (((9 1, 1 1, 1 9, 9 9, 9 1), (8 8, 2 8, 2 2, 8 2, 8 8)), ((7 7, 7 3, 3 3, 3 7, 7 7), (4 4, 6 4, 6 6, 4 6, 4 4)))");
        }

        [Test]
        public void TestPolygonsConcentricHalfDonuts()
        {
            CheckUnion("MULTIPOLYGON (((6 9, 1 9, 1 1, 6 1, 6 2, 2 2, 2 8, 6 8, 6 9)), ((6 9, 9 9, 9 1, 6 1, 6 2, 8 2, 8 8, 6 8, 6 9)), ((5 7, 3 7, 3 3, 5 3, 5 4, 4 4, 4 6, 5 6, 5 7)), ((5 4, 5 3, 7 3, 7 7, 5 7, 5 6, 6 6, 6 4, 5 4)))",
                "MULTIPOLYGON (((1 9, 6 9, 9 9, 9 1, 6 1, 1 1, 1 9), (2 8, 2 2, 6 2, 8 2, 8 8, 6 8, 2 8)), ((5 3, 3 3, 3 7, 5 7, 7 7, 7 3, 5 3), (5 4, 6 4, 6 6, 5 6, 4 6, 4 4, 5 4)))");
        }

        [Test]
        public void TestPolygonsNested()
        {
            CheckUnion("GEOMETRYCOLLECTION (POLYGON ((1 9, 9 9, 9 1, 1 1, 1 9), (3 7, 3 3, 7 3, 7 7, 3 7)), POLYGON ((3 7, 7 7, 7 3, 3 3, 3 7)))",
                "POLYGON ((1 1, 1 9, 9 9, 9 1, 1 1))");
        }

        [Test]
        public void TestPolygonsFormingHole()
        {
            CheckUnion("MULTIPOLYGON (((1 1, 4 3, 5 6, 5 9, 1 1)), ((1 1, 9 1, 6 3, 4 3, 1 1)), ((9 1, 5 9, 5 6, 6 3, 9 1)))",
                "POLYGON ((9 1, 1 1, 5 9, 9 1), (6 3, 5 6, 4 3, 6 3))");
        }

        [Test]
        public void TestPolygonsSquareGrid()
        {
            CheckUnion("MULTIPOLYGON (((0 0, 0 25, 25 25, 25 0, 0 0)), ((0 25, 0 50, 25 50, 25 25, 0 25)), ((0 50, 0 75, 25 75, 25 50, 0 50)), ((0 75, 0 100, 25 100, 25 75, 0 75)), ((25 0, 25 25, 50 25, 50 0, 25 0)), ((25 25, 25 50, 50 50, 50 25, 25 25)), ((25 50, 25 75, 50 75, 50 50, 25 50)), ((25 75, 25 100, 50 100, 50 75, 25 75)), ((50 0, 50 25, 75 25, 75 0, 50 0)), ((50 25, 50 50, 75 50, 75 25, 50 25)), ((50 50, 50 75, 75 75, 75 50, 50 50)), ((50 75, 50 100, 75 100, 75 75, 50 75)), ((75 0, 75 25, 100 25, 100 0, 75 0)), ((75 25, 75 50, 100 50, 100 25, 75 25)), ((75 50, 75 75, 100 75, 100 50, 75 50)), ((75 75, 75 100, 100 100, 100 75, 75 75)))",
                "POLYGON ((0 25, 0 50, 0 75, 0 100, 25 100, 50 100, 75 100, 100 100, 100 75, 100 50, 100 25, 100 0, 75 0, 50 0, 25 0, 0 0, 0 25))");
        }

        /**
         * Sequential lines are still noded
         */

        [Test]
        public void TestLinesSequential()
        {
            CheckUnion("MULTILINESTRING ((1 1, 5 1), (9 1, 5 1))",
                "MULTILINESTRING ((1 1, 5 1), (5 1, 9 1))");
        }

        /**
         * Overlapping lines are noded with common portions merged
         */

        [Test]
        public void TestLinesOverlapping()
        {
            CheckUnion("MULTILINESTRING ((1 1, 2 1, 3 1), (4 1, 3 1, 2 1))",
                "MULTILINESTRING ((1 1, 2 1), (2 1, 3 1), (3 1, 4 1))");
        }

        /**
         * A network of lines is dissolved noded at degree > 2 vertices
         */

        [Test]
        public void TestLinesNetwork()
        {
            //<image url="$(ProjectDir)\DocumentImages\LinesNetwork.png"/>
            CheckUnion("MULTILINESTRING ((1 9, 3.1 8, 5 7, 7 8, 9 9), (5 7, 5 3, 4 3, 2 3), (9 5, 7 4, 5 3, 8 1))",
                "MULTILINESTRING ((1 9, 3.1 8), (2 3, 4 3), (3.1 8, 5 7), (4 3, 5 3), (5 3, 5 7), (5 3, 7 4), (5 3, 8 1), (5 7, 7 8), (7 4, 9 5), (7 8, 9 9))");
        }

        private void CheckUnion(string wkt, string wktExpected)
        {
            var coverage = Read(wkt);
            var expected = Read(wktExpected);
            var result = CoverageUnion.Union(coverage);
            CheckEqual(expected, result);
        }
    }
}
