using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Utils;
using XmiSchema.Core.Enums;
using Utils;
using XmiSchema.Core.Manager;

namespace ClassMapper
{
    internal class StructuralMaterialMapper : BaseMapper
    {
        public static XmiStructuralMaterial Map(IXmiManager manager, int modelIndex, Material material)
        {
            try
            {
            var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(material);

                // ✅ 前置校验防止创建无效 material
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                {
                    Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile(
                        $"[StructuralMaterialMapper] Skipped invalid material. ID={id}, Name={name}, NativeID={nativeId}");
                    return null;
                }

                string materialTypeString = !string.IsNullOrEmpty(material.MaterialClass)
                ? material.MaterialClass
                : "Unknown";

            var materialType = ExtensionEnumHelper.FromEnumValue<XmiStructuralMaterialTypeEnum>(materialTypeString)
                               ?? XmiStructuralMaterialTypeEnum.Unknown;

            double? grade = GetMaterialDoubleParameter(material, "Grade");
            double? unitWeight = GetMaterialDoubleParameter(material, "Unit Weight");

            // ✅ 获取结构资产参数
            string? eModulus = null;
            string? gModulus = null;
            string? poissonRatio = null;
            double? thermalCoefficient = null;

            if (material.StructuralAssetId != ElementId.InvalidElementId)
            {
                var doc = material.Document;
                var structAssetElem = doc.GetElement(material.StructuralAssetId) as PropertySetElement;
                if (structAssetElem?.GetStructuralAsset() is StructuralAsset structAsset)
                {
                    unitWeight = Converters.KilogramsPerCubicFootToKilogramsPerCubicMeter(structAsset.Density);
                    eModulus = FormatXYZ(structAsset.YoungModulus);
                    gModulus = FormatXYZ(structAsset.ShearModulus);
                    poissonRatio = FormatXYZ(structAsset.PoissonRatio);
                    thermalCoefficient = structAsset.ThermalExpansionCoefficient.X;
                }
            }

            // ✅ 使用 CreateMaterial 方法创建材料
            return manager.CreateStructuralMaterial(
                modelIndex,
                id,
                name,
                ifcGuid,
                nativeId,
                description,
                materialType,
                grade ?? 0f,
                unitWeight ?? 0f,
                eModulus ?? string.Empty,
                gModulus ?? string.Empty,
                poissonRatio ?? string.Empty,
                thermalCoefficient ?? 0f
            );
        }            catch (Exception ex)
            {
                Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile($"[StructuralMaterialMapper] Error: {ex}");
                throw;
            }
        }

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

        private static string FormatXYZ(XYZ vec)
        {
            return $"({vec.X:0.##################}, {vec.Y:0.##################}, {vec.Z:0.##################})";
        }
    }
}
