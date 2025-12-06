using System.Collections.Concurrent;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Betekk.RevitXmiExporter.ClassMapper.Base;
using Betekk.RevitXmiExporter.Utils;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Enums;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Models.Entities.StructuralAnalytical;
using XmiSchema.Core.Utils;

namespace Betekk.RevitXmiExporter.ClassMapper
{
    internal class StructuralCurveMemberMapper : StructuralBaseEntityMapper
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

                var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(member);

                Curve curve = member.GetCurve();
                if (curve == null)
                {
                    ModelInfoBuilder.WriteErrorLogToFile($"[StructuralCurveMemberMapper] Curve not found. Member Id={id}, Name={name}");
                    ErrorStatistics.Increment("Curve_Null");
                    return null;
                }

                XYZ start = curve.GetEndPoint(0);
                XYZ end = curve.GetEndPoint(1);
                if (start == null || end == null)
                {
                    ModelInfoBuilder.WriteErrorLogToFile($"[StructuralCurveMemberMapper] Missing endpoints. NativeId={nativeId}, Member Id={id}, Name={name}");
                    ErrorStatistics.Increment("EndPoint_Null");
                    return null;
                }

                XmiCrossSection crossSection = null;
                ElementType sectionType = null;
                if (member.SectionTypeId != ElementId.InvalidElementId)
                {
                    sectionType = member.Document.GetElement(member.SectionTypeId) as ElementType;
                    if (sectionType != null)
                    {
                        crossSection = CrossSectionMapper.Map(manager, modelIndex, sectionType);

                        ICollection<ElementId> matIds = sectionType.GetMaterialIds(false);
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
                        ModelInfoBuilder.WriteErrorLogToFile($"[StructuralCurveMemberMapper] SectionTypeId={member.SectionTypeId} could not be resolved. Member id={id}, name={name}");
                        ErrorStatistics.Increment("SectionType_NotFound");
                    }
                }
                else
                {
                    ModelInfoBuilder.WriteErrorLogToFile($"[StructuralCurveMemberMapper] Invalid SectionTypeId for member id={id}, name={name}");
                    ErrorStatistics.Increment("SectionType_Invalid");
                }

                XmiStorey storey = null;
                if (member.LevelId != ElementId.InvalidElementId)
                {
                    Level level = member.Document.GetElement(member.LevelId) as Level;
                    if (level != null)
                    {
                        storey = StoreyMapper.Map(manager, modelIndex, level);
                    }
                }

                XmiStructuralPointConnection beginNode = StructuralPointConnectionMapper.Map(manager, modelIndex, $"{id}_start", $"{name}_start", $"{nativeId}_start", storey, start);
                XmiStructuralPointConnection endNode = StructuralPointConnectionMapper.Map(manager, modelIndex, $"{id}_end", $"{name}_end", $"{nativeId}_end", storey, end);

                if (beginNode == null || endNode == null)
                {
                    ErrorStatistics.Increment("Node_Null");
                    return null;
                }

                List<XmiStructuralPointConnection> nodes = new List<XmiStructuralPointConnection> { beginNode, endNode };
                List<XmiSegment> segments = SegmentMapper.MapCurveSegments(id, name, nativeId, curve);
                if (segments.Count == 0)
                {
                    ModelInfoBuilder.WriteErrorLogToFile($"[StructuralCurveMemberMapper] Missing segment geometry for member id={id}, name={name}");
                    ErrorStatistics.Increment("Segment_Missing");
                }

                string roleName = member.StructuralRole.ToString();
                XmiStructuralCurveMemberTypeEnum memberType =
                    ExtensionEnumHelper.FromEnumValue<XmiStructuralCurveMemberTypeEnum>(roleName)
                    ?? XmiStructuralCurveMemberTypeEnum.Unknown;

                Transform transform = member.GetTransform();
                string localAxisX = transform != null ? $"{transform.BasisX.X},{transform.BasisX.Y},{transform.BasisX.Z}" : "0,0,0";
                string localAxisY = transform != null ? $"{transform.BasisY.X},{transform.BasisY.Y},{transform.BasisY.Z}" : "0,0,0";
                string localAxisZ = transform != null ? $"{transform.BasisZ.X},{transform.BasisZ.Y},{transform.BasisZ.Z}" : "0,0,0";

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
                    XmiSystemLineEnum.TopMiddle,
                    beginNode,
                    endNode,
                    curve.Length,
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

        internal static class ErrorStatistics
        {
            private static readonly ConcurrentDictionary<string, int> ErrorCounts = new();

            public static void Increment(string key)
            {
                ErrorCounts.AddOrUpdate(key, 1, (_, old) => old + 1);
            }

            public static void LogAllToFile(string filePath)
            {
                try
                {
                    using StreamWriter writer = new StreamWriter(filePath, false);
                    foreach (KeyValuePair<string, int> kv in ErrorCounts)
                    {
                        writer.WriteLine($"{kv.Key}: {kv.Value}");
                    }
                }
                catch
                {
                }
            }
        }
    }
}
