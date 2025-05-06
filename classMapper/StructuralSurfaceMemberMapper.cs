using Autodesk.Revit.DB;
using XmiCore;
using System.Linq;
using Lists;
using Utils;
using System.Collections.Generic;

namespace ClassMapper
{
    internal class StructuralSurfaceMemberMapper : BaseMapper
    {
        public static XmiStructuralSurfaceMember Map(Element element)
        {
            var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

            // ✅ 材料提取（类似 Beam 逻辑）
            XmiStructuralMaterial material = null;
            if (element is FamilyInstance fi)
            {
                var matIds = fi.GetMaterialIds(false);
                if (matIds.Count > 0)
                {
                    var matElement = element.Document.GetElement(matIds.First()) as Material;
                    if (matElement != null)
                    {
                        material = StructuralMaterialMapper.Map(matElement);
                    }
                }
            }

            // ✅ 类型 SurfaceMemberTypeEnum 判断（从类别判断）
            string surfaceTypeStr = "Unknown";
            if (element.Category?.Name.ToLower().Contains("wall") == true)
                surfaceTypeStr = "Wall";
            else if (element.Category?.Name.ToLower().Contains("floor") == true)
                surfaceTypeStr = "Slab";

            var surfaceType = ExtensionEnumHelper.FromEnumValue<XmiStructuralSurfaceMemberTypeEnum>(surfaceTypeStr)
                              ?? XmiStructuralSurfaceMemberTypeEnum.Unknown;

            // ✅ 厚度（常见为“厚度”、“Thickness”参数）
            double thickness = 0;
            var thicknessParam = element.LookupParameter("Thickness");

            if (thicknessParam != null && thicknessParam.StorageType == StorageType.Double)
            {
                thickness = Converters.ConvertValueToMillimeter(thicknessParam.AsDouble());
            }

            // ✅ System Plane 默认 Axis（你也可以扩展用参数判断是 Floor/WALL）
            var systemPlane = XmiStructuralSurfaceMemberSystemPlaneEnum.Unknown;

            // ✅ 节点列表（提取为角点）
            // ✅ 节点列表（提取为四个角点）
            var nodes = new List<XmiStructuralPointConnection>();

            // 假设你只处理 Wall/Floor 类型（面状），直接从 Geometry 提四个顶点
            if (element.get_Geometry(new Options { ComputeReferences = true }) is GeometryElement geometry)
            {
                foreach (var geoObj in geometry)
                {
                    if (geoObj is Solid solid && solid.Faces.Size > 0)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            Mesh mesh = face.Triangulate();

                            // 四点容器，避免重复
                            HashSet<XYZ> uniquePoints = new HashSet<XYZ>(new XYZEqualityComparer());

                            for (int i = 0; i < mesh.NumTriangles; i++)
                            {
                                MeshTriangle tri = mesh.get_Triangle(i);
                                uniquePoints.Add(tri.get_Vertex(0));
                                uniquePoints.Add(tri.get_Vertex(1));
                                uniquePoints.Add(tri.get_Vertex(2));
                            }

                            foreach (var pt in uniquePoints)
                            {
                                var existing = StructuralDataContext.StructuralPointConnectionList
                                    .FirstOrDefault(p => IsSamePoint(p.Point, pt));
                                if (existing != null)
                                {
                                    nodes.Add(existing);
                                }
                                else
                                {
                                    var mapped = StructuralPointConnectionMapper.MapFromXYZ(pt, element);
                                    StructuralDataContext.StructuralPointConnectionList.Add(mapped);
                                    nodes.Add(mapped);
                                }
                            }

                            break; // 只处理第一个Face
                        }

                        break; // 只处理第一个Solid
                    }
                }
            }


            // ✅ Storey 楼层关联
            XmiStructuralStorey storey = null;
            if (element.LevelId != null && element.LevelId != ElementId.InvalidElementId)
            {
                string levelNativeId = element.LevelId.IntegerValue.ToString();
                storey = StructuralDataContext.StructuralStoreyList
                    .FirstOrDefault(s => s.NativeId == levelNativeId);

                if (storey == null)
                {
                    var levelElement = element.Document.GetElement(element.LevelId) as Level;
                    if (levelElement != null)
                    {
                        storey = StructuralStoreyMapper.Map(levelElement);
                        StructuralDataContext.StructuralStoreyList.Add(storey);
                    }
                }
            }

            // ✅ Segment 暂不支持，留空
            var segments = new List<XmiSegment>();

            // ✅ 面积（Area）
            double area = 0;
            if (element.LookupParameter("Area") != null)
            {
                area = Converters.ConvertValueToMillimeter(element.LookupParameter("Area").AsDouble());
            }

            // ✅ 高度（适用于 Wall），计算最大Z差
            double height = 0;
            if (nodes.Count >= 2)
            {
                var zs = nodes.Select(n => n.Point.Z).ToList();
                height = zs.Max() - zs.Min();
            }

            // ✅ ZOffset 默认 0
            double zOffset = 0;

            // ✅ Local Axis 默认单位向量
            string localAxisX = "1,0,0";
            string localAxisY = "0,1,0";
            string localAxisZ = "0,0,1";

            return new XmiStructuralSurfaceMember(
                id,
                name,
                ifcGuid,
                nativeId,
                description,
                material,
                surfaceType,
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
        }

        // ✅ 判断点是否接近
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
    }
    public class XYZEqualityComparer : IEqualityComparer<XYZ>
    {
        private const double Tolerance = 1e-6;

        public bool Equals(XYZ a, XYZ b)
        {
            return a != null && b != null &&
                   a.IsAlmostEqualTo(b, Tolerance);
        }

        public int GetHashCode(XYZ obj)
        {
            unchecked
            {
                return obj == null ? 0 :
                    obj.X.GetHashCode() ^ obj.Y.GetHashCode() ^ obj.Z.GetHashCode();
            }
        }
    }

}
