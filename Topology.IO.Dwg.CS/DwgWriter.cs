using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Topology.IO.Dwg.CS
{
    /// <summary>
    /// Reads features based on JTS model and creates their AutoCAD representation
    /// using single floating precision model.
    /// Created AutoCAD entities are not database resident, it's up to you to commit
    /// them to the existing <c>Database</c> using <c>Transaction</c>.
    /// </summary>
    /// <remarks>
    /// This library references two Autodesk libraries being part of managed ObjectARX.
    /// Referenced libraries are <c>acdbmgd.dll</c> and <c>acmgd.dll</c> which may be found
    /// in the root installation folder of the targeted Autodesk platform/vertical.
    /// </remarks>
    public class DwgWriter : GeometryWriter
    {
        public DwgWriter() : base()
        {
        }

        public DwgWriter(GeometryFactory factory) : base(factory)
        {
        }

        /// <summary>
        /// Returns <see cref="Point3d"/> structure converted from <see cref="Coordinate"/>.
        /// If <see cref="Coordinate"/> is two-dimensional, collapses <c>Z</c> axis of
        /// resulting <see cref="Point3d"/> to 0.
        /// </summary>
        /// <param name="coordinate">A <see cref="Coordinate"/> structure.</param>
        /// <returns>A <see cref="Point3d"/> structure.</returns>
        /// <remarks></remarks>
        public Point3d WritePoint3d(Coordinate coordinate)
        {
            if (!double.IsNaN(coordinate.Z))
                return new Point3d(PrecisionModel.MakePrecise(coordinate.X), PrecisionModel.MakePrecise(coordinate.Y), PrecisionModel.MakePrecise(coordinate.Z));
            else
                return new Point3d(PrecisionModel.MakePrecise(coordinate.X), PrecisionModel.MakePrecise(coordinate.Y), 0);
        }

        /// <summary>
        /// Returns <see cref="Point3d"/> structure converted from <see cref="Point"/> geometry.
        /// If <see cref="Point"/> is two-dimensional, collapses <c>Z</c> axis of resulting
        /// <see cref="Point3d"/> to 0.
        /// </summary>
        /// <param name="point">A <see cref="Point"/> geometry.</param>
        /// <returns>A <see cref="Point3d"/> structure.</returns>
        /// <remarks></remarks>
        public Point3d WritePoint3d(Point point)
        {
            return WritePoint3d(point.Coordinate);
        }

        /// <summary>
        /// Returns <see cref="Point2d"/> structure converted from <see cref="Coordinate"/>.
        /// If <see cref="Coordinate"/> is three-dimensional, clamps resulting <c>Z</c> axis.
        /// </summary>
        /// <param name="coordinate">A <see cref="Coordinate"/> structure.</param>
        /// <returns>A <see cref="Point2d"/> structure.</returns>
        /// <remarks></remarks>
        public Point2d WritePoint2d(Coordinate coordinate)
        {
            return new Point2d(PrecisionModel.MakePrecise(coordinate.X), PrecisionModel.MakePrecise(coordinate.Y));
        }

        /// <summary>
        /// Returns <see cref="Point2d"/> structure converted from <see cref="Point"/> geometry.
        /// If <see cref="Point"/> is three-dimensional, clamps resulting <c>Z</c> axis.
        /// </summary>
        /// <param name="point">A <see cref="Point"/> geometry.</param>
        /// <returns>A <see cref="Point2d"/> structure.</returns>
        /// <remarks></remarks>
        public Point2d WritePoint2d(Point point)
        {
            return WritePoint2d(point.Coordinate);
        }

        /// <summary>
        /// Returns <see cref="DBPoint"/> entity converted from <see cref="Point"/> geometry.
        /// </summary>
        /// <param name="point">A <see cref="Point"/> geometry.</param>
        /// <returns>A <see cref="DBPoint"/> entity (<c>POINT</c>).</returns>
        /// <remarks></remarks>
        public DBPoint WriteDbPoint(Point point)
        {
            return new DBPoint(WritePoint3d(point));
        }

        /// <summary>
        /// Returns <see cref="Polyline"/> entity converted from <see cref="LineString"/> geometry.
        /// </summary>
        /// <param name="lineString">A <see cref="LineString"/> geometry.</param>
        /// <returns>A <see cref="Polyline"/> entity (<c>LWPOLYLINE</c>).</returns>
        /// <remarks>
        /// If first and last coordinate in the <see cref="LineString"/> coordinate sequence are equal,
        /// returned <see cref="Polyline"/> is closed. To check whether <see cref="LineString"/> is
        /// closed, see it's <see cref="LineString.IsClosed"/> property.
        /// <para>
        /// If first <see cref="LineString"/>'s coordinate is 3D, then it's Z value gets translated
        /// into the <see cref="Polyline"/>'s <c>Elevation</c>.
        /// </para>
        /// </remarks>
        public Polyline WritePolyline(LineString lineString, double width = 0d)
        {
            var geometry = new Polyline();
            int i = 0;
            foreach (var coordinate in lineString.Coordinates)
            {
                geometry.AddVertexAt(i, new Point2d(coordinate.X, coordinate.Y), 0, width, width);
                i += 1;
            }
            geometry.Closed = lineString.StartPoint.EqualsExact(lineString.EndPoint);
            geometry.MinimizeMemory();

            if (i > 0)
            {
                if (lineString.Coordinates[0].Z != 0)
                    geometry.Elevation = lineString.Coordinates[0].Z;
            }

            return geometry;
        }

        /// <summary>
        /// Returns <see cref="Polyline"/> entity converted from <see cref="LinearRing"/> geometry.
        /// Resulting <see cref="Polyline"/> is always closed.
        /// </summary>
        /// <param name="linearRing">A <see cref="LinearRing"/> geometry.</param>
        /// <returns>A <see cref="Polyline"/> entity (<c>LWPOLYLINE</c>).</returns>
        /// <remarks></remarks>
        public Polyline WritePolyline(LinearRing linearRing)
        {
            var geometry = new Polyline();
            int i = 0;
            foreach (var coordinate in linearRing.Coordinates)
            {
                geometry.AddVertexAt(i, WritePoint2d(coordinate), 0, 0, 0);
                i += 1;
            }
            geometry.Closed = true;
            geometry.MinimizeMemory();

            if (i > 0)
            {
                if (linearRing.Coordinates[0].Z != 0)
                    geometry.Elevation = linearRing.Coordinates[0].Z;
            }

            return geometry;
        }

        /// <summary>
        /// Returns <c>1..n</c> collection of <see cref="Polyline"/> entities converted from <see cref="Polygon"/> geometry.
        /// First <see cref="Polyline"/> in a collection is always <see cref="Polygon.Shell"/>. The rest of
        /// resulting collection items represent <see cref="Polygon.Holes"/>, if <see cref="Polygon"/> geometry had
        /// holes (inner boundaries) in first place.
        /// </summary>
        /// <param name="polygon">A <see cref="Polygon"/> geometry.</param>
        /// <returns>Array of <see cref="Polyline"/> entities (<c>LWPOLYLINE</c>s).</returns>
        /// <remarks></remarks>
        public Polyline[] WritePolyline(Polygon polygon)
        {
            var polylines = new List<Polyline>
            {
                WritePolyline(polygon.Shell)
            };

            foreach (var hole in polygon.Holes)
                polylines.Add(WritePolyline(hole));

            return polylines.ToArray();
        }

        /// <summary>
        /// Returns <see cref="Polyline3d"/> entity converted from <see cref="LineString"/> geometry.
        /// </summary>
        /// <param name="lineString">A <see cref="LineString"/> geometry.</param>
        /// <returns>A <see cref="Polyline3d"/> entity (<c>POLYLINE</c>).</returns>
        /// <remarks>
        /// If first and last coordinate in the <see cref="LineString"/> coordinate sequence are equal,
        /// returned <see cref="Polyline3d"/> is closed. To check whether <see cref="LineString"/> is
        /// closed, see it's <see cref="LineString.IsClosed"/> property.
        /// </remarks>
        public Polyline3d WritePolyline3d(LineString lineString)
        {
            var points = new Point3dCollection();
            foreach (var coordinate in lineString.Coordinates)
                points.Add(WritePoint3d(coordinate));
            return new Polyline3d(Poly3dType.SimplePoly, points, lineString.StartPoint.EqualsExact(lineString.EndPoint));
        }

        /// <summary>
        /// Returns <see cref="Polyline3d"/> entity converted from <see cref="LinearRing"/> geometry.
        /// Resulting <see cref="Polyline3d"/> is always closed.
        /// </summary>
        /// <param name="linearRing">A <see cref="LinearRing"/> geometry.</param>
        /// <returns>A <see cref="Polyline3d"/> entity (<c>POLYLINE</c>).</returns>
        /// <remarks></remarks>
        public Polyline3d WritePolyline3d(LinearRing linearRing)
        {
            var points = new Point3dCollection();
            foreach (var coordinate in linearRing.Coordinates)
                points.Add(WritePoint3d(coordinate));
            return new Polyline3d(Poly3dType.SimplePoly, points, true);
        }

        /// <summary>
        /// Returns <see cref="Polyline2d"/> entity converted from <see cref="LineString"/> geometry.
        /// </summary>
        /// <param name="lineString">A <see cref="LineString"/> geometry.</param>
        /// <returns>A <see cref="Polyline2d"/> ("old-style") entity.</returns>
        /// <remarks>
        /// If first and last coordinate in the <see cref="LineString"/> coordinate sequence are equal,
        /// returned <see cref="Polyline2d"/> is closed. To check whether <see cref="LineString"/> is
        /// closed, see it's <see cref="LineString.IsClosed"/> property.
        /// </remarks>
        public Polyline2d WritePolyline2d(LineString lineString)
        {
            var points = new Point3dCollection();
            foreach (var coordinate in lineString.Coordinates)
                points.Add(WritePoint3d(coordinate));
            return new Polyline2d(Poly2dType.SimplePoly, points, 0, lineString.StartPoint.EqualsExact(lineString.EndPoint), 0, 0, null);
        }

        /// <summary>
        /// Returns <see cref="Polyline2d"/> entity converted from <see cref="LinearRing"/> geometry.
        /// Resulting <see cref="Polyline2d"/> is always closed.
        /// </summary>
        /// <param name="linearRing">A <see cref="LinearRing"/> geometry.</param>
        /// <returns>A <see cref="Polyline2d"/> ("old-style") entity.</returns>
        /// <remarks></remarks>
        public Polyline2d WritePolyline2d(LinearRing linearRing)
        {
            var points = new Point3dCollection();
            foreach (var coordinate in linearRing.Coordinates)
                points.Add(WritePoint3d(coordinate));
            return new Polyline2d(Poly2dType.SimplePoly, points, 0, true, 0, 0, null);
        }

        /// <summary>
        /// Returns <see cref="Line"/> entity converted from <see cref="LineSegment"/> geometry.
        /// </summary>
        /// <param name="lineSegment">A <see cref="LineSegment"/> geometry.</param>
        /// <returns>A <see cref="Line"/> entity (<c>LINE</c>).</returns>
        /// <remarks></remarks>
        public Line WriteLine(LineSegment lineSegment)
        {
            var geometry = new Line
            {
                StartPoint = WritePoint3d(lineSegment.P0),
                EndPoint = WritePoint3d(lineSegment.P1)
            };
            return geometry;
        }

        /// <summary>
        /// Returns <see cref="MPolygon"/> entity converted from <see cref="Polygon"/> geometry.
        /// </summary>
        /// <param name="polygon">A <see cref="Polygon"/> geometry.</param>
        /// <returns>A <see cref="MPolygon"/> entity (<c>MPOLYGON</c>).</returns>
        /// <remarks></remarks>
        public MPolygon WriteMPolygon(Polygon polygon)
        {
            var ent = new MPolygon();

            foreach (MPolygonLoop polLoop in GetMPolygonLoopCollection(polygon))
                ent.AppendMPolygonLoop(polLoop, false, 0);

            ent.BalanceTree();
            return ent;
        }

        /// <summary>
        /// Returns <see cref="MPolygon"/> entity converted from <see cref="MultiPolygon"/> geometry.
        /// </summary>
        /// <param name="multiPolygon">A <see cref="MultiPolygon"/> geometry.</param>
        /// <returns>A <see cref="MPolygon"/> entity (<c>MPOLYGON</c>).</returns>
        /// <remarks></remarks>
        public MPolygon WriteMPolygon(MultiPolygon multiPolygon)
        {
            var ent = new MPolygon();

            foreach (Polygon polygon in multiPolygon.Geometries)
            {
                foreach (MPolygonLoop polLoop in GetMPolygonLoopCollection(polygon))
                    ent.AppendMPolygonLoop(polLoop, false, 0);
            }

            ent.BalanceTree();
            return ent;
        }

        public Entity WriteEntity(Geometry geometry, string rxClassName)
        {
            switch (rxClassName)
            {
                case "AcDbMPolygon":
                    {
                        return WriteMPolygon((MultiPolygon)geometry);
                    }

                case "AcDbLine":
                case "AcDbPolyline":
                case "AcDbMline":
                    {
                        return WritePolyline((LineString)geometry);
                    }

                case "AcDb2dPolyline":
                    {
                        return WritePolyline2d((LineString)geometry);
                    }

                case "AcDb3dPolyline":
                    {
                        return WritePolyline3d((LineString)geometry);
                    }

                case "AcDbPoint":
                case "AcDbBlockReference":
                    {
                        return WriteDbPoint((Point)geometry);
                    }

                default:
                    {
                        throw new ArgumentException(string.Format("Geometry conversion from {0} to {1} is not supported.", rxClassName, geometry.GeometryType));
                        /* TODO Change to default(_) if this is not a reference type */
                    }
            }
        }

        private MPolygonLoop GetMPolygonLoop(LinearRing linearRing)
        {
            var ent = new MPolygonLoop();

            foreach (var coord in linearRing.Coordinates)
                ent.Add(new BulgeVertex(WritePoint2d(coord), 0));

            return ent;
        }

        private MPolygonLoopCollection GetMPolygonLoopCollection(Polygon polygon)
        {
            var col = new MPolygonLoopCollection
            {
                GetMPolygonLoop(polygon.Shell)
            };

            foreach (var hole in polygon.Holes)
                col.Add(GetMPolygonLoop(hole));

            return col;
        }
    }
}
