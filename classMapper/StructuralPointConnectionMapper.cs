using Autodesk.Revit.DB;
using Betekk.RevitXmiExporter.ClassMapper.Base;
using Betekk.RevitXmiExporter.Utils;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Geometries;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Models.Entities.StructuralAnalytical;

namespace Betekk.RevitXmiExporter.ClassMapper
{
    internal class StructuralPointConnectionMapper : StructuralBaseEntityMapper
    {
        // Cache connections by coordinate tuple to avoid duplicates.
        private static readonly Dictionary<string, XmiStructuralPointConnection> ConnectionCache = new();

        public static XmiStructuralPointConnection Map(IXmiManager manager, int modelIndex, Element element)
        {
            try
            {
                var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

                XmiStorey storey = null;
                if (element.LevelId != null && element.LevelId != ElementId.InvalidElementId)
                {
                    Level levelElement = element.Document.GetElement(element.LevelId) as Level;
                    if (levelElement != null)
                    {
                        storey = StoreyMapper.Map(manager, modelIndex, levelElement);
                    }
                }

                XmiPoint3D point = Point3DMapper.Map(manager, modelIndex, element);
                return Map(manager, modelIndex, id, name, nativeId, storey, point);
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[StructuralPointConnectionMapper.Element] {ex}");
                throw;
            }
        }

        public static XmiStructuralPointConnection Map(IXmiManager manager, int modelIndex, string id, string name, string nativeId, XmiStorey storey, XYZ position)
        {
            try
            {
                double x = Math.Round(Converters.ConvertValueToMillimeter(position.X), 3);
                double y = Math.Round(Converters.ConvertValueToMillimeter(position.Y), 3);
                double z = Math.Round(Converters.ConvertValueToMillimeter(position.Z), 3);
                string key = $"{x}_{y}_{z}";

                if (ConnectionCache.TryGetValue(key, out XmiStructuralPointConnection cached))
                    return cached;

                XmiPoint3D point = manager.CreatePoint3D(
                    modelIndex,
                    $"{id}_point",
                    $"{name}_point",
                    "",
                    $"{nativeId}_point",
                    "",
                    x, y, z
                );

                XmiStructuralPointConnection connection = manager.CreateStructuralPointConnection(
                    modelIndex,
                    id,
                    name,
                    "",
                    nativeId,
                    "",
                    storey,
                    point
                );

                ConnectionCache[key] = connection;
                return connection;
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[StructuralPointConnectionMapper.XYZ] {ex}");
                throw;
            }
        }

        public static XmiStructuralPointConnection Map(IXmiManager manager, int modelIndex, string id, string name, string nativeId, XmiStorey storey, XmiPoint3D point)
        {
            try
            {
                double x = Math.Round(point.X, 3);
                double y = Math.Round(point.Y, 3);
                double z = Math.Round(point.Z, 3);
                string key = $"{x}_{y}_{z}";

                if (ConnectionCache.TryGetValue(key, out XmiStructuralPointConnection cached))
                    return cached;

                XmiStructuralPointConnection connection = manager.CreateStructuralPointConnection(
                    modelIndex,
                    id,
                    name,
                    "",
                    nativeId,
                    "",
                    storey,
                    point
                );

                ConnectionCache[key] = connection;
                return connection;
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[StructuralPointConnectionMapper.Point3D] {ex}");
                throw;
            }
        }
    }
}
