using System;
using Autodesk.Revit.DB;
using Betekk.RevitXmiExporter.ClassMapper.Base;
using Betekk.RevitXmiExporter.Utils;
using XmiSchema.Core.Geometries;
using XmiSchema.Core.Manager;

namespace Betekk.RevitXmiExporter.ClassMapper
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

                XmiPoint3D newPoint = manager.CreatePoint3D(
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
                ModelInfoBuilder.WriteErrorLogToFile($"[Point3DMapper] {ex}");
                throw;
            }
        }
    }
}
