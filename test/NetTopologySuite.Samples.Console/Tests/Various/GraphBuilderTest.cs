using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NUnit.Framework;
using QuickGraph;
using QuickGraph.Algorithms.Observers;
using QuickGraph.Algorithms.ShortestPath;

namespace GisSharpBlog.NetTopologySuite.Samples.Tests.Various
{
    [TestFixture]
    public class GraphBuilderTest
    {
        private static double ComputeWeight(LineString line)
        { return line.Length; }

        private const string shp = ".shp";
        private const string shx = ".shx";
        private const string dbf = ".dbf";

        private GeometryFactory factory;
        private LineString a, b, c, d, e;
        private Point start, end;
        private readonly GraphBuilder.ComputeWeightDelegate weightComputer = ComputeWeight;

        //[TestFixtureSetUp]
        public void FixtureSetup()
        {
            Environment.CurrentDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"../../../NetTopologySuite.Samples.Shapefiles");
        }

        [SetUp]
        public void Setup()
        {
            factory = GeometryFactory.Fixed;

            // Build sample geometries
            a = factory.CreateLineString(new Coordinate[]
            {
                new Coordinate(0, 0),
                new Coordinate(100, 0),
                new Coordinate(200, 100),
                new Coordinate(200, 200),
            });
            b = factory.CreateLineString(new Coordinate[]
            {
                new Coordinate(0, 0),
                new Coordinate(100, 100),
                new Coordinate(200, 200),
            });
            c = factory.CreateLineString(new Coordinate[]
            {
                new Coordinate(0, 0),
                new Coordinate(0, 100),
                new Coordinate(100, 200),
                new Coordinate(200, 200),
            });
            d = factory.CreateLineString(new Coordinate[]
            {
                new Coordinate(0, 0),
                new Coordinate(300, 0),
                new Coordinate(300, 200),
                new Coordinate(150, 200),
                new Coordinate(150, 300),
            });
            e = factory.CreateLineString(new Coordinate[]
            {
                new Coordinate(100, 300),
                new Coordinate(150, 300),
                new Coordinate(200, 300),
            });
            start = a.StartPoint;
            end = d.EndPoint;
        }

        [Test]
        public void BuildGraphAndSearchShortestPathUsingGeometryUnion()
        {
            var edges = a.Union(b).Union(c).Union(d).Union(e);
            Assert.IsNotNull(edges);
            Assert.IsTrue(edges.GetType() == typeof(MultiLineString));
            Assert.Greater(edges.NumGeometries, 0);
            foreach (var edge in ((GeometryCollection)edges).Geometries)
            {
                Assert.IsNotNull(edge);
                Assert.IsTrue(edge.GetType() == typeof(LineString));
                Debug.WriteLine(edge);
            }

            // Build graph
            IDictionary<IEdge<Geometry>, double> consts = new Dictionary<IEdge<Geometry>, double>(edges.NumGeometries);
            var graph = new AdjacencyGraph<Geometry, IEdge<Geometry>>(true);
            foreach (LineString str in ((GeometryCollection)edges).Geometries)
            {
                // Add vertex 1
                Geometry vertex1 = str.StartPoint;
                Assert.IsNotNull(vertex1);
                if (!graph.ContainsVertex(vertex1))
                {
                    Debug.WriteLine(string.Format("Adding vertex {0} to the list", vertex1));
                    graph.AddVertex(vertex1);
                }
                else Debug.WriteLine(string.Format("Vertex {0} already present", vertex1));

                // Add vertex 2
                Geometry vertex2 = str.EndPoint;
                Assert.IsNotNull(vertex2);
                if (!graph.ContainsVertex(vertex2))
                {
                    Debug.WriteLine(string.Format("Adding vertex {0} to the list", vertex2));
                    graph.AddVertex(vertex2);
                }
                else Debug.WriteLine(string.Format("Vertex {0} already present", vertex2));

                // Compute weight
                double weight = weightComputer(str);
                Assert.Greater(weight, 0.0);

                // Add edge for 1 => 2
                IEdge<Geometry> edge1 = new Edge<Geometry>(vertex1, vertex2);
                Assert.IsNotNull(edge1);
                graph.AddEdge(edge1);
                consts.Add(edge1, weight);

                // Add edge for 2 => 1
                IEdge<Geometry> edge2 = new Edge<Geometry>(vertex2, vertex1);
                Assert.IsNotNull(edge2);
                graph.AddEdge(edge2);
                consts.Add(edge2, weight);
            }

            // Perform DijkstraShortestPathAlgorithm
            var dijkstra = new DijkstraShortestPathAlgorithm<Geometry, IEdge<Geometry>>(graph, e => consts[e]);

            // attach a distance observer to give us the shortest path distances
            var distObserver = new VertexDistanceRecorderObserver<Geometry, IEdge<Geometry>>(e => consts[e]);
            distObserver.Attach(dijkstra);

            // Attach a Vertex Predecessor Recorder Observer to give us the paths
            var predecessorObserver = new VertexPredecessorRecorderObserver<Geometry, IEdge<Geometry>>();
            predecessorObserver.Attach(dijkstra);

            // Run the algorithm
            Debug.WriteLine(string.Format("Starting algorithm from root vertex {0}", start));
            dijkstra.Compute(start);

            foreach (var kvp in distObserver.Distances)
                Debug.WriteLine(string.Format("Distance from root to node {0} is {1}",
                    kvp.Key, kvp.Value));
            foreach (var kvp in predecessorObserver.VertexPredecessors)
                Debug.WriteLine(string.Format("If you want to get to {0} you have to enter through the IN edge {1}", kvp.Key, kvp.Value));
            Check(graph, consts, predecessorObserver);

            // Detach the observers
            distObserver.Attach(dijkstra);
            predecessorObserver.Attach(dijkstra);
        }

        [Test]
        [Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedException(typeof(TopologyException))]
        public void CheckGraphBuilderExceptionUsingNoGeometries()
        {
            var builder = new GraphBuilder();
            builder.PrepareAlgorithm();
        }

        [Test]
        [Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedException(typeof(TopologyException))]
        public void CheckGraphBuilderExceptionUsingOneGeometry()
        {
            var builder = new GraphBuilder();
            Assert.IsTrue(builder.Add(a));
            builder.PrepareAlgorithm();
        }

        [Test]
        [Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedException(typeof(TopologyException))]
        public void CheckGraphBuilderExceptionUsingARepeatedGeometry()
        {
            var builder = new GraphBuilder();
            Assert.IsTrue(builder.Add(a));
            Assert.IsFalse(builder.Add(a));
            builder.PrepareAlgorithm();
        }

        [Test]
        [Microsoft.VisualStudio.TestTools.UnitTesting.ExpectedException(typeof(TopologyException))]
        public void CheckGraphBuilderExceptionUsingDifferentFactories()
        {
            var builder = new GraphBuilder();
            Assert.IsTrue(builder.Add(a));
            Assert.IsTrue(builder.Add(b, c));
            Assert.IsTrue(builder.Add(d));
            builder.Add(GeometryFactory.Default.CreateLineString(new Coordinate[]
            {
                new Coordinate(0 ,0),
                new Coordinate(50 , 50),
            }));
        }

        [Test]
        public void BuildGraphAndSearchShortestPathUsingGraphBuilder()
        {
            // Build algorithm
            var builder = new GraphBuilder();
            builder.Add(a);
            builder.Add(b, c);
            builder.Add(d);
            var algorithm = builder.PrepareAlgorithm();

            // Attach a distance observer to give us the shortest path distances
            var distObserver = new VertexDistanceRecorderObserver<Point, IEdge<Point>>(e => 1);
            distObserver.Attach(algorithm);

            // Attach a Vertex Predecessor Recorder Observer to give us the paths
            var predecessorObserver = new VertexPredecessorRecorderObserver<Point, IEdge<Point>>();
            predecessorObserver.Attach(algorithm);

            // Run algorithm
            algorithm.Compute(start);

            // Check results
            double distance = distObserver.Distances[end];
            Assert.AreEqual(2, distance);
            var predecessors = predecessorObserver.VertexPredecessors;
            for (int i = 0; i < distance; i++)
            {
                var edge = predecessors[end];
                if (i == 0)
                {
                    Assert.AreEqual(d.GetPointN(d.NumPoints - 2), edge.Source);
                    Assert.AreEqual(d.EndPoint, edge.Target);
                }
                else if (i == 1)
                {
                    Assert.AreEqual(a.StartPoint, edge.Source);
                    Assert.AreEqual(d.GetPointN(d.NumPoints - 2), edge.Target);
                }
                end = edge.Source;
            }

            // Detach the observers
            distObserver.Attach(algorithm);
            predecessorObserver.Attach(algorithm);
        }

        //[Ignore]
        [Test]
        public void BuildGraphBinary()
        {
            string path = "strade" + shp;
            Assert.IsTrue(File.Exists(path));

            //var reader = new ShapefileReader(path, factory);
            GeometryCollection coll =/* reader.ReadAll()*/default;
            Assert.IsNotNull(coll);
            Assert.IsNotEmpty(coll.Geometries);

            var result = coll.Geometries[0];
            for (int i = 1; i < coll.NumGeometries; i++)
            {
                Debug.WriteLine(string.Format("Union of {0}'th geometry", i));
                result = result.Union(coll.Geometries[i]);
            }
            Assert.IsNotNull(result);
            Assert.IsInstanceOf(typeof(MultiLineString), result);

            var wkbwriter = new WKBWriter();
            byte[] rawdata = wkbwriter.Write(result);
            Assert.IsNotEmpty(rawdata);

            path = "graph";
            if (File.Exists(path))
                File.Delete(path);
            Assert.IsFalse(File.Exists(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.Write(rawdata, 0, rawdata.Length);
            Assert.IsTrue(File.Exists(path));
        }

        //[Ignore]
        [Test]
        public void BuildShapefilesFromGraphBinary()
        {
            int index = 0;
            Geometry edges;
            var reader = /*new WKBReader(factory)*/default(WKBReader);
            using (var stream = new FileStream("graph", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                edges = reader.Read(stream);
                index++;
            }
            Assert.AreEqual(1, index);
            Assert.IsNotNull(edges);
            Assert.IsInstanceOf(typeof(MultiLineString), edges);
            Assert.AreEqual(1179, edges.NumGeometries);

            string field1 = "OBJECTID";
            string field2 = "DESCRIPTION";
            IList features = new List<Feature>(edges.NumGeometries);
            for (int i = 0; i < edges.NumGeometries; i++)
            {
                var ls = edges.GetGeometryN(i);
                Assert.IsInstanceOf(typeof(LineString), ls);

                var f = new Feature(ls, new AttributesTable());
                f.Attributes.Add(field1, i);
                f.Attributes.Add(field2, string.Format("length: {0}", Convert.ToInt64(ls.Length)));
                features.Add(f);
            }
            Assert.IsNotEmpty(features);
            Assert.AreEqual(edges.NumGeometries, features.Count);

            var header = new DbaseFileHeader();
            header.NumRecords = edges.NumGeometries;
            header.NumFields = 1;
            header.AddColumn(field1, 'N', 5, 0);
            header.AddColumn(field2, 'C', 254, 0);

            string path = "graph";
            if (File.Exists(path + shp))
                File.Delete(path + shp);
            Assert.IsFalse(File.Exists(path + shp));
            if (File.Exists(path + shx))
                File.Delete(path + shx);
            Assert.IsFalse(File.Exists(path + shx));
            if (File.Exists(path + dbf))
                File.Delete(path + dbf);
            Assert.IsFalse(File.Exists(path + dbf));

            //var writer = new ShapefileDataWriter(path, factory);
            //writer.Header = header;
            //writer.Write(features);

            Assert.IsTrue(File.Exists(path + shp));
            Assert.IsTrue(File.Exists(path + shx));
            Assert.IsTrue(File.Exists(path + dbf));

            IList subset = new List<Feature>(15);
            for (int i = 0; i < 15; i++)
                subset.Add(features[i]);
            Assert.IsNotEmpty(subset);
            Assert.AreEqual(15, subset.Count);

            path = "minimalgraph";
            if (File.Exists(path + shp))
                File.Delete(path + shp);
            Assert.IsFalse(File.Exists(path + shp));
            if (File.Exists(path + shx))
                File.Delete(path + shx);
            Assert.IsFalse(File.Exists(path + shx));
            if (File.Exists(path + dbf))
                File.Delete(path + dbf);
            Assert.IsFalse(File.Exists(path + dbf));

            //writer = new ShapefileDataWriter(path, factory);
            //writer.Header = header;
            //writer.Write(subset);

            Assert.IsTrue(File.Exists(path + shp));
            Assert.IsTrue(File.Exists(path + shx));
            Assert.IsTrue(File.Exists(path + dbf));
        }

        [Test]
        public void BuildGraphFromMinimalGraphShapefile()
        {
            string path = "minimalgraph.shp";
            int count = 15;
            Assert.IsTrue(File.Exists(path));
            //var reader = new ShapefileReader(path);
            var edges = /*reader.ReadAll()*/default(GeometryCollection);
            Assert.IsNotNull(edges);
            Assert.IsInstanceOf(typeof(GeometryCollection), edges);
            Assert.AreEqual(count, edges.NumGeometries);

            LineString startls = null;
            // Build graph
            var consts = new Dictionary<IEdge<Geometry>, double>(edges.NumGeometries);
            var graph = new AdjacencyGraph<Geometry, IEdge<Geometry>>(true);
            foreach (MultiLineString mlstr in edges.Geometries)
            {
                Assert.AreEqual(1, mlstr.NumGeometries);
                var str = (LineString)mlstr.GetGeometryN(0);
                if (startls == null)
                    startls = str;

                // Add vertex 1
                Geometry vertex1 = str.StartPoint;
                Assert.IsNotNull(vertex1);
                if (!graph.ContainsVertex(vertex1))
                {
                    Debug.WriteLine(string.Format("Adding vertex {0} to the list", vertex1));
                    graph.AddVertex(vertex1);
                }
                else Debug.WriteLine(string.Format("Vertex {0} already present", vertex1));

                // Add vertex 2
                Geometry vertex2 = str.EndPoint;
                Assert.IsNotNull(vertex2);
                if (!graph.ContainsVertex(vertex2))
                {
                    Debug.WriteLine(string.Format("Adding vertex {0} to the list", vertex2));
                    graph.AddVertex(vertex2);
                }
                else Debug.WriteLine(string.Format("Vertex {0} already present", vertex2));

                // Compute weight
                double weight = weightComputer(str);
                Assert.Greater(weight, 0.0);

                // Add edge 1 => 2
                IEdge<Geometry> edge1 = new Edge<Geometry>(vertex1, vertex2);
                Assert.IsNotNull(edge1);
                graph.AddEdge(edge1);
                consts.Add(edge1, weight);

                // Add edge 2 => 1
                IEdge<Geometry> edge2 = new Edge<Geometry>(vertex2, vertex1);
                Assert.IsNotNull(edge2);
                graph.AddEdge(edge2);
                consts.Add(edge2, weight);
            }

            // Perform DijkstraShortestPathAlgorithm
            var dijkstra = new DijkstraShortestPathAlgorithm<Geometry, IEdge<Geometry>>(graph, e => consts[e]);

            // attach a distance observer to give us the shortest path distances
            var distObserver = new VertexDistanceRecorderObserver<Geometry, IEdge<Geometry>>(e => 1);
            distObserver.Attach(dijkstra);

            // Attach a Vertex Predecessor Recorder Observer to give us the paths
            var predecessorObserver = new VertexPredecessorRecorderObserver<Geometry, IEdge<Geometry>>();
            predecessorObserver.Attach(dijkstra);

            // Run the algorithm
            Assert.IsNotNull(startls);
            Geometry startPoint = startls.StartPoint;
            Debug.WriteLine(string.Format("Starting algorithm from root vertex {0}", startPoint));
            dijkstra.Compute(startPoint);

            foreach (var kvp in distObserver.Distances)
                Debug.WriteLine(string.Format("Distance from root to node {0} is {1}",
                    kvp.Key, kvp.Value));
            foreach (var kvp in predecessorObserver.VertexPredecessors)
                Debug.WriteLine(string.Format(
                    "If you want to get to {0} you have to enter through the IN edge {1}", kvp.Key, kvp.Value));
            Check(graph, consts, predecessorObserver);

            // Detach the observers
            distObserver.Attach(dijkstra);
            predecessorObserver.Attach(dijkstra);
        }

        private void Check(IVertexSet<Geometry> graph, IDictionary<IEdge<Geometry>, double> consts,
            VertexPredecessorRecorderObserver<Geometry, IEdge<Geometry>> predecessorObserver)
        {
            foreach (var v in graph.Vertices)
            {
                double distance = 0;
                var vertex = v;
                IEdge<Geometry> predecessor;
                while (predecessorObserver.VertexPredecessors.TryGetValue(vertex, out predecessor))
                {
                    distance += consts[predecessor];
                    vertex = predecessor.Source;
                }
                Console.WriteLine("A -> {0}: {1}", v, distance);
            }
        }
    }
}
