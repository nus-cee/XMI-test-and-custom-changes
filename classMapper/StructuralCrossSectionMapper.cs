using Autodesk.Revit.DB;
using XmiCore;
using System.Linq;
using Lists;
using Utils;

namespace ClassMapper
{
    internal class StructuralCrossSectionMapper : BaseMapper
    {
        public static XmiStructuralCrossSection Map(Element element)
        {
            var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

            // ✅ 提取 Material（从元素提取材质）
            XmiStructuralMaterial material = null;

            if (element is FamilyInstance fi)
            {
                var matIds = fi.GetMaterialIds(false);
                if (matIds.Count > 0)
                {
                    var matElement = element.Document.GetElement(matIds.First()) as Material;
                    if (matElement != null)
                    {
                        string materialNativeId = matElement.Id.Value.ToString();

                        // 尝试从已有列表中查找
                        material = StructuralDataContext.StructuralMaterialList
                            .FirstOrDefault(m => m.NativeId == materialNativeId);

                        // 如果没有找到，就映射并添加进去
                        if (material == null)
                        {
                            material = StructuralMaterialMapper.Map(matElement);
                            StructuralDataContext.StructuralMaterialList.Add(material);
                        }
                    }
                }
            }


            // ✅ 处理 Shape
            string shapeString = "Unknown"; // 默认
            if (element.Category != null)
            {
                shapeString = element.Name;
            }
            var shapeEnum = ExtensionEnumHelper.FromEnumValue<XmiShapeEnum>(shapeString)
                          ?? XmiShapeEnum.Unknown;

            // ✅ 提取 Parameters（比如宽高等）
            string[] parameters = [];

            double width = 0, height = 0;
            if (element.LookupParameter("b") != null)
                width = Converters.ConvertValueToMillimeter(element.LookupParameter("b").AsDouble());
            if (element.LookupParameter("h") != null)
                height = Converters.ConvertValueToMillimeter(element.LookupParameter("h").AsDouble());

            if (width > 0 || height > 0)
            {
                parameters = new string[]
                {
                    width.ToString("F2"),
                    height.ToString("F2")
                };
            }

            // ✅ 面积
            double area = 0;
            if (element.LookupParameter("Area") != null)
            {
                area = Converters.ConvertValueToMillimeter(element.LookupParameter("Area").AsDouble());
            }

            // ✅ 惯性矩、半径、模量、塑性模量、扭转常数
            double secondMomentOfAreaXAxis = 0;
            double secondMomentOfAreaYAxis = 0;
            double radiusOfGyrationXAxis = 0;
            double radiusOfGyrationYAxis = 0;
            double elasticModulusXAxis = 0;
            double elasticModulusYAxis = 0;
            double plasticModulusXAxis = 0;
            double plasticModulusYAxis = 0;
            double torsionalConstant = 0;

            if (element.LookupParameter("Ix") != null)
                secondMomentOfAreaXAxis = Converters.ConvertValueToMillimeter(element.LookupParameter("Ix").AsDouble());

            if (element.LookupParameter("Iy") != null)
                secondMomentOfAreaYAxis = Converters.ConvertValueToMillimeter(element.LookupParameter("Iy").AsDouble());

            if (element.LookupParameter("rx") != null)
                radiusOfGyrationXAxis = Converters.ConvertValueToMillimeter(element.LookupParameter("rx").AsDouble());

            if (element.LookupParameter("ry") != null)
                radiusOfGyrationYAxis = Converters.ConvertValueToMillimeter(element.LookupParameter("ry").AsDouble());

            if (element.LookupParameter("Sx") != null)
                elasticModulusXAxis = Converters.ConvertValueToMillimeter(element.LookupParameter("Sx").AsDouble());

            if (element.LookupParameter("Sy") != null)
                elasticModulusYAxis = Converters.ConvertValueToMillimeter(element.LookupParameter("Sy").AsDouble());

            if (element.LookupParameter("Zx") != null)
                plasticModulusXAxis = Converters.ConvertValueToMillimeter(element.LookupParameter("Zx").AsDouble());

            if (element.LookupParameter("Zy") != null)
                plasticModulusYAxis = Converters.ConvertValueToMillimeter(element.LookupParameter("Zy").AsDouble());

            if (element.LookupParameter("J") != null)
                torsionalConstant = Converters.ConvertValueToMillimeter(element.LookupParameter("J").AsDouble());

            // ✅ 返回 CrossSection
            return new XmiStructuralCrossSection(
                id,
                name,
                ifcGuid,
                nativeId,
                description,
                material,
                shapeEnum,
                parameters,
                area,
                secondMomentOfAreaXAxis,
                secondMomentOfAreaYAxis,
                radiusOfGyrationXAxis,
                radiusOfGyrationYAxis,
                elasticModulusXAxis,
                elasticModulusYAxis,
                plasticModulusXAxis,
                plasticModulusYAxis,
                torsionalConstant
            );
        }
    }
}
