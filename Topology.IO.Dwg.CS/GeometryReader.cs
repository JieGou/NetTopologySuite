using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using Microsoft.VisualBasic;
using NetTopologySuite.Geometries;

namespace Topology.IO.Dwg.CS
{
    /// <summary>
    /// Defines algorithm used for curve-based geometries tesselation.
    /// <para>
    /// Curves need to be tessellated (broken up into lines) in order to be converted to
    /// JTS feature representation. The degree of tessellation determines how accurate the
    /// converted curve will be (how close it will approximate the original curve geometry)
    /// and how much performance overhead is required to generate the representation of a curve.
    /// </para>
    /// </summary>
    /// <remarks></remarks>
    public enum CurveTessellation
    {
        /// <summary>
        /// No tessellation, meaning that curves will get converted either into straight segments or
        /// generic curves, depending on reader/writer curve-based geometry implementation.
        /// <see cref="GeometryReader.CurveTessellationValue"/> parameter is ignored.
        /// </summary>
        /// <remarks></remarks>
        None,

        /// <summary>
        /// Curves are tessellated by means of linear points sampling. Linear sampling
        /// divides a curve into equal-sized segments. Number of segments depends on the
        /// value stored in <see cref="GeometryReader.CurveTessellationValue"/>. If
        /// <see cref="GeometryReader.CurveTessellationValue"/> is negative or zero,
        /// default value used for tessellation is <c>16</c>.
        /// </summary>
        /// <remarks></remarks>
        Linear,

        /// <summary>
        /// Curves are tessellated by means of linear points sampling, where size of each
        /// equal-sized segment depends on size (scale) of overall curve geometry. Smaller
        /// the geometry, resulting number of segments is lower. Similar algorithm is
        /// used for on-screen curve tesselation.
        /// Overall scale factor can be set via <see cref="GeometryReader.CurveTessellationValue"/>
        /// property value. If <see cref="GeometryReader.CurveTessellationValue"/> is
        /// negative or zero, default value used for tessellation is <c>1</c>.
        /// </summary>
        /// <remarks></remarks>
        Scaled
    }

    public abstract class GeometryReader : GeometryReaderWriter
    {
        private CurveTessellation m_CurveTessellationMethod = CurveTessellation.Linear;
        private double m_CurveTessellationValue = 15;

        public GeometryReader() : base()
        {
        }

        public GeometryReader(GeometryFactory factory) : base(factory)
        {
        }

        /// <summary>
        /// Method used for curve-based geometries tesselation. For more information
        /// see <see cref="CurveTessellation"/> enumerator description.
        /// Default value is <see cref="CurveTessellation.Linear"/>.
        /// </summary>
        /// <value>Method used for curve-based geometries tesselation.</value>
        /// <remarks></remarks>
        public CurveTessellation CurveTessellationMethod
        {
            get
            {
                return m_CurveTessellationMethod;
            }
            set
            {
                m_CurveTessellationMethod = value;
            }
        }

        /// <summary>
        /// Gets or sets a parameter for curve tessellation method set by <see cref="CurveTessellationMethod"/>.
        /// For exact parameter definition see <see cref="CurveTessellation"/> enumerator description.
        /// </summary>
        /// <value>Curve tessellation method parameter value.</value>
        public double CurveTessellationValue
        {
            get
            {
                switch (CurveTessellationMethod)
                {
                    case CurveTessellation.Linear:
                        if (m_CurveTessellationValue > 0) return m_CurveTessellationValue;
                        else return 16;

                    case CurveTessellation.Scaled:
                        if (m_CurveTessellationValue > 0) return m_CurveTessellationValue;
                        else return 1;

                    default:
                        return 0;
                }
            }
            set
            {
                m_CurveTessellationValue = value;
            }
        }
    }
}