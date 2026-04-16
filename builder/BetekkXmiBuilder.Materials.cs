using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Betekk.RevitXmiExporter.Utils;
using System.Linq;
using XmiSchema.Entities.Bases;
using XmiSchema.Entities.Commons;
using XmiSchema.Entities.Geometries;
using XmiSchema.Entities.Physical;
using XmiSchema.Entities.Relationships;
using XmiSchema.Entities.StructuralAnalytical;
using XmiSchema.Enums;
using XmiSchema.Managers;
using XmiSchema.Parameters;

namespace Betekk.RevitXmiExporter.Builder
{
    /// <summary>
    /// Partial class containing material processing methods.
    /// Handles extraction and caching of materials from Revit elements.
    /// </summary>
    public partial class BetekkXmiBuilder
    {
        private XmiMaterial? GetOrCreateXmiMaterial(Document doc, ElementId materialId)
        {
            if (materialId == null || materialId == ElementId.InvalidElementId)
            {
                return null;
            }

            string cacheKey = materialId.ToString();

            // Check cache
            if (_materialCache.TryGetValue(cacheKey, out XmiMaterial existingMaterial))
            {
                return existingMaterial;
            }

            // Get Revit material
            Material revitMaterial = doc.GetElement(materialId) as Material;
            if (revitMaterial == null)
            {
                return null;
            }

            // Extract properties
            string id = Guid.NewGuid().ToString();
            string name = revitMaterial.Name ?? "Unknown Material";
            string nativeId = materialId.ToString();

            // Map Revit material class to XmiMaterialTypeEnum
            XmiMaterialTypeEnum materialType = MapRevitMaterialClass(revitMaterial);

            // Extract structural properties
            StructuralAsset structuralAsset = null;
            try
            {
                PropertySetElement propSet = doc.GetElement(revitMaterial.StructuralAssetId) as PropertySetElement;
                if (propSet != null)
                {
                    structuralAsset = propSet.GetStructuralAsset();
                }
            }
            catch { }

            // Default values
            double grade = 0;
            double unitWeight = 0; // kg/m³
            string elasticModulus = "0";
            string shearModulus = "0";
            string poissonRatio = "0";
            double thermalCoefficient = 0;

            if (structuralAsset != null)
            {
                // Unit weight (density) - Revit stores in lb/ft³, convert to kg/m³
                if (structuralAsset.Density != null)
                {
                    double densityLbPerCubicFt = structuralAsset.Density;
                    unitWeight = densityLbPerCubicFt * 16.0185; // lb/ft³ to kg/m³
                }

                // Elastic modulus - Revit stores in psi, convert to MPa
                if (structuralAsset.YoungModulus != null)
                {
                    double youngModulusPsi = structuralAsset.YoungModulus.X; // Use X-axis value
                    double youngModulusMPa = youngModulusPsi * 0.00689476; // psi to MPa
                    elasticModulus = youngModulusMPa.ToString("F2");
                }

                // Shear modulus - Revit stores in psi, convert to MPa
                if (structuralAsset.ShearModulus != null)
                {
                    double shearModulusPsi = structuralAsset.ShearModulus.X;
                    double shearModulusMPa = shearModulusPsi * 0.00689476; // psi to MPa
                    shearModulus = shearModulusMPa.ToString("F2");
                }

                // Poisson's ratio (dimensionless)
                if (structuralAsset.PoissonRatio != null)
                {
                    poissonRatio = structuralAsset.PoissonRatio.X.ToString("F4");
                }

                // Thermal expansion coefficient - Revit stores in 1/°F, convert to 1/°C
                if (structuralAsset.ThermalExpansionCoefficient != null)
                {
                    double thermalExpPerF = structuralAsset.ThermalExpansionCoefficient.X;
                    thermalCoefficient = thermalExpPerF * 1.8; // 1/°F to 1/°C
                }

                // Try to extract grade/strength (material-specific)
                // For concrete: CompressiveStrength
                // For steel: MinimumYieldStress or MinimumTensileStrength
                if (structuralAsset.ConcreteCompression != null)
                {
                    double strengthPsi = structuralAsset.ConcreteCompression;
                    grade = strengthPsi * 0.00689476; // Convert psi to MPa
                }
                else if (structuralAsset.MinimumYieldStress != null)
                {
                    double yieldPsi = structuralAsset.MinimumYieldStress;
                    grade = yieldPsi * 0.00689476; // Convert psi to MPa
                }
                else if (structuralAsset.MinimumTensileStrength != null)
                {
                    double tensilePsi = structuralAsset.MinimumTensileStrength;
                    grade = tensilePsi * 0.00689476; // Convert psi to MPa
                }
            }

            // Create XmiMaterial
            XmiMaterial xmiMaterial = _model.CreateXmiMaterial(
                id,
                name,
                string.Empty,  // ifcGuid (materials don't have IFC GUIDs from Revit)
                nativeId,
                string.Empty,  // description
                materialType,
                grade,
                unitWeight,
                elasticModulus,
                shearModulus,
                poissonRatio,
                thermalCoefficient
            );

            _materialCache[cacheKey] = xmiMaterial;
            _materialCount++;
            return xmiMaterial;
        }

        /// <summary>
        /// Maps Revit material class to XmiMaterialTypeEnum.
        /// </summary>
        private XmiMaterialTypeEnum MapRevitMaterialClass(Material revitMaterial)
        {
            string materialClass = revitMaterial.MaterialClass ?? "";

            // Revit material classes (common ones)
            if (materialClass.Contains("Concrete", StringComparison.OrdinalIgnoreCase))
                return XmiMaterialTypeEnum.Concrete;
            if (materialClass.Contains("Steel", StringComparison.OrdinalIgnoreCase) ||
                materialClass.Contains("Metal", StringComparison.OrdinalIgnoreCase))
                return XmiMaterialTypeEnum.Steel;
            if (materialClass.Contains("Wood", StringComparison.OrdinalIgnoreCase) ||
                materialClass.Contains("Timber", StringComparison.OrdinalIgnoreCase))
                return XmiMaterialTypeEnum.Timber;
            if (materialClass.Contains("Aluminum", StringComparison.OrdinalIgnoreCase) ||
                materialClass.Contains("Aluminium", StringComparison.OrdinalIgnoreCase))
                return XmiMaterialTypeEnum.Aluminium;
            if (materialClass.Contains("Masonry", StringComparison.OrdinalIgnoreCase))
                return XmiMaterialTypeEnum.Masonry;

            return XmiMaterialTypeEnum.Unknown;
        }

        /// <summary>
        /// Retrieves the structural material ElementId from a FamilyInstance, falling back to its type.
        /// </summary>
        private ElementId GetMaterialIdFromFamilyInstance(FamilyInstance familyInstance)
        {
            if (familyInstance == null)
            {
                return ElementId.InvalidElementId;
            }

            ElementId materialId = familyInstance.StructuralMaterialId;
            if (materialId != null && materialId != ElementId.InvalidElementId)
            {
                return materialId;
            }

            FamilySymbol familySymbol = familyInstance.Symbol;
            if (familySymbol == null)
            {
                return ElementId.InvalidElementId;
            }

            Parameter materialParam = familySymbol.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (materialParam != null && materialParam.HasValue)
            {
                return materialParam.AsElementId();
            }

            return ElementId.InvalidElementId;
        }

        /// <summary>
        /// Ensures all materials associated with the given element are added to the XMI material list.
        /// Handles both FamilyInstance structural material parameters and general material assignments.
        /// </summary>
        private void ExportMaterialsForElement(Document doc, Element element)
        {
            if (element == null)
            {
                return;
            }

            // Prefer the structural material on family instances (covers beams/columns)
            if (element is FamilyInstance familyInstance)
            {
                ElementId structuralMaterialId = GetMaterialIdFromFamilyInstance(familyInstance);
                if (structuralMaterialId != null && structuralMaterialId != ElementId.InvalidElementId)
                {
                    GetOrCreateXmiMaterial(doc, structuralMaterialId);
                }
            }

            // Gather all other materials assigned to the element (e.g., layered floors/walls)
            ICollection<ElementId> materialIds = null;
            try
            {
                materialIds = element.GetMaterialIds(false);
            }
            catch
            {
                // If material extraction fails, do not block export; material list will just omit this element.
            }

            if (materialIds == null || materialIds.Count == 0)
            {
                return;
            }

            foreach (ElementId materialId in materialIds)
            {
                if (materialId != null && materialId != ElementId.InvalidElementId)
                {
                    GetOrCreateXmiMaterial(doc, materialId);
                }
            }
        }

        /// <summary>
        /// Gets or creates a deduplicated XmiCrossSection from a Revit FamilySymbol (Type).
        /// Uses Revit FamilySymbol ElementId as cache key.
        /// </summary>
    }
}
