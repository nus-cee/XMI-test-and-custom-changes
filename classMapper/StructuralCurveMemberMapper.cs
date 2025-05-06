using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using XmiCore;
using System.Linq;
using Lists;
using Utils;

namespace ClassMapper
{
    internal class StructuralCurveMemberMapper : BaseMapper
    {
        public static XmiStructuralCurveMember Map(AnalyticalMember member)
        {
            var id = member.Id.IntegerValue.ToString();
            var name = member.Name;
            var nativeId = id;
            var ifcGuid = ""; // 可扩展
            var description = ""; // 可扩展

            // ✅ CrossSection（暂留空）
            XmiStructuralCrossSection crossSection = null;

            // ✅ Storey（直接通过 LevelId）
            var storey = GetOrCreateStorey(member);

            // ✅ Curve + 节点
            var curve = member.GetCurve();
            if (curve == null) return null;

            var startPoint = curve.GetEndPoint(0);
            var endPoint = curve.GetEndPoint(1);

            var nodes = new List<XmiStructuralPointConnection>();
            var beginNode = FindOrCreateNode(startPoint, member);
            var endNode = FindOrCreateNode(endPoint, member);
            nodes.Add(beginNode);
            nodes.Add(endNode);

            // ✅ Segments 为空
            var segments = new List<XmiBaseEntity>();

            // ✅ SystemLine 默认值
            var systemLine = XmiStructuralCurveMemberSystemLineEnum.Unknown;

            // ✅ CurveMemberType 推断（来自结构角色）
            var roleName = member.StructuralRole.ToString();
            var curvememberType = ExtensionEnumHelper.FromEnumValue<XmiStructuralCurveMemberTypeEnum>(roleName)
                ?? XmiStructuralCurveMemberTypeEnum.Unknown;

            // ✅ Length
            double length = Converters.ConvertValueToMillimeter(curve.Length);

            // ✅ Local Axis
            string localAxisX = "0,0,0";
            string localAxisY = "0,0,0";
            string localAxisZ = "0,0,0";

            var localAxis = member.GetLocalCoordinateSystem();
            if (localAxis != null)
            {
                var x = localAxis.BasisX;
                var y = localAxis.BasisY;
                var z = localAxis.BasisZ;

                localAxisX = $"{x.X:F10},{x.Y:F10},{x.Z:F10}";
                localAxisY = $"{y.X:F10},{y.Y:F10},{y.Z:F10}";
                localAxisZ = $"{z.X:F10},{z.Y:F10},{z.Z:F10}";
            }

            // ✅ Offset 默认值
            double beginNodeXOffset = 0;
            double endNodeXOffset = 0;
            double beginNodeYOffset = 0;
            double endNodeYOffset = 0;
            double beginNodeZOffset = 0;
            double endNodeZOffset = 0;

            // ✅ Fixity 默认值
            string endFixityStart = "FFFFFF";
            string endFixityEnd = "FFFFFF";

            return new XmiStructuralCurveMember(
                id,
                name,
                ifcGuid,
                nativeId,
                description,
                crossSection,
                storey,
                curvememberType,
                nodes,
                segments,
                systemLine,
                beginNode,
                endNode,
                length,
                localAxisX,
                localAxisY,
                localAxisZ,
                beginNodeXOffset,
                endNodeXOffset,
                beginNodeYOffset,
                endNodeYOffset,
                beginNodeZOffset,
                endNodeZOffset,
                endFixityStart,
                endFixityEnd
            );
        }

        private static XmiStructuralPointConnection FindOrCreateNode(XYZ point, AnalyticalMember member)
        {
            var existing = StructuralDataContext.StructuralPointConnectionList
                .FirstOrDefault(p => IsSamePoint(p.Point, point));
            if (existing != null) return existing;

            var newNode = StructuralPointConnectionMapper.MapFromXYZ(point, member);
            StructuralDataContext.StructuralPointConnectionList.Add(newNode);
            return newNode;
        }

        private static bool IsSamePoint(XmiPoint3D p, XYZ xyz, double tolerance = 1e-3)
        {
            double px = Converters.ConvertValueToMillimeter(xyz.X);
            double py = Converters.ConvertValueToMillimeter(xyz.Y);
            double pz = Converters.ConvertValueToMillimeter(xyz.Z);

            return
                System.Math.Abs(p.X - px) < tolerance &&
                System.Math.Abs(p.Y - py) < tolerance &&
                System.Math.Abs(p.Z - pz) < tolerance;
        }

        private static XmiStructuralStorey GetOrCreateStorey(AnalyticalMember member)
        {
            var levelId = member.LevelId;
            if (levelId == ElementId.InvalidElementId) return null;

            string levelNativeId = levelId.Value.ToString();
            var storey = StructuralDataContext.StructuralStoreyList
                .FirstOrDefault(s => s.NativeId == levelNativeId);

            if (storey == null)
            {
                var levelElement = member.Document.GetElement(levelId) as Level;
                if (levelElement != null)
                {
                    storey = StructuralStoreyMapper.Map(levelElement);
                    StructuralDataContext.StructuralStoreyList.Add(storey);
                }
            }

            return storey;
        }
    }
}
