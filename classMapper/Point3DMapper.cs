using System;
using Autodesk.Revit.DB;
using XmiSchema.Core.Geometries;
using Utils;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Models;

namespace ClassMapper
{
    internal class Point3DMapper : BaseMapper
    {

        
        public static XmiPoint3D Map(IXmiManager manager, int modelIndex, Element element)
        {
            try
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

                // ✅ 使用 manager 的 CreatePoint3D 方法创建点
                var newPoint = manager.CreatePoint3D(
                    modelIndex,
                    id,
                    name,
                    ifcGuid,
                    nativeId,
                    description,
                    x,
                    y,
                    z
                );
                return newPoint;
            }
            catch (Exception ex)
            {
                Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile($"[Point3DMapper] Error: {ex}");
                throw;
            }
        }
    }
}