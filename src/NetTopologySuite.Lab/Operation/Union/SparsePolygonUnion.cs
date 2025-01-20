using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Index;
using NetTopologySuite.Index.Strtree;

namespace NetTopologySuite.Operation.Union
{
    /// <summary>
    /// 稀疏多边形合并<para/>
    /// Unions a set of polygonal geometries by partitioning them
    /// into connected sets of polygons.
    /// This works best for a <i>sparse</i> set of polygons.
    /// Sparse means that if the geometries are partitioned
    /// into connected sets, the number of clusters
    /// is a significant fraction of the total number of geometries.
    /// The algorithm used provides performance and memory advantages
    /// over the <see cref="CascadedPolygonUnion"/> algorithm.
    /// It also has the advantage that it does not alter input geometries
    /// which do not intersect any other input geometry.
    /// <para/>
    /// Non-sparse sets will work, but may be slower than using cascaded union.
    /// </summary>
    /// <author>mdavis</author>
    public class SparsePolygonUnion
    {
        /// <summary>
        /// 合并
        /// </summary>
        /// <param name="geoms">拟合并的几何对象集合</param>
        /// <param name="isSimplify">合并的多边形是否简化 默认false</param>
        /// <returns></returns>
        public static Geometry Union(ICollection<Geometry> geoms, bool isSimplify = false)
        {
            var op = new SparsePolygonUnion(geoms);
            return op.Union(isSimplify);
        }

        /// <summary>
        /// 合并
        /// </summary>
        /// <param name="geoms"></param>
        /// <returns></returns>
        public static Geometry Union(Geometry geoms, bool isSimplify = false)
        {
            var polys = PolygonExtracter.GetPolygons(geoms);
            var op = new SparsePolygonUnion(polys);
            return op.Union(isSimplify);
        }

        private readonly ICollection<Geometry> _inputPolys;
        /// <summary>
        /// STR树
        /// </summary>
        private STRtree<PolygonNode> _index;
        private int _count;
        private readonly List<PolygonNode> _nodes = new List<PolygonNode>();
        //private GeometryFactory _geomFactory;

        /// <summary>
        /// 构造方法
        /// </summary>
        /// <param name="polys">多边形集合</param>
        public SparsePolygonUnion(ICollection<Geometry> polys)
        {
            this._inputPolys = polys;
            // guard against null input
            if (_inputPolys == null)
                _inputPolys = new List<Geometry>();
        }
        /// <summary>
        /// 合并
        /// </summary>
        /// <returns></returns>
        public Geometry Union(bool isSimplify = false)
        {
            if (_inputPolys.Count == 0)
                return null;

            LoadIndex(/*inputPolys*/);

            //--- cluster the geometries
            foreach (var queryNode in _nodes)
            {
                _index.Query(queryNode.Envelope, new PolygonNodeVisitor(queryNode));
            }

            //--- compute union of each cluster
            var clusterGeom = new List<Geometry>();
            foreach (var node in _nodes)
            {
                var geom = node.Union();
                if (geom == null) continue;
                // 去除多边形边多余点
                if (isSimplify && geom is Polygon polygon)
                {
                    var simplifyPolygon = Simplify.TopologyPreservingSimplifier.Simplify(polygon, 0.0005);
                    clusterGeom.Add(simplifyPolygon);
                }
                else clusterGeom.Add(geom);
            }

            var geomFactory = _inputPolys.First().Factory;
            return geomFactory.BuildGeometry(clusterGeom);
        }
        /// <summary>
        /// 载入(多边形)到STR树
        /// </summary>
        private void LoadIndex(/*IEnumerable<Geometry> inputPolys*/)
        {
            _index = new STRtree<PolygonNode>();
            foreach (var geom in _inputPolys)
            {
                Add(geom);
            }
        }

        private void Add(Geometry poly)
        {
            var node = new PolygonNode(_count++, poly);
            _nodes.Add(node);
            _index.Insert(poly.EnvelopeInternal, node);
        }
        /// <summary>
        /// 多边形节点类
        /// </summary>
        private class PolygonNode
        {
            private readonly int _id;
            private bool _isFree = true;
            private readonly Geometry _poly;
            private PolygonNode _root;
            private List<PolygonNode> _nodes;

            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="id">序号</param>
            /// <param name="poly">多边形</param>
            public PolygonNode(int id, Geometry poly)
            {
                _id = id;
                _poly = poly;
            }

            public int Id
            {
                get => _id;
            }

            public Geometry Polygon => _poly;
            /// <summary>
            /// 矩形包围框
            /// </summary>
            public Envelope Envelope => _poly.EnvelopeInternal;

            //public bool Intersects(PolygonNode node)
            //{
            //    // this would benefit from having a short-circuiting intersects 
            //    var pg = PreparedGeometryFactory.Prepare(_poly);
            //    return pg.Intersects(node._poly);
            //    //return poly.intersects(node.poly);
            //}

            public bool IsInSameCluster(PolygonNode node)
            {
                if (_isFree || node._isFree) return false;
                return _root == node._root;
            }

            public void Merge(PolygonNode node)
            {
                if (this == node)
                    throw new ArgumentException("Can't merge node with itself");

                if (Id < node.Id)
                {
                    Add(node);
                }
                else
                {
                    node.Add(this);
                }
            }

            private void InitCluster()
            {
                _isFree = false;
                _root = this;
                _nodes = new List<PolygonNode>();
                _nodes.Add(this);
            }

            private void Add(PolygonNode node)
            {
                if (_isFree) InitCluster();

                if (node._isFree)
                {
                    node._isFree = false;
                    node._root = _root;
                    _root._nodes.Add(node);
                }
                else
                {
                    _root.MergeRoot(node.Root);
                }
            }

            /// <summary>
            /// Add the other root's nodes to this root's list.
            /// Set the other nodes to have this as root.
            /// Free the other root's node list.
            /// </summary>
            /// <param name="root">The other root noe</param>
            private void MergeRoot(PolygonNode root)
            {
                if (_nodes == root._nodes)
                    throw new InvalidOperationException("Attempt to merge same cluster");

                foreach (var node in root._nodes)
                {
                    _nodes.Add(node);
                    node._root = this;
                }

                root._nodes = null;
            }

            private PolygonNode Root
            {
                get
                {
                    if (_isFree)
                        throw new InvalidOperationException("free node has no root");
                    if (_root != null)
                        return _root;
                    return this;
                }
            }

            public Geometry Union()
            {
                // free polys are returned unchanged
                if (_isFree) return _poly;
                // only root nodes can compute a union
                if (_root != this) return null;
                return CascadedPolygonUnion.Union(ToPolygons(_nodes));
            }

            private static List<Geometry> ToPolygons(ICollection<PolygonNode> nodes)
            {
                var polys = new List<Geometry>(nodes.Count);
                foreach (var node in nodes)
                    polys.Add(node._poly);
                return polys;
            }

        }

        private class PolygonNodeVisitor : IItemVisitor<PolygonNode>
        {
            private readonly PolygonNode _queryNode;
            private readonly IPreparedGeometry _prep;

            public PolygonNodeVisitor(PolygonNode queryNode)
            {
                _queryNode = queryNode;
                _prep = PreparedGeometryFactory.Prepare(queryNode.Polygon);
            }

            public void VisitItem(PolygonNode node)
            {
                if (node == _queryNode) return;
                // avoid duplicate intersections
                if (node.Id > _queryNode.Id) return;
                if (_queryNode.IsInSameCluster(node)) return;
                if (!_prep.Intersects(node.Polygon)) return;
                _queryNode.Merge(node);
            }
        }

    }
}
