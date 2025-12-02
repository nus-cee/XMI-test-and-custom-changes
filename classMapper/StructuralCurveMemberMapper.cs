using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Enums;
using XmiSchema.Core.Geometries;
using XmiSchema.Core.Manager;
using System.Collections.Generic;
using Utils;
using XmiSchema.Core.Utils;
using Revit_to_XMI.utils;
using System.Collections.Concurrent;

namespace ClassMapper
{
    internal class StructuralCurveMemberMapper : BaseMapper
    {
        public static XmiStructuralCurveMember Map(IXmiManager manager, int modelIndex, AnalyticalMember member)
        {
            try
            {
                if (member == null)
                {
                    ErrorStatistics.Increment("Member_Null");
                    return null;
                }

                // 1️⃣ 提取基础属性
                var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(member);

                // 4️⃣ 获取几何曲线与起止点
                var curve = member.GetCurve();
                if (curve == null)
                {
                    ModelInfoBuilder.WriteErrorLogToFile($"[StructuralCurveMemberMapper] Warning: GetCurve returned null for member id={id}, name={name}");
                    ErrorStatistics.Increment("Curve_Null");
                    return null;
                }

                var start = curve.GetEndPoint(0);
                var end = curve.GetEndPoint(1);



                if (start == null || end == null)
                {
                    ModelInfoBuilder.WriteErrorLogToFile($"[StructuralCurveMemberMapper] Warning: GetEndPoint null: start={start}, end={end}, nativeId={nativeId},member id={id}, name={name}");
                    ErrorStatistics.Increment("EndPoint_Null");
                    return null;
                }

                // 2️⃣ 截面 CrossSection
                XmiStructuralCrossSection crossSection = null;
                ElementType sectionType = null;
                if (member.SectionTypeId != ElementId.InvalidElementId)
                {
                    sectionType = member.Document.GetElement(member.SectionTypeId) as ElementType;
                    if (sectionType != null)
                    {
                        crossSection = StructuralCrossSectionMapper.Map(manager, modelIndex, sectionType);

                        var matIds = sectionType.GetMaterialIds(false);
                        if (matIds.Count == 0)
                        {
                            ModelInfoBuilder.WriteErrorLogToFile($"[StructuralCurveMemberMapper] SectionType has no material: member id={id}, name={name}, sectionType={sectionType.Name}");
                            ErrorStatistics.Increment("Material_Missing");
                        }
                        else
                        {
                            var matElement = sectionType.Document.GetElement(matIds.First()) as Material;
                            if (matElement == null)
                            {
                                ModelInfoBuilder.WriteErrorLogToFile($"[StructuralCurveMemberMapper] SectionType material id={matIds.First()} not found: member id={id}, name={name}, sectionType={sectionType.Name}");
                                ErrorStatistics.Increment("Material_NotFound");
                            }
                        }
                    }
                    else
                    {
                        ModelInfoBuilder.WriteErrorLogToFile($"[StructuralCurveMemberMapper] SectionTypeId={member.SectionTypeId} 找不到对应的 ElementType，crossSection 为空，将传入null。member id={id}, name={name}");
                        ErrorStatistics.Increment("SectionType_NotFound");
                    }
                }
                else
                {
                    ModelInfoBuilder.WriteErrorLogToFile($"[StructuralCurveMemberMapper] member.SectionTypeId is InvalidElementId，crossSection 为空，将传入null。member id={id}, name={name}");
                    ErrorStatistics.Increment("SectionType_Invalid");
                }

                // 3️⃣ 楼层 Storey
                XmiStructuralStorey storey = null;
                if (member.LevelId != ElementId.InvalidElementId)
                {
                    var level = member.Document.GetElement(member.LevelId) as Level;
                    if (level != null)
                    {
                        storey = StructuralStoreyMapper.Map(manager, modelIndex, level);
                    }
                }

                // 5️⃣ 节点
                var beginNode = StructuralPointConnectionMapper.Map(manager, modelIndex, $"{id}_start", $"{name}_start", $"{nativeId}_start", storey, start);
                var endNode = StructuralPointConnectionMapper.Map(manager, modelIndex, $"{id}_end", $"{name}_end", $"{nativeId}_end", storey, end);

                if (beginNode == null || endNode == null)
                {
                    ErrorStatistics.Increment("Node_Null");
                    return null;
                }

                // 6️⃣ 节点列表与空段
                var nodes = new List<XmiStructuralPointConnection> { beginNode, endNode };
                var segments = new List<XmiSegment>();

                // 7️⃣ 构件类型
                var roleName = member.StructuralRole.ToString();
                var memberType = ExtensionEnumHelper.FromEnumValue<XmiStructuralCurveMemberTypeEnum>(roleName) ?? XmiStructuralCurveMemberTypeEnum.Unknown;

                // 8️⃣ 坐标系
                var transform = member.GetTransform();
                string localAxisX = transform != null ? $"{transform.BasisX.X},{transform.BasisX.Y},{transform.BasisX.Z}" : "0,0,0";
                string localAxisY = transform != null ? $"{transform.BasisY.X},{transform.BasisY.Y},{transform.BasisY.Z}" : "0,0,0";
                string localAxisZ = transform != null ? $"{transform.BasisZ.X},{transform.BasisZ.Y},{transform.BasisZ.Z}" : "0,0,0";

                // 9️⃣ 点坐标
                var startPt = beginNode;
                var endPt = endNode;


                double length = 0;

                return manager.CreateStructuralCurveMember(
                    modelIndex,
                    id,
                    name,
                    ifcGuid,
                    nativeId,
                    description,
                    crossSection,
                    storey,
                    memberType,
                    nodes,
                    segments,
                    XmiStructuralCurveMemberSystemLineEnum.Unknown,
                    beginNode,
                    endNode,
                    length,
                    localAxisX,
                    localAxisY,
                    localAxisZ,
                    0, 0, 0, 0, 0, 0,
                    "FFFFFF",
                    "FFFFFF"
                );
            }
            catch (System.Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[StructuralCurveMemberMapper] Error: {ex}");
                ErrorStatistics.Increment("Exception");
                throw;
            }
        }
        // 🧠 错误统计工具类
        internal static class ErrorStatistics
        {
            private static readonly ConcurrentDictionary<string, int> _errorCounts = new();

            public static void Increment(string key)
            {
                _errorCounts.AddOrUpdate(key, 1, (_, old) => old + 1);
            }

            public static void LogAllToFile(string filePath)
            {
                try
                {
                    using var writer = new StreamWriter(filePath, false); // 覆盖写入
                    foreach (var kv in _errorCounts)
                    {
                        writer.WriteLine($"{kv.Key}: {kv.Value}");
                    }
                }
                catch { /* 日志写失败忽略 */ }
            }
        }
    }
}
