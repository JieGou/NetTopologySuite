using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace Topology.IO.Dwg.CS
{
    public class CoordinateComparer_XY : IComparer<Coordinate>
    {
        public int Compare(Coordinate x, Coordinate y)
        {
            // 返回值：0——相等，1——大于，-1——小于
            if (x != null && y != null)
            {
                if (x.X.CompareTo(y.X) != 0)
                {
                    return x.X.CompareTo(y.X);
                }
                else if (x.Y.CompareTo(y.Y) != 0)
                {
                    return x.Y.CompareTo(y.Y);
                }
                else if (x.Z.CompareTo(y.Z) != 0)
                {
                    return x.Z.CompareTo(y.Z);
                }
                else
                {
                    return 0;
                }
            }
            return 0;
        }
    }
}
