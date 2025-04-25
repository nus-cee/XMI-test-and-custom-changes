using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using XmiCore;
using Utils;


namespace ClassMapper
{
    internal class Point3DMapper : BaseMapper
    {
        public static XmiPoint3D Map(Element element)
        {
            var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

            double x = 0, y = 0, z = 0;

            if (element is ReferencePoint referencePoint)
            {
                XYZ pos = referencePoint.Position;
                x = Converters.ConvertValueToMillimeter(pos.X);
                y = Converters.ConvertValueToMillimeter(pos.Y);
                z = Converters.ConvertValueToMillimeter(pos.Z);
            }

            return new XmiPoint3D(
                id,
                name,
                ifcGuid,
                nativeId,
                description,
                x,
                y,
                z
            );
        }
    }
}
