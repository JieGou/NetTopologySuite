using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Simplify;
using NUnit.Framework;
using System;

namespace NetTopologySuite.Tests.NUnit.Simplify
{
    [TestFixture]
    public class TopologyPreservingSimplifierTest : GeometryTestCase
    {
        [Test]
        public void TestPoint()
        {
            CheckTPSNoChange("POINT (10 10)", 1);
        }

        [Test]
        public void TestPolygonEmpty()
        {
            CheckTPSNoChange("POLYGON(EMPTY)", 1);
        }

        //<image url="$(ProjectDir)\DocumentImages\MultiPolygonWithSmallComponents.png"/>
        /// <summary>
        /// TestMultiPolygonWithSmallComponents
        /// </summary>
        /// <remarks>
        /// Test is from http://postgis.refractions.net/pipermail/postgis-users/2008-April/019327.html
        /// <para/>
        /// Exhibits the issue where simplified polygon shells can "jump" across
        /// holes, causing invalid topology.
        /// </remarks>
        [Test, Ignore("Known to fail")]
        public void TestMultiPolygonWithSmallComponents()
        {
            CheckTPS("POLYGON ((20 220, 40 220, 60 220, 80 220, 100 220, 120 220, 140 220, 140 180, 100 180, 60 180,     20 180, 20 220))",
        10,
        "POLYGON ((20 220, 140 220, 140 180, 20 180, 20 220))");
        }

        [Test]
        public void TestPolygonNoReduction()
        {
            CheckTPSNoChange("POLYGON ((20 220, 140 220, 140 180, 20 180, 20 220))",
            10);
        }

        [Test]
        public void TestPolygonNoReductionWithConflicts()
        {
            CheckTPSNoChange("POLYGON ((40 240, 160 241, 280 240, 280 160, 160 240, 40 140, 40 240))",
            10);
        }

        [Test]
        public void TestPolygonWithTouchingHole()
        {
            CheckTPS("POLYGON ((80 200, 240 200, 240 60, 80 60, 80 200), (120 120, 220 120, 180 199, 160 200, 140 199, 120 120))",
            10,
            "POLYGON ((80 200, 240 200, 240 60, 80 60, 80 200), (120 120, 220 120, 180 199, 160 200, 140 199, 120 120))");
        }

        [Test]
        public void TestFlattishPolygon()
        {
            CheckTPS("POLYGON ((0 0, 50 0, 53 0, 55 0, 100 0, 70 1,  60 1, 50 1, 40 1, 0 0))",
            10,
            "POLYGON ((0 0, 50 0, 100 0, 70 1, 0 0))");
        }

        [Test]
        public void TestPolygonWithFlattishHole()
        {
            CheckTPS("POLYGON ((0 0, 0 200, 200 200, 200 0, 0 0), (140 40, 90 95, 40 160, 95 100, 140 40))",
            20,
            "POLYGON ((0 0, 0 200, 200 200, 200 0, 0 0), (140 40, 90 95, 40 160, 95 100, 140 40))");
        }

        [Test]
        public void TestTinySquare()
        {
            CheckTPS("POLYGON ((0 5, 5 5, 5 0, 0 0, 0 1, 0 5))",
            10,
            "POLYGON ((0 0, 5 5, 5 0, 0 0))");
        }

        [Test]
        public void TestTinyLineString()
        {
            CheckTPS("LINESTRING (0 5, 1 5, 2 5, 5 5)",
            10,
            "LINESTRING (0 5, 5 5)");
        }

        [Test]
        public void TestTinyClosedLineString()
        {
            CheckTPSNoChange("LINESTRING (0 0, 5 0, 5 5, 0 0)",
            10);
        }

        [Test]
        public void TestMultiPoint()
        {
            CheckTPSNoChange("MULTIPOINT(80 200, 240 200, 240 60, 80 60, 80 200, 140 199, 120 120)",
            10);
        }

        [Test]
        public void TestMultiLineString()
        {
            CheckTPS("MULTILINESTRING( (0 0, 50 0, 70 0, 80 0, 100 0), (0 0, 50 1, 60 1, 100 0) )",
            10,
            "MULTILINESTRING ((0 0, 100 0), (0 0, 50 1, 100 0))");
        }

        [Test]
        public void TestMultiLineStringWithEmpty()
        {
            CheckTPS("MULTILINESTRING(EMPTY, (0 0, 50 0, 70 0, 80 0, 100 0), (0 0, 50 1, 60 1, 100 0) )",
            10,
            "MULTILINESTRING ((0 0, 100 0), (0 0, 50 1, 100 0))");
        }

        [Test]
        public void TestMultiPolygonWithEmpty()
        {
            CheckTPS("MULTIPOLYGON (EMPTY, ((10 90, 10 10, 90 10, 50 60, 10 90)), ((70 90, 90 90, 90 70, 70 70, 70 90)))",
            10,
            "MULTIPOLYGON (((10 90, 10 10, 90 10, 50 60, 10 90)), ((70 90, 90 90, 90 70, 70 70, 70 90)))");
        }

        [Test]
        public void TestGeometryCollection()
        {
            CheckTPSNoChange("GEOMETRYCOLLECTION (MULTIPOINT (80 200, 240 200, 240 60, 80 60, 80 200, 140 199, 120 120), POLYGON ((80 200, 240 200, 240 60, 80 60, 80 200)), LINESTRING (80 200, 240 200, 240 60, 80 60, 80 200, 140 199, 120 120))",
              10);
        }

        [Test]
        public void TestNoCollapse_mL()
        {
            CheckTPS(
          "MULTILINESTRING ((0 0, 100 0), (0 0, 60 1, 100 0))",
            10.0,
            "MULTILINESTRING ((0 0, 100 0), (0 0, 60 1, 100 0))"
            );
        }

        [Test]
        public void TestNoCollapseMany_mL()
        {
            CheckTPS(
                "MULTILINESTRING ((0 100, 400 100), (0 100, 105 122, 245 116, 280 110, 330 120, 400 100), (0 100, 155 79, 270 90, 350 70, 400 100), (0 100, 110 130, 205 138, 330 130, 400 100))",
                100.0,
                "MULTILINESTRING ((0 100, 400 100), (0 100, 105 122, 400 100), (0 100, 350 70, 400 100), (0 100, 110 130, 205 138, 400 100))"
            );
        }

        [Test]
        public void TestNoCollapseSmallSquare()
        {
            CheckTPS(
          "POLYGON ((0 5, 5 5, 5 0, 0 0, 0 1, 0 5))",
            100,
            "POLYGON ((0 0, 5 5, 5 0, 0 0))"
            );
        }

        [Test]
        public void TestPolygonRemoveEndpoint()
        {
            CheckTPS(
          "POLYGON ((220 180, 261 175, 380 220, 300 40, 140 30, 30 220, 176 176, 220 180))",
            40,
            "POLYGON ((30 220, 380 220, 300 40, 140 30, 30 220))"
            );
        }

        [Test]
        public void TestLinearRingRemoveEndpoint()
        {
            CheckTPS(
          "LINEARRING (220 180, 261 175, 380 220, 300 40, 140 30, 30 220, 176 176, 220 180)",
            40,
            "LINEARRING (30 220, 380 220, 300 40, 140 30, 30 220)"
            );
        }

        [Test]
        public void TestPolygonKeepEndpointWithCross()
        {
            CheckTPS(
          "POLYGON ((50 52, 60 50, 90 60, 90 10, 10 10, 10 90, 60 90, 50 55, 40 80, 20 60, 40 50, 50 52))",
            10,
            "POLYGON ((20 60, 50 52, 90 60, 90 10, 10 10, 10 90, 60 90, 50 55, 40 80, 20 60))"
            );
        }

        // see https://trac.osgeo.org/geos/ticket/1064
        [Test]
        public void TestPolygonRemoveFlatEndpoint()
        {
            CheckTPS(
          "POLYGON ((42 42, 0 42, 0 100, 42 100, 100 42, 42 42))",
            1,
            "POLYGON ((100 42, 0 42, 0 100, 42 100, 100 42))"
            );
        }

        /**
         * Test is from http://lists.jump-project.org/pipermail/jts-devel/2008-February/002350.html
         */
        [Test]
        public void TestPolygonWithSpike()
        {
            CheckTPS("POLYGON ((3312459.605 6646878.353, 3312460.524 6646875.969, 3312459.427 6646878.421, 3312460.014 6646886.391, 3312465.889 6646887.398, 3312470.827 6646884.839, 3312475.4 6646878.027, 3312477.289 6646871.694, 3312472.748 6646869.547, 3312468.253 6646874.01, 3312463.52 6646875.779, 3312459.605 6646878.353))",
            2,
            "POLYGON ((3312459.605 6646878.353, 3312460.524 6646875.969, 3312459.427 6646878.421, 3312460.014 6646886.391, 3312465.889 6646887.398, 3312470.827 6646884.839, 3312477.289 6646871.694, 3312472.748 6646869.547, 3312459.605 6646878.353))");
        }

        private void CheckTPS(string wkt, double tolerance, string wktExpected)
        {
            var geom = Read(wkt);
            var actual = TopologyPreservingSimplifier.Simplify(geom, tolerance);
            var expected = Read(wktExpected);
            //TODO: add this once the "skipping over rings" problem is fixed
            //CheckValid(actual);
            CheckEqual(expected, actual);
        }

        private void CheckTPSNoChange(string wkt, double tolerance)
        {
            CheckTPS(wkt, tolerance, wkt);
        }
    }

}
