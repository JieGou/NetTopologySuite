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
        /// <param name="allRootPaths">从根节点出发的路径</param>
        /// <param name="root">根节点</param>
        /// <returns></returns>
        private List<Tuple<List<IEdge<Coordinate>>, int>> GetAllMainAndBranchPathsFromRoot(AdjacencyGraph<Coordinate, IEdge<Coordinate>> graph, List<Tuple<List<IEdge<Coordinate>>, double>> allRootPaths, Coordinate root)
        {
            var mainAndBranchPaths = new List<Tuple<List<IEdge<Coordinate>>, int>>();

            //图中所有的节点
            var allVertexs = graph.Vertices.ToList();
            //已经遍历过的节点
            var visitedVertexs = new List<Coordinate>();

            int colorIndex = 0;
            foreach (var itemPath in allRootPaths)
            {
                if (itemPath.Item1.Any(e => visitedVertexs.Any(vx => vx.Equals(e.Source) || visitedVertexs.Any(v => v.Equals(e.Target))))) continue;

                var itemPathVertexs = GetPathVertexs(itemPath);
                itemPathVertexs.Remove(root);
                visitedVertexs.AddRange(itemPathVertexs);

                mainAndBranchPaths.Add(new Tuple<List<IEdge<Coordinate>>, int>(itemPath.Item1, colorIndex));
            }
            // <image url="$(ProjectDir)\DocumentImages\HierarchicalPath.png"/>
            //TODO 递归处理分支,每一级分支用不同的颜色表示
            while (allVertexs.Except(visitedVertexs).Any())
            {
                break;
            }
            return mainAndBranchPaths;
        }

        private List<Coordinate> GetPathVertexs(Tuple<List<IEdge<Coordinate>>, double> longestPath)
        {
            var longestPathVertexs = new List<Coordinate>();

            foreach (var edge in longestPath.Item1)
            {
                var sourceVertex = edge.Source;
                if (sourceVertex != null && !longestPathVertexs.Any(v => v.Equals(sourceVertex))) longestPathVertexs.Add(sourceVertex);
                var targetVertex = edge.Target;
                if (targetVertex != null && !longestPathVertexs.Any(v => v.Equals(targetVertex))) longestPathVertexs.Add(targetVertex);
            }
            return longestPathVertexs;
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
                    var allRootPaths = GetAllPathFromRoot(graph, root);

                    var db = HostApplicationServices.WorkingDatabase;
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                        var btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                        allRootPaths = allRootPaths.OrderByDescending(t => t.Item2).ToList();

                        var rootPaths = GetAllMainAndBranchPathsFromRoot(graph, allRootPaths, root);

                        foreach (var itemPathTuple in rootPaths)
                        {
                            var itemPath = itemPathTuple.Item1;
                            int colorIndex = itemPathTuple.Item2;
                            var lineString = graphBuilder.BuildString(itemPath);

                            //输出最长主路径 黄色
                            Entity longestPolyline = writer.WritePolyline(lineString);
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

        private List<Tuple<List<IEdge<Coordinate>>, double>> GetAllPathFromRoot(AdjacencyGraph<Coordinate, IEdge<Coordinate>> graph, Coordinate root)
        {
            var allPaths = new List<Tuple<List<IEdge<Coordinate>>, double>>();

            foreach (var target in graph.Vertices)
            {
                if (root.Equals(target)) continue;
                var tryGetPath = graph.ShortestPathsDijkstra(e => e.Source.Distance(e.Target), root);
                if (tryGetPath(target, out var itemPath))
                {
                    allPaths.Add(new Tuple<List<IEdge<Coordinate>>, double>(itemPath.ToList(), itemPath.Sum(e => e.Source.Distance(e.Target))));
                }
            }
            return allPaths;
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
    }
}
