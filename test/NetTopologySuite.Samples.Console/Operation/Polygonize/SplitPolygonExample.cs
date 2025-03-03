﻿using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Polygonize;
using NUnit.Framework;

namespace NetTopologySuite.Samples.Operation.Poligonize
{
    /*
     * based on
     * http://blog.opengeo.org/2012/06/21/splitpolygon-wps-process-p1/
     * http://blog.opengeo.org/2012/07/24/splitpolygon-wps-process-p2/
     *
     * and
     * https://github.com/mdavisog/wps-splitpoly
     *
     * and of course
     * http://sourceforge.net/mailarchive/forum.php?thread_name=CAK2ens3FY3qMT915_LoRiz6uqyww156swONSWRRaXc0anrxREg%40mail.gmail.com&forum_name=jts-topo-suite-user
     */

    public class SplitPolygonExample
    {
        internal static Geometry SplitPolygon(Geometry polygon, Geometry line)
        {
            var nodedLinework = polygon.Boundary.Union(line);
            var polygons = Polygonize(nodedLinework);

            // only keep polygons which are inside the input
            var output = new List<Geometry>();
            for (int i = 0; i < polygons.NumGeometries; i++)
            {
                var candpoly = (Polygon)polygons.GetGeometryN(i);
                if (polygon.Contains(candpoly.InteriorPoint))
                    output.Add(candpoly);
            }
            /*
            // We know that there may be some missing after 13.3.2
            // Hack: Build the difference and add the resulting polygons
            //       to the output.
            var diff = polygon.Difference(polygon.Factory.BuildGeometry(output));
            var diffPolygons = PolygonExtracter.GetPolygons(diff);
            output.AddRange(diffPolygons);
            */
            return polygon.Factory.BuildGeometry(output);
        }

        internal static Geometry Polygonize(Geometry geometry)
        {
            var lines = LineStringExtracter.GetLines(geometry);
            var polygonizer = new Polygonizer(false);
            polygonizer.Add(lines);
            var polys = new List<Geometry>(polygonizer.GetPolygons());

            var polyArray = GeometryFactory.ToGeometryArray(polys);
            return geometry.Factory.BuildGeometry(polyArray);
        }

        /*
        internal static Geometry PolygonizeForClip(Geometry geometry, IPreparedGeometry clip)
        {
            var lines = LineStringExtracter.GetLines(geometry);
            var clippedLines = new List<Geometry>();
            foreach (LineString line in lines)
            {
                if (clip.Contains(line))
                    clippedLines.Add(line);
            }
            var polygonizer = new Polygonizer();
            polygonizer.Add(clippedLines);
            var polys = polygonizer.GetPolygons();
            var polyArray = GeometryFactory.ToGeometryArray(polys);
            return geometry.Factory.CreateGeometryCollection(polyArray);
        }
         */

        internal static Geometry ClipPolygon(Geometry polygon, IPolygonal clipPolygonal)
        {
            var clipPolygon = (Geometry)clipPolygonal;
            var nodedLinework = polygon.Boundary.Union(clipPolygon.Boundary);
            var polygons = Polygonize(nodedLinework);

            /*
            // Build a prepared clipPolygon
            var prepClipPolygon = NetTopologySuite.Geometries.Prepared.PreparedGeometryFactory.Prepare(clipPolygon);
                */

            // only keep polygons which are inside the input
            var output = new List<Geometry>();
            for (int i = 0; i < polygons.NumGeometries; i++)
            {
                var candpoly = (Polygon)polygons.GetGeometryN(i);
                var interiorPoint = candpoly.InteriorPoint;
                if (polygon.Contains(interiorPoint) &&
                    /*prepClipPolygon.Contains(candpoly)*/
                    clipPolygon.Contains(interiorPoint))
                    output.Add(candpoly);
            }
            /*
            return polygon.Factory.CreateGeometryCollection(
                GeometryFactory.ToGeometryArray(output));
                */
            return polygon.Factory.BuildGeometry(output);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            var test = new SplitPolygonExample();
            try
            {
                test.Run();
                //test.RunClip();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        [Test]
        public void TestSplit()
        {
            Run();
        }

        //[Test]
        //public void TestClip()
        //{
        //    RunClip();
        //}

        internal void Run()
        {
            var reader = new WKTReader();
            var polygon = reader.Read("POLYGON((0 0, 0 100, 100 100, 100 0, 0 0), (10 10, 90 10, 90 90, 10 90, 10 10))");

            string[] lineWkts = new[]
            {
                //<image url="$(ProjectDir)\DocumentImages\SplitPolygon01.png"/>
                "MULTILINESTRING((50 -10, 50 20),(50 110, 50 80))",
                //<image url="$(ProjectDir)\DocumentImages\SplitPolygon02.png"/>
                "LINESTRING(50 20, 50 -10, 110 -10, 110 110, 50 110, 50 80)",
                //<image url="$(ProjectDir)\DocumentImages\SplitPolygon03.png"/>
                "LINESTRING(50 -10, 50 48, 51 49, 49 51, 50 52, 50 110)",
                //<image url="$(ProjectDir)\DocumentImages\SplitPolygon04.png"/>
                "LINESTRING(49 -10, 51 50, 49 110)",
                //<image url="$(ProjectDir)\DocumentImages\SplitPolygon05.png"/>
                "LINESTRING(50 -10, 50 110)",
                //<image url="$(ProjectDir)\DocumentImages\SplitPolygon06.png"/>
                "LINESTRING(5 -10, 5 110)",
                //<image url="$(ProjectDir)\DocumentImages\SplitPolygon07.png"/>
                "LINESTRING(5 -10, 5 95, 110 95)",
                //<image url="$(ProjectDir)\DocumentImages\SplitPolygon08.png"/>
                //分割结果——注意为了清晰起见对分割结果作了偏移
                //<image url="$(ProjectDir)\DocumentImages\SplitPolygon08Result.png"/>
                "LINESTRING(5 -10, 5 110, 110 50)"
            };

            foreach (string lineWkt in lineWkts)
                DoSplitTest(polygon, lineWkt);
        }

        [Test]
        public void RunClip()
        {
            var reader = new WKTReader();
            var polygon = reader.Read("POLYGON((0 0, 0 100, 100 100, 100 0, 0 0), (10 10, 90 10, 90 90, 10 90, 10 10))");

            string[] clipPolygonWkts = new[]
                               {
                                   "POLYGON((-10 45, 110 45, 110 55, -10 55, -10 45))",
                               };

            Console.WriteLine(string.Format("Clipping\n{0}", polygon));
            foreach (string lineWkt in clipPolygonWkts)
            {
                var polygonal = (IPolygonal)reader.Read(lineWkt);
                Console.WriteLine(string.Format("\nwith\n{0}", lineWkt));
                //<image url = "$(ProjectDir)\DocumentImages\ClipPolygon01.png" />
                var clippedPolygons = ClipPolygon(polygon, polygonal);
                Console.WriteLine(string.Format("results in:\n{0}", clippedPolygons));
            }
        }

        [Test()]
        public void TestGG1()
        {
            //<image url = "$(ProjectDir)\DocumentImages\SplitPolygon11.png" />

            DoSplitTest(@"POLYGON((0 0, 0 100, 100 100, 100 0, 0 0), (10 75, 10 90, 90 90, 90 75, 10 75))",
                        @"LINESTRING(50 110, 50 -10)");
        }

        //<image url = "$(ProjectDir)\DocumentImages\SplitPolygon09.png" />
        [Test()]
        public void TestCheckerBoard()
        {
            DoSplitTest(@"POLYGON((0 0, 0 90, 90 90, 90 0, 0 0), (25 25, 65 25, 65 65, 25 65, 25 25))",
                        @"MULTILINESTRING((30 -5, 30 95), (60 -5, 60 95), (-5 30, 95 30), (-5 60, 95 60))");
        }
        //<image url = "$(ProjectDir)\DocumentImages\SplitPolygon10.png" />

        [Test]
        public void TestWebExample()
        {
            DoSplitTest(@"POLYGON((110 20, 120 20, 120 10, 110 10, 110 20), (112 17, 118 18, 118 16, 112 15, 112 17))",
                @"LINESTRING (117 22, 112 18, 118 13, 115 8)");
        }

        private static void DoSplitTest(string geom1Wkt, string geom2Wkt, string resultWkt = null)
        {
            Console.WriteLine("Splitting\n{0}", geom1Wkt);
            var reader = new WKTReader();
            var geom1 = reader.Read(geom1Wkt);
            DoSplitTest(geom1, geom2Wkt, resultWkt);
        }

        private static void DoSplitTest(Geometry geom1, string geom2Wkt, string resultWkt = null)
        {
            Console.WriteLine("with\n{0}", geom2Wkt);

            var reader = new WKTReader();
            var geom2 = reader.Read(geom2Wkt);

            var result = SplitPolygon(geom1, geom2);
            Console.WriteLine($"results in:\n{result}");
            ToImage(geom1, geom2, result);

            if (!string.IsNullOrEmpty(resultWkt))
            {
                var expected = reader.Read(resultWkt);
                Assert.IsTrue(result.EqualsTopologically(expected),
                    "result.EqualsTopologically(expected)");
            }
        }

        private static void ToImage(Geometry geom1, Geometry geom2, Geometry geom3)
        {
            //var gpw = new Windows.Forms.GraphicsPathWriter();

            //var extent = geom1.EnvelopeInternal;
            //if (geom2 != null)
            //    extent.ExpandToInclude(geom2.EnvelopeInternal);
            //extent.ExpandBy(0.05 * extent.Width);

            //using (var img = new Bitmap(2 * ImageWidth, ImageHeight))
            //{
            //    using (var gr = Graphics.FromImage(img))
            //    {
            //        var at = CreateAffineTransformation(extent);
            //        gr.Clear(Color.WhiteSmoke);
            //        gr.SmoothingMode = SmoothingMode.AntiAlias;
            //        //gr.Transform = CreateTransform(extent);

            //        var gp1 = gpw.ToShape(at.Transform(geom1));
            //        if (geom1 is IPolygonal)
            //            gr.FillPath(Brushes.CornflowerBlue, gp1);
            //        gr.DrawPath(Pens.Blue, gp1);

            //        var gp2 = gpw.ToShape(at.Transform(geom2));
            //        if (geom2 is IPolygonal)
            //            gr.FillPath(Brushes.OrangeRed, gp2);
            //        gr.DrawPath(Pens.IndianRed, gp2);

            //        at = CreateAffineTransformation(extent, ImageWidth);

            //        var gp3 = gpw.ToShape(at.Transform(geom3));
            //        if (geom3 is IPolygonal)
            //            gr.FillPath(Brushes.Orange, gp3);
            //        gr.DrawPath(Pens.Peru, gp3);

            //    }
            //    var path = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), "png");
            //    img.Save(path, ImageFormat.Png);
            //    Console.WriteLine("Image written to {0}", new Uri(path).AbsoluteUri);
            //}
        }

        private const int ImageHeight = 320;
        private const int ImageWidth = 320;

        private static AffineTransformation CreateAffineTransformation(Envelope env, int offsetX = 0)
        {
            int imageRatio = ImageWidth / ImageHeight;
            double ratio = env.Width / env.Height;
            if (ratio > imageRatio)
            {
                double growHeight = (env.Width / imageRatio - env.Height) / 2;
                env.ExpandBy(0, growHeight);
            }
            else if (ratio < imageRatio)
            {
                double growWidth = (env.Height * imageRatio - env.Width) / 2;
                env.ExpandBy(growWidth, 0);
            }

            var s1 = new Coordinate(env.MinX, env.MaxY);
            var t1 = new Coordinate(offsetX, 0);
            var s2 = new Coordinate(env.MaxX, env.MaxY);
            var t2 = new Coordinate(offsetX + ImageWidth, 0);
            var s3 = new Coordinate(env.MaxX, env.MinY);
            var t3 = new Coordinate(offsetX + ImageWidth, ImageHeight);

            var atb = new AffineTransformationBuilder(s1, s2, s3, t1, t2, t3);
            return atb.GetTransformation();
        }
    }
}
