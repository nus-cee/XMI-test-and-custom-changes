using Autodesk.Revit.DB;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Utils;
using XmiSchema.Core.Enums;
using XmiSchema.Core.Geometries;
using System.Linq;
using Utils;
using System.Collections.Generic;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Relationships;

namespace ClassMapper
{
    internal class StructuralSurfaceMemberMapper : BaseMapper
    {
        public static XmiStructuralSurfaceMember Map(IXmiManager manager, int modelIndex, Element element)
        {
            try
            {
                // 1️⃣ 基础属性
                var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

                // 2️⃣ 材料 Material
                XmiStructuralMaterial material = null;
                if (element is FamilyInstance fi && fi.Symbol != null)
                {
                    // 提取材料
                    var matIds = fi.Symbol.GetMaterialIds(false);
                    if (matIds.Count > 0)
                    {
                        var matElement = fi.Symbol.Document.GetElement(matIds.First()) as Material;
                        if (matElement != null)
                        {
                            material = StructuralMaterialMapper.Map(manager, modelIndex, matElement);
                        }
                    }
                }
                else if (element is ElementType typeElement)
                {
                    // 提取材料
                    var matIds = typeElement.GetMaterialIds(false);
                    if (matIds.Count > 0)
                    {
                        var matElement = typeElement.Document.GetElement(matIds.First()) as Material;
                        if (matElement != null)
                        {
                            material = StructuralMaterialMapper.Map(manager, modelIndex, matElement);
                        }
                    }
                }

                // 3️⃣ 楼层 Storey
                XmiStructuralStorey storey = null;
                if (element.LevelId != null && element.LevelId != ElementId.InvalidElementId)
                {
                    var levelElement = element.Document.GetElement(element.LevelId) as Level;
                    if (levelElement != null)
                    {
                        storey = StructuralStoreyMapper.Map(manager, modelIndex, levelElement);
                        // 查找已有 storey
                        if (storey != null)
                        {
                            var existingStorey = manager.GetEntitiesOfType<XmiStructuralStorey>(modelIndex)
                                .FirstOrDefault(s => s.NativeId == storey.NativeId);
                            storey = existingStorey ?? storey;
                        }
                    }
                }

                // 4️⃣ 节点 Nodes
                var nodes = new List<XmiStructuralPointConnection>();
                

                // 5️⃣ 段 Segments
                var segments = new List<XmiSegment>();

                // 6️⃣ 类型、系统面、面积、厚度、高度、坐标系、zOffset
                var surfaceMemberType = DetermineSurfaceType(element);
                var systemPlane = XmiStructuralSurfaceMemberSystemPlaneEnum.Unknown;
                double area = ExtractArea(element);
                double height = CalculateHeight(nodes);
                double thickness = ExtractThickness(element);
                const string localAxisX = "1,0,0";
                const string localAxisY = "0,1,0";
                const string localAxisZ = "0,0,1";
                const double zOffset = 0;

                // 7️⃣ 创建 SurfaceMember（新版接口参数顺序）
                var surfaceMember = manager.CreateStructuralSurfaceMember(
                    modelIndex,
                    id,
                    name,
                    ifcGuid,
                    nativeId,
                    description,
                    material,
                    surfaceMemberType,
                    thickness,
                    systemPlane,
                    nodes,
                    storey,
                    segments,
                    area,
                    zOffset,
                    localAxisX,
                    localAxisY,
                    localAxisZ,
                    height
                );

                return surfaceMember;
            }
            catch (Exception ex)
            {
                Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile($"[StructuralSurfaceMemberMapper] Error: {ex}");
                throw;
            }
        }

        private static XmiStructuralSurfaceMemberTypeEnum DetermineSurfaceType(Element element)
        {
            string surfaceTypeStr = "Unknown";
            if (element.Category?.Name.ToLower().Contains("wall") == true)
                surfaceTypeStr = "Wall";
            else if (element.Category?.Name.ToLower().Contains("floor") == true)
                surfaceTypeStr = "Slab";
            return ExtensionEnumHelper.FromEnumValue<XmiStructuralSurfaceMemberTypeEnum>(surfaceTypeStr)
                ?? XmiStructuralSurfaceMemberTypeEnum.Unknown;
        }

        private static double ExtractThickness(Element element)
        {
            var thicknessParam = element.LookupParameter("Thickness");
            if (thicknessParam?.StorageType == StorageType.Double)
                return Converters.ConvertValueToMillimeter(thicknessParam.AsDouble());
            return 0;
        }

        private static double ExtractArea(Element element)
        {
            if (element.LookupParameter("Area") != null)
                return Converters.ConvertValueToMillimeter(element.LookupParameter("Area").AsDouble());
            return 0;
        }

        private static double CalculateHeight(List<XmiStructuralPointConnection> nodes)
        {
            if (nodes.Count >= 2)
            {
                var zs = nodes.Select(n => n.Point.Z).ToList();
                return zs.Max() - zs.Min();
            }
            return 0;
        }
    }

    public class XYZEqualityComparer : IEqualityComparer<XYZ>
    {
        private const double Tolerance = 1e-6;
        public bool Equals(XYZ a, XYZ b)
        {
            return a != null && b != null && a.IsAlmostEqualTo(b, Tolerance);
        }
        public int GetHashCode(XYZ obj)
        {
            return obj == null ? 0 :
                obj.X.GetHashCode() ^ obj.Y.GetHashCode() ^ obj.Z.GetHashCode();
        }
    }
}
