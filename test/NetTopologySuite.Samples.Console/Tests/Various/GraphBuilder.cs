using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using NUnit.Framework;
using QuickGraph;
using QuickGraph.Algorithms.ShortestPath;

namespace GisSharpBlog.NetTopologySuite.Samples.Tests.Various
{
    [Obsolete("use GraphBuilder2", false)]
    public class GraphBuilder
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public delegate double ComputeWeightDelegate(LineString line);

        private static readonly ComputeWeightDelegate DefaultComputer =
            delegate (LineString line) { return line.Length; };

        private GeometryFactory factory;
        private readonly ISet<LineString> strings;

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphBuilder"/> class.
        /// </summary>
        public GraphBuilder()
        {
            factory = null;
            strings = new HashSet<LineString>();
        }

        /// <summary>
        /// Adds one or more lines to the graph.
        /// </summary>
        /// <param name="lines">A generic linestring.</param>
        /// <returns><c>true</c> if all elements are inserted, <c>false</c> otherwise</returns>
        public bool Add(params LineString[] lines)
        {
            bool result = true;
            foreach (var line in lines)
            {
                var newfactory = line.Factory;
                if (factory == null)
                    factory = newfactory;
                else if (!newfactory.PrecisionModel.Equals(factory.PrecisionModel))
                    throw new TopologyException("all geometries must have the same precision model");

                if (result)
                    result = strings.Add(line);
            }
            return result;
        }

        /// <summary>
        ///
        /// </summary>
        /// <remarks>
        /// Initializes the builder using a function
        /// that computes the weights using <see cref="LineString">edge</see>'s length.
        /// </remarks>
        /// <returns></returns>
        public DijkstraShortestPathAlgorithm<Point, IEdge<Point>> PrepareAlgorithm()
        {
            return PrepareAlgorithm(DefaultComputer);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="computer">
        /// A function that computes the weight
        /// of any <see cref="LineString">edge</see> of the graph
        /// </param>
        /// <returns></returns>
        public DijkstraShortestPathAlgorithm<Point, IEdge<Point>> PrepareAlgorithm(ComputeWeightDelegate computer)
        {
            if (strings.Count < 2)
                throw new TopologyException("you must specify two or more geometries to build a graph");

            var edges = BuildEdges();

            var consts = new Dictionary<IEdge<Point>, double>(edges.NumGeometries);
            var graph = new AdjacencyGraph<Point, IEdge<Point>>(true);
            foreach (LineString str in edges.Geometries)
            {
                var vertex1 = str.StartPoint;
                Assert.IsTrue(vertex1 != null);
                if (!graph.ContainsVertex(vertex1))
                    graph.AddVertex(vertex1);

                var vertex2 = str.EndPoint;
                Assert.IsTrue(vertex2 != null);
                if (!graph.ContainsVertex(vertex2))
                    graph.AddVertex(vertex2);

                double weight = computer(str);
                var edge = new Edge<Point>(vertex1, vertex2);
                Assert.IsTrue(edge != null);

                graph.AddEdge(edge);
                consts.Add(edge, weight);
            }

            // Use Dijkstra
            return new DijkstraShortestPathAlgorithm<Point, IEdge<Point>>(graph, e => consts[e]);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private MultiLineString BuildEdges()
        {
            Geometry temp = null;
            foreach (var line in strings)
            {
                if (temp == null)
                    temp = line;
                else temp = temp.Union(line);
            }

            MultiLineString edges;
            if (temp == null || temp.NumGeometries == 0)
                edges = MultiLineString.Empty;
            else if (temp.NumGeometries == 1)
                edges = factory.CreateMultiLineString(new LineString[] { (LineString)temp, });
            else edges = (MultiLineString)temp;
            return edges;
        }
    }
}
