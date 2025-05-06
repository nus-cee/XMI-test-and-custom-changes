using System;
using Autodesk.Revit.DB;
using XmiCore;
using Utils;

namespace ClassMapper
{
    internal class Point3DMapper : BaseMapper
    {
        public static XmiPoint3D Map(Element element)
        {
            // ✅ 使用 BaseMapper 中缓存的 sessionUuid（即作为 point ID）
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
