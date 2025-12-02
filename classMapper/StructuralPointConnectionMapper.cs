using Autodesk.Revit.DB;
using Utils;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Geometries;
using XmiSchema.Core.Manager;
using System;
using System.Collections.Generic;

namespace ClassMapper
{
    internal class StructuralPointConnectionMapper : BaseMapper
    {
        // 坐标去重缓存：key = "x_y_z"
        private static readonly Dictionary<string, XmiStructuralPointConnection> _connectionCache = new();

        // ✅ 从 Element 创建连接点（用于 AnalyticalNode、ReferencePoint 等）
        public static XmiStructuralPointConnection Map(IXmiManager manager, int modelIndex, Element element)
        {
            try
            {
                var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

                // 1️⃣ 创建楼层（可选）
                XmiStructuralStorey storey = null;
                if (element.LevelId != null && element.LevelId != ElementId.InvalidElementId)
                {
                    var levelElement = element.Document.GetElement(element.LevelId) as Level;
                    if (levelElement != null)
                    {
                        storey = manager.CreateStructuralStorey(
                            modelIndex,
                            levelElement.Id.IntegerValue.ToString(),
                            levelElement.Name,
                            "",
                            levelElement.Id.IntegerValue.ToString(),
                            "",
                            Converters.ConvertValueToMillimeter(levelElement.Elevation),
                            1f,
                            null,
                            null,
                            null
                        );
                    }
                }

                // 2️⃣ 创建点
                var point = Point3DMapper.Map(manager, modelIndex, element);
                return Map(manager, modelIndex, id, name, nativeId, storey, point);
            }
            catch (Exception ex)
            {
                Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile($"[StructuralPointConnectionMapper - From Element] Error: {ex}");
                throw;
            }
        }

        // ✅ 从 XYZ 创建连接点（用于 AnalyticalMember 起止点）
        public static XmiStructuralPointConnection Map(IXmiManager manager, int modelIndex, string id, string name, string nativeId, XmiStructuralStorey storey, XYZ position)
        {
            try
            {
                double x = Math.Round(Converters.ConvertValueToMillimeter(position.X), 3);
                double y = Math.Round(Converters.ConvertValueToMillimeter(position.Y), 3);
                double z = Math.Round(Converters.ConvertValueToMillimeter(position.Z), 3);
                string key = $"{x}_{y}_{z}";

                if (_connectionCache.TryGetValue(key, out var cached))
                    return cached;

                var point = manager.CreatePoint3D(
                    modelIndex,
                    $"{id}_point",
                    $"{name}_point",
                    "",
                    $"{nativeId}_point",
                    "",
                    x, y, z
                );

                var connection = manager.CreateStructuralPointConnection(
                    modelIndex,
                    id,
                    name,
                    "",
                    nativeId,
                    "",
                    storey,
                    point
                );

                _connectionCache[key] = connection;
                return connection;
            }
            catch (Exception ex)
            {
                Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile($"[StructuralPointConnectionMapper - XYZ] Error: {ex}");
                throw;
            }
        }

        // ✅ 从已有 Point3D 创建连接点（统一走缓存）
        public static XmiStructuralPointConnection Map(IXmiManager manager, int modelIndex, string id, string name, string nativeId, XmiStructuralStorey storey, XmiPoint3D point)
        {
            try
            {
                double x = Math.Round(point.X, 3);
                double y = Math.Round(point.Y, 3);
                double z = Math.Round(point.Z, 3);
                string key = $"{x}_{y}_{z}";

                if (_connectionCache.TryGetValue(key, out var cached))
                    return cached;

                var connection = manager.CreateStructuralPointConnection(
                    modelIndex,
                    id,
                    name,
                    "",
                    nativeId,
                    "",
                    storey,
                    point
                );

                _connectionCache[key] = connection;
                return connection;
            }
            catch (Exception ex)
            {
                Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile($"[StructuralPointConnectionMapper - from Point3D] Error: {ex}");
                throw;
            }
        }
    }
}
