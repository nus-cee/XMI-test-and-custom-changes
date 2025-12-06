using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Betekk.RevitXmiExporter.ClassMapper.Base;
using Betekk.RevitXmiExporter.Utils;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Enums;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Utils;

namespace Betekk.RevitXmiExporter.ClassMapper
{
    internal class MaterialMapper : StructuralBaseEntityMapper
    {
        public static XmiMaterial Map(IXmiManager manager, int modelIndex, Material material)
        {
            try
            {
                var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(material);

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                {
                    ModelInfoBuilder.WriteErrorLogToFile(
                        $"[MaterialMapper] Skipped invalid material. ID={id}, Name={name}, NativeID={nativeId}");
                    return null;
                }

                string materialTypeString = !string.IsNullOrEmpty(material.MaterialClass)
                    ? material.MaterialClass
                    : "Unknown";

                XmiMaterialTypeEnum materialType =
                    ExtensionEnumHelper.FromEnumValue<XmiMaterialTypeEnum>(materialTypeString)
                    ?? XmiMaterialTypeEnum.Unknown;
                double? grade = GetMaterialDoubleParameter(material, "Grade");
                double? unitWeight = GetMaterialDoubleParameter(material, "Unit Weight");

                string eModulus = string.Empty;
                string gModulus = string.Empty;
                string poissonRatio = string.Empty;
                double thermalCoefficient = 0f;

                if (material.StructuralAssetId != ElementId.InvalidElementId)
                {
                    Document doc = material.Document;
                    PropertySetElement structAssetElem = doc.GetElement(material.StructuralAssetId) as PropertySetElement;
                    if (structAssetElem?.GetStructuralAsset() is StructuralAsset structAsset)
                    {
                        unitWeight = Converters.KilogramsPerCubicFootToKilogramsPerCubicMeter(structAsset.Density);
                        eModulus = FormatXYZ(structAsset.YoungModulus);
                        gModulus = FormatXYZ(structAsset.ShearModulus);
                        poissonRatio = FormatXYZ(structAsset.PoissonRatio);
                        thermalCoefficient = structAsset.ThermalExpansionCoefficient.X;
                    }
                }

                XmiMaterial existingMaterial = manager
                    .GetEntitiesOfType<XmiMaterial>(modelIndex)
                    .FirstOrDefault(m => string.Equals(m.NativeId, nativeId, StringComparison.OrdinalIgnoreCase));
                if (existingMaterial != null)
                {
                    return existingMaterial;
                }

                return manager.CreateMaterial(
                    modelIndex,
                    id,
                    name,
                    ifcGuid,
                    nativeId,
                    description,
                    materialType,
                    grade ?? 0f,
                    unitWeight ?? 0f,
                    eModulus,
                    gModulus,
                    poissonRatio,
                    thermalCoefficient);
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[MaterialMapper] {ex}");
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
