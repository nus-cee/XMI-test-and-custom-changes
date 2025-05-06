using Autodesk.Revit.DB;
using XmiCore;

namespace ClassMapper
{
    internal class StructuralMaterialMapper : BaseMapper
    {
        public static XmiStructuralMaterial Map(Element element)
        {
            var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

            // ✅ 获取第一个材质（示例）
            Material material = null;
            if (element is FamilyInstance fi)
            {
                var matIds = fi.GetMaterialIds(false);
                if (matIds.Count > 0)
                    material = element.Document.GetElement(matIds.First()) as Material;
            }

            string materialTypeString = "Unknown"; // 示例，可从 material.MaterialClass 获取
            if (material != null && !string.IsNullOrEmpty(material.MaterialClass))
            {
                materialTypeString = material.MaterialClass;
            }

            var materialType = ExtensionEnumHelper.FromEnumValue<XmiStructuralMaterialTypeEnum>(materialTypeString)
                               ?? XmiStructuralMaterialTypeEnum.Unknown;

            // ✅ 提取参数
            double? grade = GetMaterialDoubleParameter(material, "Grade");
            double? unitWeight = GetMaterialDoubleParameter(material, "Unit Weight");
            double? eModulus = GetMaterialDoubleParameter(material, "Young's Modulus");
            double? gModulus = GetMaterialDoubleParameter(material, "Shear Modulus");
            double? poissonRatio = GetMaterialDoubleParameter(material, "Poisson's Ratio");
            double? thermalCoefficient = GetMaterialDoubleParameter(material, "Thermal Expansion Coefficient");

            return new XmiStructuralMaterial(
                id,
                name,
                ifcGuid,
                nativeId,
                description,
                materialType,
                grade ?? 0f,
                unitWeight ?? 0f,
                eModulus ?? 0f,
                gModulus ?? 0f,
                poissonRatio ?? 0f,
                thermalCoefficient ?? 0f
            );
        }

        // ✅ 把方法放在类里面、方法外面
        private static double? GetMaterialDoubleParameter(Material material, string parameterName)
        {
            if (material == null) return null;

            Parameter parameter = material.LookupParameter(parameterName);
            if (parameter != null && parameter.StorageType == StorageType.Double)
            {
                return parameter.AsDouble();
            }
            return null;
        }
    }
}
