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
    public abstract class GeometryReaderWriter
    {
        private GeometryFactory m_GeometryFactory;
        private bool m_AllowRepeatedCoordinates;

        public GeometryReaderWriter()
        {
        }

        public GeometryReaderWriter(GeometryFactory factory) : base()
        {
            m_GeometryFactory = factory;
        }

        /// <summary>
        /// Returns current <see cref="GeometryFactory"/> used to build geometries.
        /// </summary>
        /// <value></value>
        /// <returns>Current <see cref="GeometryFactory"/> instance.</returns>
        /// <remarks>
        /// If there's no <see cref="GeometryFactory"/> set within class constructor,
        /// a <c>Default</c> factory will be automatically instantiated. Otherwise,
        /// user-supplied <see cref="GeometryFactory"/> will be used during geometry
        /// building process.
        /// </remarks>
        public GeometryFactory GeometryFactory
        {
            get
            {
                if (m_GeometryFactory == null)
                    m_GeometryFactory = GeometryFactory.Default;
                return m_GeometryFactory;
            }
        }

        /// <summary>
        /// Returns current <see cref="PrecisionModel"/> of the coordinates within any
        /// processed <see cref="Geometry"/>.
        /// </summary>
        /// <value></value>
        /// <returns>Current <see cref="GeometryFactory.PrecisionModel"/> instance.</returns>
        /// <remarks>
        /// If there's no <see cref="GeometryFactory.PrecisionModel"/> set within class constructor,
        /// returns default <see cref="GeometryFactory.PrecisionModel"/>. Default precision model is
        /// <c>Floating</c>, meaning full double precision floating point.
        /// </remarks>
        public PrecisionModel PrecisionModel
        {
            get
            {
                return GeometryFactory.PrecisionModel;
            }
        }

        /// <summary>
        /// Gets or sets whether processed geometries include equal sequential
        /// (repeated) coordinates. Default value is <c>False</c>, meaning that
        /// any repeated coordinate within given coordinate sequence will get collapsed.
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public bool AllowRepeatedCoordinates
        {
            get
            {
                return m_AllowRepeatedCoordinates;
            }
            set
            {
                m_AllowRepeatedCoordinates = value;
            }
        }
    }
}