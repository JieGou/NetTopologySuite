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
using NetTopologySuite.Geometries;
using Topology.IO;

namespace Topology.IO.Dwg.CS
{
    public abstract class GeometryWriter : GeometryReaderWriter
    {
        public GeometryWriter() : base()
        {
        }

        public GeometryWriter(GeometryFactory factory) : base(factory)
        {
        }
    }
}