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
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using NetTopologySuite.Geometries;
using Acap = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using NetTopologySuite.EdgeGraph;
using NetTopologySuite.GeometriesGraph;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
using QuickGraph;
using QuickGraph.Algorithms;
using System;
using QuickGraph.Algorithms.ConnectedComponents;

namespace Topology.IO.Dwg.CS
{
    public class Commands
    {
        [CommandMethod("TestDwgReader", CommandFlags.UsePickSet)]
        public void CheckForPickfirstSelection()
        {
            var doc = Acap.DocumentManager.MdiActiveDocument.Editor;

            var acSSPrompt = doc.SelectImplied();

            SelectionSet acSSet = default;

            if (acSSPrompt.Status != PromptStatus.OK)
                Acap.ShowAlertDialog("Number of objects in Pickfirst selection: 0");
            else
            {
                acSSet = acSSPrompt.Value;
                Acap.ShowAlertDialog("Number of objects in Pickfirst selection: " + acSSet.Count.ToString());
            }

            if (acSSet == null || acSSet.Count == 0)
            {
                var idarrayEmpty = new ObjectId[0];
                doc.SetImpliedSelection(idarrayEmpty);

                acSSPrompt = doc.GetSelection();
            }

            if (acSSPrompt.Status != PromptStatus.OK)
            {
                Acap.ShowAlertDialog("Number of objects selected: 0");
                return;
            }

            acSSet = acSSPrompt.Value;

            Acap.ShowAlertDialog("Number of objects selected: " + acSSet.Count.ToString());

            var ids = acSSet.GetObjectIds();
            var geometrys = new DwgReader().ReadGeometryCollection(ids);

            //转换为图
            var g = new GeometryGraph(0, geometrys);
        }

        /// <summary>
        /// 最短路径
        /// </summary>
        [CommandMethod("ShortestPath")]
        public void ShortestPath()
        {
            var doc = Acap.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var values = new[] { new TypedValue((int)DxfCode.Start, "*LINE") };
            var filter = new SelectionFilter(values);

            var selOptions = new PromptSelectionOptions();
            selOptions.MessageForAdding = "选择管网:";
            selOptions.AllowDuplicates = false;
            selOptions.SingleOnly = false;
            var acSSPrompt = ed.GetSelection(selOptions, filter);

            if (acSSPrompt.Status != PromptStatus.OK)
            {
                Acap.ShowAlertDialog("Number of objects selected: 0");
                return;
            }

            var acSSet = acSSPrompt.Value;
            var ids = acSSet.GetObjectIds();

            var dwgReader = new DwgReader();
            var writer = new DwgWriter();

            var geometrys = dwgReader.ReadGeometryCollection(ids);
            var lineStrings = geometrys.Where(g => g != null && g is LineString).Cast<LineString>().ToList();

            var graphBuilder = new GraphBuilder2(true);
            graphBuilder.Add(lineStrings.ToArray());
            graphBuilder.Initialize();

            #region 最小生成树？

            var graph = graphBuilder.graph;
            var vertexs = graph.Vertices;
            var edges = graph.Edges;
            var undirectedGraph = BuildGraph(vertexs.ToList().ToArray(), edges.ToArray());
            var kruskal = new List<IEdge<Coordinate>>(undirectedGraph.MinimumSpanningTreeKruskal(e => 1));

            #endregion 最小生成树？

            //交互 选择线集合，起点、终点
            var p1O = new PromptPointOptions("\n选择路径起点:");
            PromptPointResult p1;
            while (true)
            {
                p1 = ed.GetPoint(p1O);
                if (p1.Status == PromptStatus.Cancel) break;
                if (p1.Status != PromptStatus.OK) break;
                else
                {
                    while (true)
                    {
                        var startPt = p1.Value;

                        var pPtOpts = new PromptPointOptions("")
                        {
                            Message = "\n选择终点: ",
                            UseBasePoint = true,
                            BasePoint = startPt
                        };

                        var p2 = ed.GetPoint(pPtOpts);
                        if (p2.Status != PromptStatus.OK) { return; }

                        var endPt = p2.Value;
                        if (startPt.X == endPt.X && startPt.Y == endPt.Y)
                        {
                            ed.WriteMessage("\n起点终点不能重合!");
                        }
                        else
                        {
                            var root = dwgReader.ReadCoordinate(startPt);
                            var endCoordinate = dwgReader.ReadCoordinate(endPt);
                            var shortestPath = graphBuilder.Perform(root, endCoordinate);
                            if (shortestPath == null)
                            {
                                ed.WriteMessage("\n未找到路径");
                                return;
                            }

                            //合并
                            //var merger = new NetTopologySuite.Operation.Linemerge.LineMerger();
                            //merger.Add(shortestPath);

                            var db = HostApplicationServices.WorkingDatabase;
                            using (var tr = db.TransactionManager.StartTransaction())
                            {
                                var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                                var btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                                //foreach (LineString lineString in merger.GetMergedLineStrings())
                                {
                                    //输出最短路径 红色
                                    Entity shortestPolyline = writer.WritePolyline(shortestPath);
                                    shortestPolyline.ColorIndex = 1;
                                    btr.AppendEntity(shortestPolyline);
                                    tr.AddNewlyCreatedDBObject(shortestPolyline, true);
                                }

                                tr.Commit();
                                tr.Dispose();
                            }
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取从指定点开始的所有主路径及支路径
        /// </summary>
        /// <param name="parentGraph">父图</param>
        /// <param name="root">根节点</param>
        /// <returns></returns>
        private List<Tuple<List<UndirectedEdge<Coordinate>>, int>> GetAllMainAndBranchPathsFromRoot(UndirectedGraph<Coordinate, UndirectedEdge<Coordinate>> parentGraph, Coordinate root)
        {
            //从根节点出发的路径
            var allRootPaths = GetAllPathFromRoot(parentGraph, root);

            //主/分路径 Tuple Item1表示路径边集合 Item2表示层次等级
            var mainAndBranchPaths = new List<Tuple<List<UndirectedEdge<Coordinate>>, int>>();

            //已经遍历过的节点 不允许重复
            var visitedVertexs = new HashSet<Coordinate>();

            var allExcludedEdges = new List<UndirectedEdge<Coordinate>>();//需要排除的边

            //等级边词典
            var hierarchyLevelEdgeDic = new Dictionary<int, List<UndirectedEdge<Coordinate>>>();
            int hierarchyLevelIndex = 0;
            hierarchyLevelEdgeDic[hierarchyLevelIndex] = new List<UndirectedEdge<Coordinate>>();
            foreach (var itemPath in allRootPaths)
            {
                //当前路径的边集合
                var itemEdgeList = itemPath.Item1;
                //当前边集合中的起点或终点被记录了跳过——因为最长排序在前，所以这里能加入的路径条数为根节点的邻接点个数，其它都跳过
                if (itemEdgeList.Any(e =>
                {
                    var sourcePt = e.Source;
                    var targetPt = e.Target;
                    return visitedVertexs.Any(vx => !vx.Equals2D(root) && (vx.Equals2D(sourcePt) || vx.Equals2D(targetPt)));
                })) continue;
                allExcludedEdges.AddRange(itemEdgeList);
                hierarchyLevelEdgeDic[hierarchyLevelIndex].AddRange(itemEdgeList);

                var itemPathVertex = GetPathVertexs(itemEdgeList);
                itemPathVertex.ForEach(itemV => visitedVertexs.Add(itemV));

                mainAndBranchPaths.Add(new Tuple<List<UndirectedEdge<Coordinate>>, int>(itemEdgeList, hierarchyLevelIndex));
            }
            // <image url="$(ProjectDir)\DocumentImages\HierarchicalPath.png"/>

            while (parentGraph.Edges.ToList().Count > 0)
            {
                for (int i = 0; i < mainAndBranchPaths.Count; i++)
                {
                    //主路径找到后，删除第一路径
                    EdgePredicate<Coordinate, UndirectedEdge<Coordinate>> isLoop = e =>
                    mainAndBranchPaths[i].Item1.Any(itemEdge => (e.Source.Equals2D(itemEdge.Source) && e.Target.Equals2D(itemEdge.Target)) ||
                                                                (e.Source.Equals2D(itemEdge.Target) && e.Target.Equals2D(itemEdge.Source)));
                    parentGraph.RemoveEdgeIf(isLoop);
                }
                if (parentGraph.Edges.ToList().Count == 0) break;

                //检查图的 Component个数
                var dfs = new ConnectedComponentsAlgorithm<Coordinate, UndirectedEdge<Coordinate>>(parentGraph);
                dfs.Compute();

                //子级图
                var subUndirectedGraphList = new List<UndirectedGraph<Coordinate, UndirectedEdge<Coordinate>>>();
                if (dfs.ComponentCount >= 1)
                {
                    hierarchyLevelIndex++;

                    // 分别得到各Component的图
                    var subGraphs = parentGraph.GetSubComponentGraphs(dfs);

                    foreach (var itemUndirectedGraph in subGraphs)
                    {
                        var subGraph = (UndirectedGraph<Coordinate, UndirectedEdge<Coordinate>>)itemUndirectedGraph;
                        if (subGraph == null || subGraph.EdgeCount < 1) continue;
                        subUndirectedGraphList.Add(subGraph);
                    }
                    subUndirectedGraphList = subUndirectedGraphList.OrderByDescending(x => x.EdgeCount).ToList();

                    //获取子级图的入口点
                    hierarchyLevelEdgeDic[hierarchyLevelIndex] = new List<UndirectedEdge<Coordinate>>();
                    foreach (var itemSubGraph in subUndirectedGraphList)
                    {
                        var subGrapRoot = GetGraphRoot(itemSubGraph, hierarchyLevelEdgeDic[hierarchyLevelIndex - 1]);
                        if (subGrapRoot == null) continue;
                        var allItemSubRootPaths = GetAllPathFromRoot(itemSubGraph, subGrapRoot);

                        //对次一级子图进行主路径和支路径的输出
                        foreach (var itemPath in allItemSubRootPaths)
                        {
                            //当前路径的边集合
                            var itemEdgeList = itemPath.Item1;
                            //当前边集合中的起点或终点被记录了的跳过——因为最长排序在前，所以这里加入的条数为根节点的邻接点个数，其它都跳过
                            if (itemEdgeList.Any(e =>
                            {
                                var sourcePt = e.Source;
                                var targetPt = e.Target;
                                return visitedVertexs.Any(vx => !vx.Equals2D(subGrapRoot) && (vx.Equals2D(sourcePt) || vx.Equals2D(targetPt)));
                            })) continue;
                            allExcludedEdges.AddRange(itemEdgeList);
                            hierarchyLevelEdgeDic[hierarchyLevelIndex].AddRange(itemEdgeList);

                            var itemPathVertex = GetPathVertexs(itemEdgeList);
                            itemPathVertex.ForEach(itemV => visitedVertexs.Add(itemV));

                            mainAndBranchPaths.Add(new Tuple<List<UndirectedEdge<Coordinate>>, int>(itemEdgeList, hierarchyLevelIndex));
                        }
                    }
                }
            }
            return mainAndBranchPaths;
        }

        /// <summary>
        /// 获取子图的根节点
        /// </summary>
        /// <param name="itemSubGraph"></param>
        /// <param name="upEdges">上一级的边集合</param>
        /// <returns></returns>
        private Coordinate GetGraphRoot(UndirectedGraph<Coordinate, UndirectedEdge<Coordinate>> itemSubGraph, List<UndirectedEdge<Coordinate>> upEdges)
        {
            var upVertexs = GetPathVertexs(upEdges);
            return upVertexs.FirstOrDefault(v => itemSubGraph.Vertices.Any(vx => vx.Equals2D(v)));
        }

        /// <summary>
        /// 获取边集合的点
        /// </summary>
        /// <param name="edgeList">边集合</param>
        /// <returns>点列表</returns>
        private List<Coordinate> GetPathVertexs(List<UndirectedEdge<Coordinate>> edgeList)
        {
            var longestPathVertexs = new List<Coordinate>();

            foreach (var edge in edgeList)
            {
                var sourceVertex = edge.Source;
                if (sourceVertex != null && !longestPathVertexs.Any(v => v.Equals(sourceVertex))) longestPathVertexs.Add(sourceVertex);
                var targetVertex = edge.Target;
                if (targetVertex != null && !longestPathVertexs.Any(v => v.Equals(targetVertex))) longestPathVertexs.Add(targetVertex);
            }
            return longestPathVertexs;
        }

        //TODO 喷淋标注(管径计算)
        [CommandMethod("CmdSprayCalAndDim")]
        public void CmdSprayCalAndDim()
        {
            //Reference from 超快速的喷淋计算标注程序 http://bbs.mjtd.com/thread-170300-1-1.html
            /*
             1、几何线构造图
             2、构造指定根节点的有向图
             3、对喷头作一个偏移，去除末端的，构造Edge添加到图 并作标记为虚拟边
             4、按 Shreve order计算更新tag值中的值
             5、对非虚拟边根据信息进行标注
             */
        }
        /// <summary>
        /// 管网主管支管
        /// </summary>
        [CommandMethod("CmdMainAndBranch")]
        public void CmdMainAndBranch()
        {
            var doc = Acap.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var values = new[] { new TypedValue((int)DxfCode.Start, "*LINE") };
            var filter = new SelectionFilter(values);

            var selOptions = new PromptSelectionOptions
            {
                MessageForAdding = "选择管网:",
                AllowDuplicates = false,
                SingleOnly = false
            };
            var acSSPrompt = ed.GetSelection(selOptions, filter);

            if (acSSPrompt.Status != PromptStatus.OK)
            {
                Acap.ShowAlertDialog("Number of objects selected: 0");
                return;
            }

            var acSSet = acSSPrompt.Value;
            var ids = acSSet.GetObjectIds();

            var dwgReader = new DwgReader();
            var writer = new DwgWriter();

            var geometrys = dwgReader.ReadGeometryCollection(ids);
            var lineStrings = geometrys.Where(g => g != null && g is LineString).Cast<LineString>().ToList();

            var graphBuilder = new GraphBuilder2(true);
            graphBuilder.Add(lineStrings.ToArray());
            graphBuilder.Initialize();

            var graph = graphBuilder.graph;
            var vertexs = graph.Vertices;
            var edges = graph.Edges;
            var undirectedGraph = BuildGraph(vertexs.ToList().ToArray(), edges.ToArray());

            //TODO 支持多个管网
            //检查图的 Component个数
            var dfs = new ConnectedComponentsAlgorithm<Coordinate, UndirectedEdge<Coordinate>>(undirectedGraph);
            dfs.Compute();

            var undirectedGraphList = new List<UndirectedGraph<Coordinate, UndirectedEdge<Coordinate>>>();
            if (dfs.ComponentCount <= 1)
            {
                undirectedGraphList.Add(undirectedGraph);
            }
            else
            {
                // 分别得到各Component的图
                var subGraphs = undirectedGraph.GetSubComponentGraphs(dfs);

                foreach (var itemUndirectedGraph in subGraphs)
                {
                    var subGraph = (UndirectedGraph<Coordinate, UndirectedEdge<Coordinate>>)itemUndirectedGraph;
                    if (subGraph == null || subGraph.EdgeCount < 1) continue;
                    undirectedGraphList.Add(subGraph);
                }
            }

            int componentGraphCount = undirectedGraphList.Count;
            //改造为for循环，用户可以任意选则其中一个网管所包含的点
            for (int i = 0; i < componentGraphCount; i++)
            {
                var itemUndirectedGraph = undirectedGraphList[i];
                //交互 选择线集合，起点、终点
                var pointPrompt = new PromptPointOptions("\n选择路径起点:");
                PromptPointResult pointPromptResult;
                while (true)
                {
                    pointPromptResult = ed.GetPoint(pointPrompt);
                    if (pointPromptResult.Status == PromptStatus.Cancel) break;
                    if (pointPromptResult.Status != PromptStatus.OK) break;
                    else
                    {
                        var startPt = pointPromptResult.Value;
                        var root = dwgReader.ReadCoordinate(startPt);
                        //TODO 不在管网上的 提示重新选择点
                        if (itemUndirectedGraph.Vertices.All(pt => !pt.Equals2D(root)))
                        {
                            ed.WriteMessage("\n点不在网管上，请重新选择");
                            continue;
                        }
                        var db = HostApplicationServices.WorkingDatabase;
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                            var btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                            var rootPaths = GetAllMainAndBranchPathsFromRoot(itemUndirectedGraph, root);
                            int widthBase = 0;
                            int levelCount = rootPaths.Max(p => p.Item2);
                            foreach (var itemPathTuple in rootPaths)
                            {
                                var itemPath = itemPathTuple.Item1;
                                int hierarchicalLevel = itemPathTuple.Item2;

                                //颜色序号
                                int colorIndex = hierarchicalLevel + 1;
                                int width = widthBase * (levelCount - hierarchicalLevel + 1);

                                var lineString = graphBuilder.BuildString(itemPath);

                                //输出最长主路径 黄色
                                var longestPolyline = writer.WritePolyline(lineString, width);
                                longestPolyline.ColorIndex = colorIndex;
                                btr.AppendEntity(longestPolyline);
                                tr.AddNewlyCreatedDBObject(longestPolyline, true);
                            }

                            tr.Commit();
                            tr.Dispose();
                        }
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 获取从指定根节点出发的所有路径
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="root"></param>
        /// <returns></returns>
        private List<Tuple<List<UndirectedEdge<Coordinate>>, double>> GetAllPathFromRoot(UndirectedGraph<Coordinate, UndirectedEdge<Coordinate>> graph, Coordinate root)
        {
            var allPaths = new List<Tuple<List<UndirectedEdge<Coordinate>>, double>>();

            foreach (var target in graph.Vertices)
            {
                if (root.Equals(target)) continue;
                var tryGetPath = graph.ShortestPathsDijkstra(e => e.Source.Distance(e.Target), root);
                if (tryGetPath(target, out var itemPath))
                {
                    allPaths.Add(new Tuple<List<UndirectedEdge<Coordinate>>, double>(itemPath.ToList(), itemPath.Sum(e => e.Source.Distance(e.Target))));
                }
            }
            return allPaths.OrderByDescending(t => t.Item2).ToList();
        }

        //IEdge<Coordinate>
        private UndirectedGraph<Coordinate, UndirectedEdge<Coordinate>> BuildGraph(Coordinate[] verticies, IEdge<Coordinate>[] edges)
        {
            var graph = new UndirectedGraph<Coordinate, UndirectedEdge<Coordinate>>();
            graph.AddVertexRange(verticies);
            var convEdges = new UndirectedEdge<Coordinate>[edges.Length];
            for (int i = 0; i < edges.Length; i++)
            {
                convEdges[i] = new UndirectedEdge<Coordinate>(edges[i].Source, edges[i].Target);
            }
            graph.AddEdgeRange(convEdges);
            return graph;
        }

        //from https://www.theswamp.org/index.php?topic=20226.msg245827#msg245827
        /// <summary>
        /// 合并线
        /// </summary>
        [CommandMethod("MERGELINEWORK")]
        public static void MergeLinework()
        {
            var ed = Acap.DocumentManager.MdiActiveDocument.Editor;

            var values = new[] { new TypedValue((int)DxfCode.Start, "*LINE") };
            var filter = new SelectionFilter(values);

            var selOptions = new PromptSelectionOptions();
            selOptions.MessageForAdding = "Select objects:";
            selOptions.AllowDuplicates = false;
            selOptions.SingleOnly = false;

            var result = ed.GetSelection(selOptions, filter);
            if (result.Status == PromptStatus.OK)
            {
                var db = HostApplicationServices.WorkingDatabase;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var reader = new DwgReader();
                    var writer = new DwgWriter();

                    var selSet = result.Value;
                    var merger = new NetTopologySuite.Operation.Linemerge.LineMerger();
                    foreach (var objId in selSet.GetObjectIds())
                    {
                        var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        var geometry = reader.ReadGeometry(ent);
                        merger.Add(geometry);
                    }

                    var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                    foreach (LineString lineString in merger.GetMergedLineStrings())
                    {
                        Entity outEnt = writer.WritePolyline(lineString);
                        outEnt.ColorIndex = 1;
                        btr.AppendEntity(outEnt);
                        tr.AddNewlyCreatedDBObject(outEnt, true);
                    }

                    tr.Commit();
                    tr.Dispose();
                }
            }
        }

        /// <summary>
        /// 从Graph中指定节点获取排除某些边之后的子图
        /// </summary>
        /// <param name="graph">源图</param>
        /// <param name="startNode">指定起始节点</param>
        /// <param name="excludedEdges">该节点需要排除的边</param>
        /// <returns></returns>

        public UndirectedGraph<Coordinate, UndirectedEdge<Coordinate>> GetSubgraph(UndirectedGraph<Coordinate, UndirectedEdge<Coordinate>> graph,
                                                                         Coordinate startNode,
                                                                         List<IEdge<Coordinate>> excludedEdges)
        {
            var visited = new Dictionary<Coordinate, bool>();
            var subgraph = new UndirectedGraph<Coordinate, UndirectedEdge<Coordinate>>();
            var queue = new Queue<Coordinate>();

            queue.Enqueue(startNode);
            visited[startNode] = true;

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();

                // Iterate through all adjacent vertices
                foreach (var adjacentEdge in graph.AdjacentEdges(node))
                {
                    if (excludedEdges.Contains(adjacentEdge) || visited[adjacentEdge.Source] && visited[adjacentEdge.Target])
                    {
                        continue;
                    }

                    visited[adjacentEdge.Target] = true;
                    visited[adjacentEdge.Source] = true;

                    if (!node.Equals(adjacentEdge.Target)) queue.Enqueue(adjacentEdge.Target);
                    if (!node.Equals(adjacentEdge.Source)) queue.Enqueue(adjacentEdge.Source);

                    subgraph.AddEdge(adjacentEdge);
                }
            }
            return subgraph;
        }
    }
}
