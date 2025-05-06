using Autodesk.Revit.DB;
using XmiCore;
using System.Linq;
using Lists;
using Utils;
using Test;

namespace ClassMapper
{
    internal class StructuralPointConnectionMapper : BaseMapper
    {
        // ✅ 标准Map：从Element直接生成PointConnection
        public static XmiStructuralPointConnection Map(Element element)
        {
            var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

            // 处理Level

            XmiStructuralStorey storey = TestStorey.Dummy;
            //XmiStructuralStorey storey = GetOrCreateStorey(element);

            // 获取LocationPoint
            XYZ pointPosition = null;
            if (element.Location is LocationPoint locationPoint)
            {
                pointPosition = locationPoint.Point;
            }

            XmiPoint3D point = null;

            if (pointPosition != null)
            {
                // 查找现有的近似点
                var existingPoint = StructuralDataContext.Point3DList
                    .FirstOrDefault(p => IsSamePoint(p, pointPosition));

                if (existingPoint != null)
                {
                    point = existingPoint;
                }
                else
                {
                    point = Point3DMapper.Map(element);
                    StructuralDataContext.Point3DList.Add(point);
                }
            }
            else
            {
                point = Point3DMapper.Map(element);
                StructuralDataContext.Point3DList.Add(point);
            }
            //if (storey == null)
            //{
            //    Autodesk.Revit.UI.TaskDialog.Show("DEBUG", "Storey is NULL for element: " + element.Id);
            //}

            return new XmiStructuralPointConnection(
                id,
                name,
                ifcGuid,
                nativeId,
                description,
                storey,
                point
            );
        }

        // ✅ 新增MapFromXYZ：通过指定XYZ点直接生成PointConnection
        public static XmiStructuralPointConnection MapFromXYZ(XYZ pointPosition, Element element)
        {
            var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

            // 处理Level
            XmiStructuralStorey storey = GetOrCreateStorey(element);

            XmiPoint3D point = null;

            if (pointPosition != null)
            {
                var existingPoint = StructuralDataContext.Point3DList
                    .FirstOrDefault(p => IsSamePoint(p, pointPosition));

                if (existingPoint != null)
                {
                    point = existingPoint;
                }
                else
                {
                    // 这里不能用Point3DMapper.Map(element)，需要自己根据XYZ创建新的Point
                    point = new XmiPoint3D(
                        id,
                        name,
                        ifcGuid,
                        nativeId,
                        description,
                        Converters.ConvertValueToMillimeter(pointPosition.X),
                        Converters.ConvertValueToMillimeter(pointPosition.Y),
                        Converters.ConvertValueToMillimeter(pointPosition.Z)
                    );
                    StructuralDataContext.Point3DList.Add(point);
                }
            }

            return new XmiStructuralPointConnection(
                id,
                name,
                ifcGuid,
                nativeId,
                description,
                storey,
                point
            );
        }

        // ✅ 提取公共：查找或创建Storey
        private static XmiStructuralStorey GetOrCreateStorey(Element element)
        {
            XmiStructuralStorey storey = null;
            if (element.LevelId != null && element.LevelId != ElementId.InvalidElementId)
            {
                string levelNativeId = element.LevelId.Value.ToString();

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
            return storey;
        }

        // ✅ 辅助方法：比较XmiPoint3D和XYZ位置是否接近
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
}
