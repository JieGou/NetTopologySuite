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

            var path = new GraphBuilder2(true);
            path.Add(lineStrings.ToArray());
            path.Initialize();

            #region 最小生成树？

            var graph = path.graph;
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

                        #region 从指定起点输出所有的路径
                        var allPaths = new List<Tuple<List<IEdge<Coordinate>>, double>>();
                        var root = dwgReader.ReadCoordinate(startPt);
                        foreach (var target in graph.Vertices)
                        {
                            if (root.Equals(target)) continue;
                            var tryGetPath = graph.ShortestPathsDijkstra(e => e.Source.Distance(e.Target), root);
                            if (tryGetPath(target, out var itemPath))
                            {
                                allPaths.Add(new Tuple<List<IEdge<Coordinate>>, double>(itemPath.ToList(), itemPath.Sum(e => e.Source.Distance(e.Target))));
                            }
                        }
                        var longestPath = allPaths.OrderByDescending(t => t.Item2).First();
                        var longestLineString = path.BuildString(longestPath.Item1);

                        #endregion

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
                            var startCoordinate = dwgReader.ReadCoordinate(startPt);
                            var endCoordinate = dwgReader.ReadCoordinate(endPt);
                            var shortestPath = path.Perform(startCoordinate, endCoordinate);
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
                                    Entity outEnt = writer.WritePolyline(shortestPath);
                                    outEnt.ColorIndex = 1;
                                    btr.AppendEntity(outEnt);
                                    tr.AddNewlyCreatedDBObject(outEnt, true);

                                    //输出最长主路径 黄色
                                    Entity longPl = writer.WritePolyline(longestLineString);
                                    longPl.ColorIndex = 2;
                                    btr.AppendEntity(longPl);
                                    tr.AddNewlyCreatedDBObject(longPl, true);
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
