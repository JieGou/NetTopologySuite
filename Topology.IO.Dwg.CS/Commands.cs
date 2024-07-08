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
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using NetTopologySuite.Geometries;
using Acap = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using NetTopologySuite.EdgeGraph;
using NetTopologySuite.GeometriesGraph;

namespace Topology.IO.Dwg.CS
{
    public class Commands
    {
        [CommandMethod("TestDwgReader", CommandFlags.UsePickSet)]
        public void CheckForPickfirstSelection()
        {
            Editor doc = Acap.DocumentManager.MdiActiveDocument.Editor;

            PromptSelectionResult acSSPrompt = doc.SelectImplied();

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
                ObjectId[] idarrayEmpty = new ObjectId[0];
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

            ObjectId[] ids = acSSet.GetObjectIds();
            GeometryCollection geometrys = new DwgReader().ReadGeometryCollection(ids);

            //转换为图
            //var graph = EdgeGraphBuilder.Build(geometrys);
            GeometryGraph g = new GeometryGraph(0, geometrys);
        }
    }
}