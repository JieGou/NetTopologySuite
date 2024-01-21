﻿using NetTopologySuite.Simplify;
using NUnit.Framework;

namespace NetTopologySuite.Tests.NUnit.Algorithm.Hull
{
    //Note 可直接从 https://sourceforge.net/projects/jts-topology-suite.mirror/files/1.19.0/JTSTestBuilder.jar/download 下载方便测试
    public class PolygonHullSimplifierTest : GeometryTestCase
    {
        [Test]
        public void TestOuterSimple()
        {
            //<image url="$(ProjectDir)\DocumentImages\CheckHullOuter01_Original.png"/>
            string wkt = "POLYGON ((30 90, 10 40, 40 10, 70 10, 90 30, 80 80, 70 40, 30 40, 50 50, 60 70, 30 90))";
            //<image url="$(ProjectDir)\DocumentImages\CheckHullOuter01_0.png"/>
            CheckHullOuter(wkt, 0, "POLYGON ((30 90, 80 80, 90 30, 70 10, 40 10, 10 40, 30 90))");
            //<image url="$(ProjectDir)\DocumentImages\CheckHullOuter01_0.8.png"/>
            CheckHullOuter(wkt, 0.8, "POLYGON ((30 90, 60 70, 80 80, 90 30, 70 10, 40 10, 10 40, 30 90))");
            //<image url="$(ProjectDir)\DocumentImages\CheckHullOuter01_1.png"/>
            CheckHullOuter(wkt, 1, "POLYGON ((30 90, 10 40, 40 10, 70 10, 90 30, 80 80, 70 40, 30 40, 50 50, 60 70, 30 90))");
        }

        [Test]
        public void TestOuterZGore()
        {
            //<image url="$(ProjectDir)\DocumentImages\CheckHullOuter02_Original.png"/>
            string wkt = "POLYGON ((10 90, 40 60, 20 40, 40 20, 70 50, 40 30, 30 40, 60 70, 50 90, 90 90, 90 10, 10 10, 10 90))";
            //<image url="$(ProjectDir)\DocumentImages\CheckHullOuter02_0.5.png"/>
            CheckHullOuter(wkt, 0.5, "POLYGON ((10 90, 50 90, 90 90, 90 10, 10 10, 10 90))");
            //<image url="$(ProjectDir)\DocumentImages\CheckHullOuter02_0.6.png"/>
            CheckHullOuter(wkt, 0.6, "POLYGON ((10 90, 40 60, 60 70, 50 90, 90 90, 90 10, 10 10, 10 90))");
            //<image url="$(ProjectDir)\DocumentImages\CheckHullOuter02_0.7.png"/>
            CheckHullOuter(wkt, 0.7, "POLYGON ((10 90, 40 60, 30 40, 60 70, 50 90, 90 90, 90 10, 10 10, 10 90))");
        }

        //Note 可用来对多段线的点进行简化
        [Test]
        public void TestOuterFlat()
        {
            //<image url="$(ProjectDir)\DocumentImages\CheckHullOuter03_Original.png"/>
            CheckHullOuter("POLYGON ((10 10, 10 90, 90 90, 90 50, 90 10, 50 10, 10 10))",
            //<image url="$(ProjectDir)\DocumentImages\CheckHullOuter03_0.4.png"/>
                0.4, "POLYGON ((10 10, 10 90, 90 90, 90 10, 10 10))");
        }

        [Test]
        public void TestInner()
        {
            //TODO 理解为什么不包括点 (25,17)
            //<image url="$(ProjectDir)\DocumentImages\CheckHullInner01.png"/>
            CheckHullInner("POLYGON ((11 14, 2 31, 18 29, 25 17, 38 16, 29 5, 19 11, 11 0, 0 10, 11 14))",
                0.5, "POLYGON ((19 11, 29 5, 18 29, 2 31, 19 11))");
        }

        [Test]
        public void TestOuterWithHole()
        {
            CheckHullOuter("POLYGON ((50 100, 30 70, 0 50, 30 30, 50 0, 70 30, 100 50, 70 70, 50 100), (50 75, 40 50, 10 50, 36 35, 50 5, 65 35, 90 50, 60 60, 50 75))",
                0.1, "POLYGON ((50 100, 100 50, 50 0, 0 50, 50 100), (36 35, 50 5, 60 60, 36 35))");
        }

        [Test]
        public void TestInnerWithHoles()
        {
            CheckHullInner("POLYGON ((70 300, 237 395, 145 296, 251 295, 320 40, 190 20, 60 60, 100 180, 70 300), (90 270, 100 220, 128 255, 180 270, 90 270), (110 160, 90 80, 180 90, 150 100, 110 160), (250 210, 160 200, 224 185, 250 160, 250 210))",
                0.1, "POLYGON ((70 300, 100 180, 60 60, 320 40, 251 295, 145 296, 70 300), (90 270, 180 270, 100 220, 90 270), (110 160, 180 90, 90 80, 110 160), (250 210, 250 160, 160 200, 250 210))");
        }

        [Test]
        public void TestInnerMultiWithHoles()
        {
            CheckHullInner("MULTIPOLYGON (((70 300, 237 395, 145 296, 251 295, 320 40, 190 20, 60 60, 100 180, 70 300), (90 270, 100 220, 128 255, 180 270, 90 270), (110 160, 90 80, 180 90, 150 100, 110 160), (250 210, 160 200, 224 185, 250 160, 250 210)), ((290 370, 310 200, 385 123, 437 188, 440 190, 440 290, 400 370, 350 360, 340 310, 290 370), (357 267, 415 242, 389.5 234, 376 216, 357 267), (370 340, 360 280, 380 310, 400 300, 370 340)))",
                0.1, "MULTIPOLYGON (((70 300, 100 180, 60 60, 320 40, 251 295, 145 296, 70 300), (90 270, 180 270, 100 220, 90 270), (110 160, 180 90, 90 80, 110 160), (250 210, 250 160, 160 200, 250 210)), ((310 200, 437 188, 400 370, 350 360, 340 310, 310 200), (357 267, 415 242, 376 216, 357 267), (370 340, 400 300, 360 280, 370 340)))");
        }

        [Test]
        public void TestOuterMultiWithHoles()
        {
            CheckHullOuter("MULTIPOLYGON (((50 50, 50 250, 100 253, 100 250, 100 300, 300 300, 200 200, 300 150, 300 50, 50 50), (180 200, 70 200, 70 70, 200 100, 280 70, 200 150, 180 200)), ((90 180, 160 180, 160 100, 125 139, 100 100, 90 180)), ((380 280, 310 280, 250 200, 310 230, 350 150, 380 280)))",
                0.1, "MULTIPOLYGON (((50 50, 50 250, 100 300, 300 300, 200 200, 300 150, 300 50, 50 50), (180 200, 70 200, 70 70, 200 100, 180 200)), ((90 180, 160 180, 160 100, 100 100, 90 180)), ((380 280, 350 150, 250 200, 310 280, 380 280)))");
        }

        //-------------------------------------------------

        [Test]
        public void TestByAreaOuterSimple()
        {
            string wkt = "POLYGON ((30 90, 10 40, 40 10, 70 10, 90 30, 80 80, 70 40, 30 40, 50 50, 60 70, 30 90))";
            CheckHullByAreaDelta(wkt, 0, "POLYGON ((10 40, 30 90, 60 70, 50 50, 30 40, 70 40, 80 80, 90 30, 70 10, 40 10, 10 40))");
            CheckHullByAreaDelta(wkt, 0.01, "POLYGON ((10 40, 30 90, 60 70, 50 50, 30 40, 70 40, 80 80, 90 30, 70 10, 40 10, 10 40))");
            CheckHullByAreaDelta(wkt, 0.1, "POLYGON ((10 40, 30 90, 60 70, 50 50, 70 40, 80 80, 90 30, 70 10, 40 10, 10 40))");
            CheckHullByAreaDelta(wkt, 0.2, "POLYGON ((30 90, 60 70, 70 40, 80 80, 90 30, 70 10, 40 10, 10 40, 30 90))");
            CheckHullByAreaDelta(wkt, 1, "POLYGON ((30 90, 80 80, 90 30, 70 10, 40 10, 10 40, 30 90))");
        }

        [Test]
        public void TestGoreRemoval()
        {
            CheckHullByAreaDelta("POLYGON ((30 120, 60 240, 200 220, 60.02 240.08, 80 320, 320 280, 230 160, 250 60, 30 120))",
                0.01, "POLYGON ((30 120, 80 320, 320 280, 230 160, 250 60, 30 120))");
        }

        //=================================================

        private void CheckHullOuter(string wkt, double vertexNumFraction, string wktExpected)
        {
            CheckHull(wkt, true, vertexNumFraction, wktExpected);
        }

        private void CheckHullInner(string wkt, double vertexNumFraction, string wktExpected)
        {
            CheckHull(wkt, false, vertexNumFraction, wktExpected);
        }

        private void CheckHull(string wkt, bool isOuter, double vertexNumFraction, string wktExpected)
        {
            var geom = Read(wkt);
            var actual = PolygonHullSimplifier.Hull(geom, isOuter, vertexNumFraction);
            //System.out.println(actual);
            Assert.That(actual.IsValid, Is.True);

            var expected = Read(wktExpected);
            CheckEqual(expected, actual);
        }

        private void CheckHullByAreaDelta(string wkt, double areaDeltaRatio, string wktExpected)
        {
            var geom = Read(wkt);
            var actual = PolygonHullSimplifier.HullByAreaDelta(geom, true, areaDeltaRatio);
            //System.out.println(actual);
            Assert.That(actual.IsValid, Is.True);

            var expected = Read(wktExpected);
            CheckEqual(expected, actual);
        }
    }
}
