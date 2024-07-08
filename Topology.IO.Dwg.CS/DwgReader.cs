using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Topology.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;
using System.Collections;

namespace Topology.IO.Dwg.CS
{
    /// <summary>
    /// Reads AutoCAD entities and creates geometric representation of the features
    /// based on JTS model using single floating precision model.
    /// Curve-based entities and sub-entities are tesselated during the process.
    /// Processed AutoCAD entities are supposed to be database resident (DBRO).
    /// </summary>
    /// <remarks>
    /// To maintain the link between database-resident entity and it's JTS representation
    /// you may use <see cref="Geometry.UserData"/> property to store either <c>ObjectId</c>
    /// or a <c>Handle</c> of an entity. Keep in mind that <see cref="Geometry.UserData"/>
    /// property may not persist during certain topology-related operations.
    /// <para>
    /// This library references two Autodesk libraries being part of managed ObjectARX.
    /// Referenced libraries are <c>acdbmgd.dll</c> and <c>acmgd.dll</c> which may be found
    /// in the root installation folder of the targeted Autodesk platform/vertical.
    /// </para>
    /// </remarks>
    public class DwgReader : GeometryReader
    {
        public DwgReader() : base()
        {
        }

        public DwgReader(GeometryFactory factory) : base(factory)
        {
        }

        /// <summary>
        /// Returns 3D <see cref="Coordinate"/> converted from <see cref="Point3d"/> structure.
        /// </summary>
        /// <param name="point3d">A <see cref="Point3d"/> structure.</param>
        /// <returns>A three-dimensional <see cref="Coordinate"/> representation.</returns>
        /// <remarks></remarks>
        public Coordinate ReadCoordinate(Point3d point3d)
        {
            return new Coordinate(PrecisionModel.MakePrecise(point3d.X), PrecisionModel.MakePrecise(point3d.Y)/*, this.PrecisionModel.MakePrecise(point3d.Z)*/);
        }

        /// <summary>
        /// Returns 2D <see cref="Coordinate"/> converted from <see cref="Point2d"/> structure.
        /// </summary>
        /// <param name="point2d">A <see cref="Point2d"/> structure.</param>
        /// <returns>A two-dimensional <see cref="Coordinate"/> representation.</returns>
        /// <remarks></remarks>
        public Coordinate ReadCoordinate(Point2d point2d)
        {
            return new Coordinate(PrecisionModel.MakePrecise(point2d.X), PrecisionModel.MakePrecise(point2d.Y));
        }

        /// <summary>
        /// Returns <see cref="Point"/> geometry converted from <see cref="DBPoint"/> entity.
        /// </summary>
        /// <param name="dbPoint">A <see cref="DBPoint"/> entity (<c>POINT</c>).</param>
        /// <returns>A <see cref="Point"/> geometry.</returns>
        /// <remarks></remarks>
        public Point ReadPoint(DBPoint dbPoint)
        {
            return GeometryFactory.CreatePoint(ReadCoordinate(dbPoint.Position));
        }

        /// <summary>
        /// Returns <see cref="Point"/> geometry converted from <see cref="BlockReference"/> entity.
        /// </summary>
        /// <param name="blockReference">A <see cref="BlockReference"/> entity (<c>INSERT</c>).</param>
        /// <returns>A <see cref="Point"/> geometry.</returns>
        /// <remarks></remarks>
        public Point ReadPoint(BlockReference blockReference)
        {
            return GeometryFactory.CreatePoint(ReadCoordinate(blockReference.Position));
        }

        /// <summary>
        /// Returns <see cref="LineString"/> geometry converted from <see cref="Polyline"/> entity.
        /// </summary>
        /// <param name="polyline">A <see cref="Polyline"/> entity (<c>LWPOLYLINE</c>).</param>
        /// <returns>A <see cref="LineString"/> geometry.</returns>
        /// <remarks>
        /// If polyline entity contains arc segments (bulges), such segments will be
        /// tessellated using settings defined via <see cref="CurveTessellationMethod"/>.
        /// <para>
        /// If polyline's <c>Closed</c> flag is set to <c>True</c>, then resulting <see cref="LineString"/>'s
        /// <see cref="LineString.IsClosed"/> property will also reflect <c>True</c>.
        /// In case resulting coordinate sequence has <c>0..1</c> coordinates,
        /// geometry conversion will fail returning an empty <see cref="LineString"/>.
        /// </para>
        /// Polyline with <c>Elevation</c> set gets converted into 3D <see cref="LineString"/>.
        /// </remarks>
        public LineString ReadLineString(Polyline polyline)
        {
            var points = new CoordinateList();
            for (int i = 0; i <= polyline.NumberOfVertices - 1; i++)
            {
                switch (polyline.GetSegmentType(i))
                {
                    case SegmentType.Arc:
                        {
                            points.Add(GetTessellatedCurveCoordinates(polyline.GetArcSegmentAt(i)), AllowRepeatedCoordinates);
                            break;
                        }

                    default:
                        {
                            points.Add(ReadCoordinate(polyline.GetPoint3dAt(i)), AllowRepeatedCoordinates);
                            break;
                        }
                }
            }

            if (polyline.Closed)
                points.Add(points[0]);

            if (points.Count > 1)
            {
                if (polyline.Elevation != 0)
                {
                    foreach (var coord in points)
                        coord.Z = polyline.Elevation;
                }

                return GeometryFactory.CreateLineString(points.ToCoordinateArray());
            }
            else
                return LineString.Empty;
        }

        /// <summary>
        /// Returns <see cref="LineString"/> geometry converted from <see cref="Polyline3d"/> entity.
        /// </summary>
        /// <param name="polyline3d">A <see cref="Polyline3d"/> entity (<c>POLYLINE</c>).</param>
        /// <returns>A <see cref="LineString"/> geometry.</returns>
        /// <remarks>
        /// If polyline's <c>Closed</c> flag is set to <c>True</c>, then resulting <see cref="LineString"/>'s
        /// <see cref="LineString.IsClosed"/> property will also reflect <c>True</c>.
        /// In case resulting coordinate sequence has <c>0..1</c> coordinates,
        /// geometry conversion will fail returning an empty <see cref="LineString"/>.
        /// </remarks>
        public LineString ReadLineString(Polyline3d polyline3d)
        {
            var points = new CoordinateList();

            if (polyline3d.PolyType == Poly3dType.SimplePoly)
            {
                var trans = polyline3d.Database.TransactionManager.StartTransaction();

                var iterator = polyline3d.GetEnumerator();
                while (iterator.MoveNext())
                {
                    var vertex = (PolylineVertex3d)iterator.Current;
                    points.Add(ReadCoordinate(vertex.Position), AllowRepeatedCoordinates);
                }
                trans.Commit();
                trans.Dispose();
            }
            else
            {
                var collection = new DBObjectCollection();
                polyline3d.Explode(collection);
                foreach (Line line in collection)
                {
                    points.Add(ReadCoordinate(line.StartPoint), false);
                    points.Add(ReadCoordinate(line.EndPoint), false);
                }
                collection.Dispose();
            }

            if (polyline3d.Closed)
                points.Add(points[0]);

            if (points.Count > 1)
                return GeometryFactory.CreateLineString(points.ToCoordinateArray());
            else
                return LineString.Empty;
        }

        /// <summary>
        /// Returns <see cref="LineString"/> geometry converted from <see cref="Polyline2d"/> entity.
        /// </summary>
        /// <param name="polyline2d">A <see cref="Polyline2d"/> ("old-style") entity.</param>
        /// <returns>A <see cref="LineString"/> geometry.</returns>
        /// <remarks>
        /// If polyline's <c>Closed</c> flag is set to <c>True</c>, then resulting <see cref="LineString"/>'s
        /// <see cref="LineString.IsClosed"/> property will also reflect <c>True</c>.
        /// In case resulting coordinate sequence has <c>0..1</c> coordinates,
        /// geometry conversion will fail returning an empty <see cref="LineString"/>.
        /// </remarks>
        public LineString ReadLineString(Polyline2d polyline2d)
        {
            var points = new CoordinateList();

            if (polyline2d.PolyType == Poly2dType.SimplePoly)
            {
                var TR = polyline2d.Database.TransactionManager.StartTransaction();
                var iterator = polyline2d.GetEnumerator();
                while (iterator.MoveNext())
                {
                    var vertex = (Vertex2d)iterator.Current;
                    points.Add(ReadCoordinate(vertex.Position), AllowRepeatedCoordinates);
                }
                TR.Commit();
                TR.Dispose();
            }
            else
            {
                var collection = new DBObjectCollection();
                polyline2d.Explode(collection);
                foreach (Line line in collection)
                {
                    points.Add(ReadCoordinate(line.StartPoint), false);
                    points.Add(ReadCoordinate(line.EndPoint), false);
                }
                collection.Dispose();
            }

            if (polyline2d.Closed)
                points.Add(points[0]);

            if (points.Count > 1)
                return GeometryFactory.CreateLineString(points.ToCoordinateArray());
            else
                return LineString.Empty;
        }

        /// <summary>
        /// Returns <see cref="LineString"/> geometry converted from <see cref="Line"/> entity.
        /// </summary>
        /// <param name="line">A <see cref="Line"/> entity (<c>LINE</c>).</param>
        /// <returns>A <see cref="LineString"/> geometry.</returns>
        /// <remarks>
        /// In case resulting coordinate sequence has 1 or 0 coordinates,
        /// geometry conversion will fail returning an empty <see cref="LineString"/>.
        /// </remarks>
        public LineString ReadLineString(Line line)
        {
            var points = new CoordinateList();
            points.Add(ReadCoordinate(line.StartPoint));
            points.Add(ReadCoordinate(line.EndPoint), AllowRepeatedCoordinates);

            if (points.Count > 1)
                return GeometryFactory.CreateLineString(points.ToCoordinateArray());
            else
                return LineString.Empty;
        }

        /// <summary>
        /// Returns <see cref="LineString"/> geometry converted from <see cref="Mline"/> (<c>MultiLine</c>) entity.
        /// </summary>
        /// <param name="multiLine">A <see cref="Mline"/> entity (<c>MLINE</c>).</param>
        /// <returns>A <see cref="LineString"/> geometry.</returns>
        /// <remarks>
        /// In case resulting coordinate sequence has 1 or 0 coordinates,
        /// geometry conversion will fail returning an empty <see cref="LineString"/>.
        /// </remarks>
        public LineString ReadLineString(Mline multiLine)
        {
            var points = new CoordinateList();

            for (int i = 0; i <= multiLine.NumberOfVertices - 1; i++)
                points.Add(ReadCoordinate(multiLine.VertexAt(i)), AllowRepeatedCoordinates);

            if (multiLine.IsClosed)
                points.Add(points[0]);

            if (points.Count > 1)
                return GeometryFactory.CreateLineString(points.ToCoordinateArray());
            else
                return LineString.Empty;
        }

        /// <summary>
        /// Returns <see cref="LineString"/> geometry converted from <see cref="Arc"/> entity.
        /// During conversion <see cref="Arc"/> is being tessellated using settings defined
        /// via <see cref="CurveTessellationMethod"/>.
        /// </summary>
        /// <param name="arc">An <see cref="Arc"/> entity (<c>ARC</c>).</param>
        /// <returns>A <see cref="LineString"/> geometry.</returns>
        /// <remarks></remarks>
        public LineString ReadLineString(Arc arc)
        {
            var points = new CoordinateList(GetTessellatedCurveCoordinates(arc), AllowRepeatedCoordinates);

            if (points.Count > 1)
                return GeometryFactory.CreateLineString(points.ToCoordinateArray());
            else
                return LineString.Empty;
        }

        /// <summary>
        /// Returns <see cref="MultiPolygon"/> geometry converted from <see cref="MPolygon"/> entity.
        /// </summary>
        /// <param name="multiPolygon">A <see cref="MPolygon"/> entity.</param>
        /// <returns>A <see cref="MultiPolygon"/> geometry.</returns>
        /// <remarks>
        /// If <see cref="MPolygon"/> entity contains arc segments (bulges), such segments will
        /// get tessellated using settings defined via <see cref="CurveTessellationMethod"/>.
        /// </remarks>
        public MultiPolygon ReadMultiPolygon(MPolygon multiPolygon)
        {
            var polygons = new List<Polygon>();

            for (int i = 0; i <= multiPolygon.NumMPolygonLoops - 1; i++)
            {
                if (multiPolygon.GetLoopDirection(i) == LoopDirection.Exterior)
                {
                    var shell = GeometryFactory.CreateLinearRing(GetMPolygonLoopCoordinates(multiPolygon, multiPolygon.GetMPolygonLoopAt(i)));

                    var holes = new List<LinearRing>();
                    foreach (int j in multiPolygon.GetChildLoops(i))
                    {
                        if (multiPolygon.GetLoopDirection(j) == LoopDirection.Interior)
                            holes.Add(GeometryFactory.CreateLinearRing(GetMPolygonLoopCoordinates(multiPolygon, multiPolygon.GetMPolygonLoopAt(j))));
                    }
                    polygons.Add(GeometryFactory.CreatePolygon(shell, holes.ToArray()));
                }
            }

            return GeometryFactory.CreateMultiPolygon(polygons.ToArray());
        }

        /// <summary>
        /// Returns <see cref="Geometry"/> representation converted from <see cref="Entity"/> entity.
        /// Throws an exception if given <see cref="Entity"/> conversion is not supported.
        /// <para>
        /// Supported AutoCAD entity types:
        /// <list type="table">
        /// <listheader>
        /// <term>Class Name</term>
        /// <term>DXF Code</term>
        /// </listheader>
        /// <item>
        /// <term>AcDbPoint</term>
        /// <term><c>POINT</c></term>
        /// </item>
        /// <item>
        /// <term>AcDbBlockReference</term>
        /// <term><c>INSERT</c></term>
        /// </item>
        /// <item>
        /// <term>AcDbLine</term>
        /// <term><c>LINE</c></term>
        /// </item>
        /// <item>
        /// <term>AcDbArc</term>
        /// <term><c>ARC</c></term>
        /// </item>
        /// <item>
        /// <term>AcDbPolyline</term>
        /// <term><c>LWPOLYLINE</c></term>
        /// </item>
        /// <item>
        /// <term>AcDb2dPolyline</term>
        /// <term><c>POLYLINE</c></term>
        /// </item>
        /// <item>
        /// <term>AcDb3dPolyline</term>
        /// <term><c>POLYLINE</c></term>
        /// </item>
        /// <item>
        /// <term>AcDbMline</term>
        /// <term><c>MLINE</c></term>
        /// </item>
        /// <item>
        /// <term>AcDbMPolygon</term>
        /// <term><c>MPOLYGON</c></term>
        /// </item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="entity">An AutoCAD <see cref="Entity"/>.</param>
        /// <returns>A <see cref="Geometry"/> representation.</returns>
        /// <remarks></remarks>
        public Geometry ReadGeometry(Entity entity)
        {
            switch (entity.GetRXClass().Name)
            {
                case "AcDbPoint":
                    return ReadPoint((DBPoint)entity);

                case "AcDbBlockReference":
                    return ReadPoint((BlockReference)entity);

                case "AcDbLine":
                    return ReadLineString((Line)entity);

                case "AcDbPolyline":
                    return ReadLineString((Polyline)entity);

                case "AcDb2dPolyline":
                    return ReadLineString((Polyline2d)entity);

                case "AcDb3dPolyline":
                    return ReadLineString((Polyline3d)entity);

                case "AcDbArc":
                    return ReadLineString((Arc)entity);

                case "AcDbMline":
                    return ReadLineString((Mline)entity);

                case "AcDbMPolygon":
                    // added due to possibly invalid MPolygons (returned by AutoCAD as ImpEntity)
                    var ent = entity as MPolygon;
                    if (ent != null)
                    {
                        return ReadMultiPolygon(ent);
                    }
                    else
                    {
                        throw new ArgumentException(string.Format("Invalid MPolygon entity. Conversion to IGeometry is not possible.", entity.GetRXClass().Name));
                        /* TODO Change to default(_) if this is not a reference type */
                    }

                default:
                    throw new ArgumentException(string.Format("Conversion from {0} entity to IGeometry is not supported.", entity.GetRXClass().Name));
                    /* TODO Change to default(_) if this is not a reference type */
            }
        }

        private Coordinate[] GetTessellatedCurveCoordinates(CircularArc3d curve)
        {
            var points = new CoordinateList();

            if (curve.StartPoint != curve.EndPoint)
            {
                switch (CurveTessellationMethod)
                {
                    case CurveTessellation.None:
                        points.Add(ReadCoordinate(curve.StartPoint));
                        points.Add(ReadCoordinate(curve.EndPoint));
                        break;

                    case CurveTessellation.Linear:
                        foreach (var item in curve.GetSamplePoints(Convert.ToInt32(CurveTessellationValue)))
                        {
                            var point = item.Point;
                            points.Add(ReadCoordinate(point));
                        }

                        break;

                    case CurveTessellation.Scaled:
                        double area = curve.GetArea(curve.GetParameterOf(curve.StartPoint), curve.GetParameterOf(curve.EndPoint)) * CurveTessellationValue;

                        double angle = Math.Acos((curve.Radius - 1.0 / (area / 2.0)) / (double)curve.Radius);
                        int segments = Convert.ToInt32(2 * Math.PI / angle);

                        if (segments < 8)
                            segments = 8;
                        if (segments > 128)
                            segments = 128;

                        foreach (var item in curve.GetSamplePoints(Convert.ToInt32(segments)))
                        {
                            var point = item.Point;
                            points.Add(ReadCoordinate(point));
                        }

                        break;
                }
            }

            return points.ToCoordinateArray();
        }

        private Coordinate[] GetTessellatedCurveCoordinates(Matrix3d parentEcs, CircularArc2d curve)
        {
            var matrix = parentEcs.Inverse();
            var pts = curve.GetSamplePoints(3);

            var startPt = new Point3d(pts[0].X, pts[0].Y, 0);
            var midPt = new Point3d(pts[1].X, pts[1].Y, 0);
            var endPt = new Point3d(pts[2].X, pts[2].Y, 0);

            startPt.TransformBy(matrix);
            midPt.TransformBy(matrix);
            endPt.TransformBy(matrix);

            return GetTessellatedCurveCoordinates(new CircularArc3d(startPt, midPt, endPt));
        }

        private Coordinate[] GetTessellatedCurveCoordinates(Matrix3d parentEcs, Point2d startPoint, Point2d endPoint, double bulge)
        {
            return GetTessellatedCurveCoordinates(parentEcs, new CircularArc2d(startPoint, endPoint, bulge, false));
        }

        private Coordinate[] GetTessellatedCurveCoordinates(Arc curve)
        {
            CircularArc3d circularArc;

            try
            {
                circularArc = new CircularArc3d(curve.StartPoint, curve.GetPointAtParameter((curve.EndParam - curve.StartParam) / 2), curve.EndPoint);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception /*ex*/)
            {
                circularArc = new CircularArc3d(curve.StartPoint, curve.GetPointAtParameter((curve.EndParam + curve.StartParam) / 2), curve.EndPoint);
            }
            return GetTessellatedCurveCoordinates(circularArc);
        }

        private Coordinate[] GetMPolygonLoopCoordinates(MPolygon multiPolygon, MPolygonLoop multiPolygonLoop)
        {
            var points = new CoordinateList();

            for (int i = 0; i <= multiPolygonLoop.Count - 1; i++)
            {
                var vert = multiPolygonLoop[i];
                if (vert.Bulge == 0) points.Add(ReadCoordinate(vert.Vertex), AllowRepeatedCoordinates);
                else
                {
                    Point2d endPoint;
                    if (i + 1 <= multiPolygonLoop.Count - 1) endPoint = multiPolygonLoop[i + 1].Vertex;
                    else endPoint = multiPolygonLoop[0].Vertex;

                    foreach (var point in GetTessellatedCurveCoordinates(multiPolygon.Ecs, vert.Vertex, endPoint, vert.Bulge))
                        points.Add(point, AllowRepeatedCoordinates);
                }
            }

            if (!points[0].Equals2D(points[points.Count - 1]))
                points.Add(points[0]);

            return points.ToCoordinateArray();
        }

        public GeometryCollection ReadGeometryCollection(DBObjectCollection dbObjects)
        {
            var col = new List<Geometry>();

            foreach (DBObject dbObj in dbObjects)
            {
                var geom = ReadGeometry((Entity)dbObj);
                if (geom != null) col.Add(geom);
            }

            return new GeometryCollection(col.ToArray());
        }

        public GeometryCollection ReadGeometryCollection(ObjectId[] objectIds)
        {
            var col = new List<Geometry>();

            var db = HostApplicationServices.WorkingDatabase;
            var tr = db.TransactionManager.StartTransaction();

            try
            {
                foreach (var objId in objectIds)
                {
                    var ent = tr.GetObject(objId, OpenMode.ForRead, false) as Entity;
                    var geom = ReadGeometry(ent);
                    if (geom != null) col.Add(geom);
                }
                tr.Commit();
            }
            finally
            {
                tr.Dispose();
            }

            return new GeometryCollection(col.ToArray());
        }

        public GeometryCollection ReadGeometryCollection(ObjectIdCollection objectIds)
        {
            var col = new List<Geometry>();

            var db = HostApplicationServices.WorkingDatabase;
            var tr = db.TransactionManager.StartTransaction();

            try
            {
                foreach (ObjectId objId in objectIds)
                {
                    var ent = tr.GetObject(objId, OpenMode.ForRead, false) as Entity;
                    var geom = ReadGeometry(ent);
                    if (geom != null) col.Add(geom);
                }
                tr.Commit();
            }
            finally
            {
                tr.Dispose();
            }

            return new GeometryCollection(col.ToArray());
        }
    }
}
