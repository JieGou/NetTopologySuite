using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NetTopologySuite.Tests.NUnit.Algorithm
{
    public class LengthTest : GeometryTestCase
    {

        [TestCase]
        public void TestLength()
        {
            var factory = new GeometryFactory();

            // 创建 LineSegment 列表，包含不同方向的线段
            var segments = new List<LineSegment>
        {
            new LineSegment(new Coordinate(0, -100), new Coordinate(0, -50)),
            new LineSegment(new Coordinate(0, -50), new Coordinate(0, 0)),

            new LineSegment(new Coordinate(0, 0), new Coordinate(50, 0)),
            new LineSegment(new Coordinate(50, 0), new Coordinate(100, 0)),

            new LineSegment(new Coordinate(400, -100), new Coordinate(400, -50)),
            new LineSegment(new Coordinate(400, -50), new Coordinate(400, 0)),

            new LineSegment(new Coordinate(400, 0), new Coordinate(350, 0)),
            new LineSegment(new Coordinate(350, 0), new Coordinate(300, 0)),
        };

            // 将 LineSegment 转换为 LineString
            var lineStrings = new List<LineString>();
            int i = 0;
            foreach (var segment in segments)
            {
                var coordinates = new[] { segment.P0, segment.P1 };
                var itemLineString = factory.CreateLineString(coordinates);
                if (i % 2 == 0) itemLineString = factory.CreateLineString(itemLineString.CoordinateSequence.Reversed());
                lineStrings.Add(itemLineString);
                i++;
            }

            // 使用 LineMerger 合并 LineString
            var lineMerger = new NetTopologySuite.Operation.Linemerge.LineMerger();
            lineMerger.Add(lineStrings);
            var mergedLineStrings = lineMerger.GetMergedLineStrings();

            // 输出合并后的 LineString
            foreach (LineString mergedLineString in mergedLineStrings)
            {
                if (mergedLineString == null) continue;
                System.Console.WriteLine(mergedLineString);
                //判断顺时针 逆时针
                bool isCounterClockwise = CreateLinearRingFromLineString(mergedLineString).IsCCW;
            }
            CheckLengthOfLine("LINESTRING (100 200, 200 200, 200 100, 100 100, 100 200)", 400.0);
        }
        public static LineString EnsureClosed(LineString lineString)
        {
            if (lineString.IsClosed)
            {
                return lineString;
            }

            var factory = lineString.Factory;
            var coordinateList = new CoordinateList(lineString.Coordinates);
            // 添加起始点到末尾以封闭线串
            coordinateList.Add(lineString.StartPoint.Coordinate, false);

            return factory.CreateLineString(coordinateList.ToCoordinateArray());
        }

        public static LinearRing CreateLinearRingFromLineString(LineString lineString)
        {
            var factory = lineString.Factory;

            // 检查 LineString 是否为空
            if (lineString.IsEmpty)
            {
                return factory.CreateLinearRing((CoordinateSequence)null);
            }

            // 检查点的数量是否足够
            if (lineString.NumPoints < LinearRing.MinimumValidSize)
            {
                throw new ArgumentException($"LineString 必须至少有 {LinearRing.MinimumValidSize} 个点才能创建 LinearRing");
            }

            // 确保 LineString 是封闭的
            var coordinateList = new CoordinateList(lineString.IsClosed ? lineString.Coordinates : EnsureClosed(lineString).Coordinates);

            var coordinateSequence = factory.CoordinateSequenceFactory.Create(coordinateList.ToCoordinateArray());
            return factory.CreateLinearRing(coordinateSequence);
        }

        void CheckLengthOfLine(string wkt, double expectedLen)
        {
            var ring = (LineString)Read(wkt);

            var pts = ring.CoordinateSequence;
            double actual = Length.OfLine(pts);
            Assert.AreEqual(actual, expectedLen);
        }
    }
}
