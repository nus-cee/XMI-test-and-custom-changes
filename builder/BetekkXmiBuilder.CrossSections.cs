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
    /// Partial class containing cross-section processing and analytical member handling.
    /// Handles extraction of cross-section shapes and parameters, plus analytical member processing.
    /// </summary>
    public partial class BetekkXmiBuilder
    {
        private XmiCrossSection? GetOrCreateXmiCrossSection(Document doc, FamilyInstance familyInstance)
        {
            if (familyInstance == null)
            {
                return null;
            }

            FamilySymbol familySymbol = familyInstance.Symbol;
            if (familySymbol == null)
            {
                return null;
            }

            ElementId typeId = familySymbol.Id;
            string cacheKey = typeId.ToString();

            // Check cache
            if (_crossSectionCache.TryGetValue(cacheKey, out XmiCrossSection existingCrossSection))
            {
                return existingCrossSection;
            }

            // Extract properties
            string id = Guid.NewGuid().ToString();
            string name = familySymbol.Name ?? "Unknown Section";
            string nativeId = typeId.ToString();

            // Extract material from family instance
            ElementId materialId = GetMaterialIdFromFamilyInstance(familyInstance);
            XmiMaterial? material = GetOrCreateXmiMaterial(doc, materialId);

            // Extract cross-section dimensions
            // For now, use a simplified approach - we'll try to detect common profile types
            // and extract basic dimensions
            (XmiShapeEnum shape, IXmiShapeParameters parameters, double area) = ExtractCrossSectionShape(familySymbol);

            // Extract or calculate section properties
            // Note: Revit doesn't always expose these directly, so we'll use defaults for now
            double secondMomentXAxis = 0;
            double secondMomentYAxis = 0;
            double radiusOfGyrationX = 0;
            double radiusOfGyrationY = 0;
            double elasticModulusX = 0;
            double elasticModulusY = 0;
            double plasticModulusX = 0;
            double plasticModulusY = 0;
            double torsionalConstant = 0;

            // Try to extract area from family parameters
            Parameter areaParam = familySymbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_AREA);
            if (areaParam != null && areaParam.HasValue)
            {
                // Revit stores area in ft², convert to mm²
                double areaFt2 = areaParam.AsDouble();
                area = areaFt2 * 92903.04; // ft² to mm²
            }

            // Create XmiCrossSection
            XmiCrossSection xmiCrossSection = _model.CreateXmiCrossSection(
                id,
                name,
                string.Empty,  // ifcGuid (cross-sections don't have IFC GUIDs from Revit)
                nativeId,
                string.Empty,  // description
                material,
                shape,
                parameters,
                area,
                secondMomentXAxis,
                secondMomentYAxis,
                radiusOfGyrationX,
                radiusOfGyrationY,
                elasticModulusX,
                elasticModulusY,
                plasticModulusX,
                plasticModulusY,
                torsionalConstant
            );

            _crossSectionCache[cacheKey] = xmiCrossSection;
            _crossSectionCount++;
            return xmiCrossSection;
        }

        /// <summary>
        /// Extracts cross-section shape and parameters from Revit FamilySymbol.
        /// Returns shape enum, parameters, and calculated area.
        /// </summary>
        private (XmiShapeEnum shape, IXmiShapeParameters parameters, double area) ExtractCrossSectionShape(FamilySymbol familySymbol)
        {
            // Try to extract common structural framing parameters
            // Width (b)
            Parameter widthParam = familySymbol.LookupParameter("b") ??
                                   familySymbol.LookupParameter("Width") ??
                                   familySymbol.LookupParameter("w");

            // Height (h or d)
            Parameter heightParam = familySymbol.LookupParameter("h") ??
                                    familySymbol.LookupParameter("d") ??
                                    familySymbol.LookupParameter("Height") ??
                                    familySymbol.LookupParameter("Depth");

            if (widthParam != null && heightParam != null && widthParam.HasValue && heightParam.HasValue)
            {
                // Convert from feet to mm
                double width = Converters.ConvertValueToMillimeter(widthParam.AsDouble());
                double height = Converters.ConvertValueToMillimeter(heightParam.AsDouble());

                // Check if it's square or rectangular
                if (Math.Abs(width - height) < 0.1)
                {
                    // Square section
                    var parameters = new RectangularShapeParameters(height, width);
                    double area = height * width;
                    return (XmiShapeEnum.Rectangular, parameters, area);
                }
                else
                {
                    // Rectangular section
                    var parameters = new RectangularShapeParameters(height, width);
                    double area = height * width;
                    return (XmiShapeEnum.Rectangular, parameters, area);
                }
            }

            // Try to detect circular sections
            Parameter diameterParam = familySymbol.LookupParameter("Diameter") ??
                                      familySymbol.LookupParameter("D") ??
                                      familySymbol.LookupParameter("d");

            if (diameterParam != null && diameterParam.HasValue)
            {
                double diameter = Converters.ConvertValueToMillimeter(diameterParam.AsDouble());
                var parameters = new CircularShapeParameters(diameter);
                double area = Math.PI * diameter * diameter / 4.0;
                return (XmiShapeEnum.Circular, parameters, area);
            }

            // Fallback: Unknown shape with empty parameters dictionary
            var unknownParams = new UnknownShapeParameters(new Dictionary<string, double>());
            return (XmiShapeEnum.Unknown, unknownParams, 0);
        }

        /// <summary>
        /// Processes ALL analytical members in the model (with or without physical associations).
        /// Extracts section types, creates cross-sections, and builds the analytical model.
        /// This runs BEFORE physical element processing to establish analytical entities first.
        /// </summary>
        private void ProcessAnalyticalMembers(Document doc)
        {
            try
            {
                // Try multiple collection strategies to find analytical members

                // Strategy 1: By Class (AnalyticalMember)
                FilteredElementCollector byClass = new FilteredElementCollector(doc)
                    .OfClass(typeof(AnalyticalMember))
                    .WhereElementIsNotElementType();
                int byClassCount = byClass.GetElementCount();

                // Strategy 2: By Category OST_AnalyticalMember
                FilteredElementCollector byCategory = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_AnalyticalMember)
                    .WhereElementIsNotElementType();
                int byCategoryCount = byCategory.GetElementCount();

                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Collection strategies - ByClass: {byClassCount}, ByCategory: {byCategoryCount}");

                // If no elements found, do a diagnostic scan
                if (byClassCount == 0 && byCategoryCount == 0)
                {
                    ModelInfoBuilder.WriteErrorLogToFile(
                        $"[BetekkXmiBuilder] No analytical members found. Running diagnostic scan...");

                    // Check if there are any analytical elements at all
                    FilteredElementCollector allElements = new FilteredElementCollector(doc);
                    HashSet<string> analyticalTypes = new HashSet<string>();

                    foreach (Element elem in allElements)
                    {
                        string typeName = elem.GetType().Name;
                        if (typeName.Contains("Analytical", StringComparison.OrdinalIgnoreCase))
                        {
                            analyticalTypes.Add($"{typeName} (Category: {elem.Category?.Name ?? "None"})");
                        }
                    }

                    if (analyticalTypes.Count > 0)
                    {
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"[BetekkXmiBuilder] Found these analytical element types: {string.Join(", ", analyticalTypes)}");
                    }
                    else
                    {
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"[BetekkXmiBuilder] No analytical elements found in model. The model may not have analytical model enabled.");
                    }
                }

                // Use whichever strategy found elements
                FilteredElementCollector analyticalCollector = byClassCount > 0 ? byClass : byCategory;
                int collectedCount = analyticalCollector.GetElementCount();

                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Using collection strategy that found {collectedCount} analytical members");

                foreach (Element element in analyticalCollector)
                {
                    try
                    {
                        AnalyticalMember analyticalMember = element as AnalyticalMember;
                        if (analyticalMember == null)
                        {
                            ModelInfoBuilder.WriteErrorLogToFile(
                                $"[BetekkXmiBuilder] Skipping element {element?.Id} (Type: {element?.GetType().Name}) - not an AnalyticalMember type");
                            continue;
                        }

                        string analyticalNativeId = analyticalMember.Id.ToString();
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"[BetekkXmiBuilder] Processing analytical member {analyticalNativeId} - {analyticalMember.Name}");

                        // Get the analytical curve
                        Curve curve = analyticalMember.GetCurve();
                        if (curve == null)
                        {
                            ModelInfoBuilder.WriteErrorLogToFile(
                                $"[BetekkXmiBuilder] Skipping analytical member {analyticalNativeId} - no curve found");
                            continue;
                        }

                        XYZ startPoint = curve.GetEndPoint(0);
                        XYZ endPoint = curve.GetEndPoint(1);

                        // Get element name - handle both null and empty strings
                        string name = string.IsNullOrWhiteSpace(analyticalMember.Name)
                            ? $"Analytical_{analyticalNativeId}"
                            : analyticalMember.Name;

                        // Get IFC GUID if exists
                        string ifcGuid = GetIfcGuidFromElement(analyticalMember);

                        // Try to determine if it's a column or beam based on orientation
                        // Vertical elements (columns) have significant Z-component
                        XYZ direction = (endPoint - startPoint).Normalize();
                        bool isColumn = Math.Abs(direction.Z) > 0.7; // If >70% vertical, treat as column

                        // Try to get storey/level
                        XmiStorey? storey = null;
                        try
                        {
                            Level level = doc.GetElement(analyticalMember.LevelId) as Level;
                            if (level != null)
                            {
                                string levelId = level.Id.ToString();
                                _storeyCache.TryGetValue(levelId, out storey);
                            }
                        }
                        catch { }

                        // Generate IDs for XMI entities
                        string analyticalId = Guid.NewGuid().ToString();

                        // Create deduplicated Point3D entities
                        XmiPoint3d startXmiPoint = GetOrReusePoint3D(startPoint, $"{analyticalId}_start_point");
                        XmiPoint3d endXmiPoint = GetOrReusePoint3D(endPoint, $"{analyticalId}_end_point");

                        // Create StructuralPointConnections for analytical domain
                        XmiStructuralPointConnection startConnection = GetOrCreateXmiStructuralPointConnection(
                            startPoint,
                            $"{analyticalId}_start_connection",
                            $"{name}_start",
                            $"{analyticalNativeId}_start",
                            storey,
                            startXmiPoint);

                        XmiStructuralPointConnection endConnection = GetOrCreateXmiStructuralPointConnection(
                            endPoint,
                            $"{analyticalId}_end_connection",
                            $"{name}_end",
                            $"{analyticalNativeId}_end",
                            storey,
                            endXmiPoint);

                        // Extract Section Type property from analytical member and create cross-section
                        XmiCrossSection? crossSection = GetOrCreateXmiCrossSectionFromAnalyticalMember(doc, analyticalMember);

                        // Create analytical representation (XmiStructuralCurveMember)
                        XmiStructuralCurveMember xmiAnalyticalMember = CreateXmiStructuralCurveMember(
                            analyticalId,
                            name,
                            ifcGuid,
                            analyticalNativeId,  // Use analytical element's NativeId
                            storey,
                            isColumn,
                            startConnection,
                            endConnection,
                            startXmiPoint,
                            endXmiPoint,
                            curve,
                            crossSection);  // Pass cross-section (optional)

                        // Store in cache for later lookup by physical elements
                        _analyticalMemberCache[analyticalNativeId] = xmiAnalyticalMember;
                        _analyticalMemberCount++;
                    }
                    catch (Exception ex)
                    {
                        string elementId = element?.Id?.ToString() ?? "unknown";
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"[BetekkXmiBuilder] Failed to process analytical member {elementId}: {ex.Message}");
                        ModelInfoBuilder.WriteErrorLogToFile($"[BetekkXmiBuilder] Stack trace: {ex.StackTrace}");
                    }
                }

                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Successfully processed {_analyticalMemberCount} analytical members");
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Error processing analytical members: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts the Section Type property from an Revit AnalyticalMember and creates
        /// an XmiCrossSection if a section type is assigned.
        /// Returns null if no section type is found.
        /// </summary>
        private XmiCrossSection? GetOrCreateXmiCrossSectionFromAnalyticalMember(Document doc, AnalyticalMember analyticalMember)
        {
            try
            {
                // Try to get the Section Type parameter from the analytical member
                // In Revit's analytical model, this is typically stored as a parameter
                Parameter sectionTypeParam = analyticalMember.LookupParameter("Section Type");

                if (sectionTypeParam == null || !sectionTypeParam.HasValue)
                {
                    // No section type assigned - leave analytical member without cross-section
                    return null;
                }

                // Get the element ID of the section type
                ElementId sectionTypeId = sectionTypeParam.AsElementId();
                if (sectionTypeId == null || sectionTypeId == ElementId.InvalidElementId)
                {
                    // Invalid section type - leave analytical member without cross-section
                    return null;
                }

                string cacheKey = sectionTypeId.ToString();

                // Check cache for existing cross-section
                if (_crossSectionCache.TryGetValue(cacheKey, out XmiCrossSection existingCrossSection))
                {
                    return existingCrossSection;
                }

                // Get the section type element (this is typically a FamilySymbol for structural sections)
                Element sectionTypeElement = doc.GetElement(sectionTypeId);
                if (sectionTypeElement == null)
                {
                    return null;
                }

                // Extract properties from the section type
                string id = Guid.NewGuid().ToString();
                string name = sectionTypeElement.Name ?? "Unknown Section";
                string nativeId = sectionTypeId.ToString();

                // Try to extract material from the section type
                XmiMaterial? material = null;
                ElementId materialId = ElementId.InvalidElementId;

                // Try to get material from section type
                if (sectionTypeElement is FamilySymbol familySymbol)
                {
                    Parameter materialParam = familySymbol.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                    if (materialParam != null && materialParam.HasValue)
                    {
                        materialId = materialParam.AsElementId();
                    }
                }

                if (materialId != ElementId.InvalidElementId)
                {
                    material = GetOrCreateXmiMaterial(doc, materialId);
                }

                // Extract shape parameters from the section type
                (XmiShapeEnum shape, IXmiShapeParameters parameters, double area) =
                    ExtractCrossSectionShapeFromElement(sectionTypeElement);

                // Try to extract area from section parameters
                Parameter areaParam = sectionTypeElement.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_AREA);
                if (areaParam != null && areaParam.HasValue)
                {
                    // Revit stores area in ft², convert to mm²
                    double areaFt2 = areaParam.AsDouble();
                    area = areaFt2 * 92903.04; // ft² to mm²
                }

                // Create XmiCrossSection
                XmiCrossSection xmiCrossSection = _model.CreateXmiCrossSection(
                    id,
                    name,
                    string.Empty,  // ifcGuid (sections don't have IFC GUIDs)
                    nativeId,
                    string.Empty,  // description
                    material,
                    shape,
                    parameters,
                    area,
                    0, 0, 0, 0, 0, 0, 0, 0, 0  // section properties (deferred for now)
                );

                _crossSectionCache[cacheKey] = xmiCrossSection;
                _crossSectionCount++;
                return xmiCrossSection;
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Error extracting section type from analytical member {analyticalMember.Id}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts cross-section shape and parameters from any Revit element (works for both
        /// FamilySymbol and other element types).
        /// </summary>
        private (XmiShapeEnum shape, IXmiShapeParameters parameters, double area) ExtractCrossSectionShapeFromElement(Element element)
        {
            // Try FamilySymbol first
            if (element is FamilySymbol familySymbol)
            {
                return ExtractCrossSectionShape(familySymbol);
            }

            // Try to extract parameters directly from the element
            // Width (b)
            Parameter widthParam = element.LookupParameter("b") ??
                                   element.LookupParameter("Width") ??
                                   element.LookupParameter("w");

            // Height (h or d)
            Parameter heightParam = element.LookupParameter("h") ??
                                    element.LookupParameter("d") ??
                                    element.LookupParameter("Height") ??
                                    element.LookupParameter("Depth");

            if (widthParam != null && heightParam != null && widthParam.HasValue && heightParam.HasValue)
            {
                // Convert from feet to mm
                double width = Converters.ConvertValueToMillimeter(widthParam.AsDouble());
                double height = Converters.ConvertValueToMillimeter(heightParam.AsDouble());

                var parameters = new RectangularShapeParameters(height, width);
                double area = height * width;
                return (XmiShapeEnum.Rectangular, parameters, area);
            }

            // Try circular
            Parameter diameterParam = element.LookupParameter("Diameter") ??
                                      element.LookupParameter("D") ??
                                      element.LookupParameter("d");

            if (diameterParam != null && diameterParam.HasValue)
            {
                double diameter = Converters.ConvertValueToMillimeter(diameterParam.AsDouble());
                var parameters = new CircularShapeParameters(diameter);
                double area = Math.PI * diameter * diameter / 4.0;
                return (XmiShapeEnum.Circular, parameters, area);
            }

            // Fallback: Unknown shape
            var unknownParams = new UnknownShapeParameters(new Dictionary<string, double>());
            return (XmiShapeEnum.Unknown, unknownParams, 0);
        }

    }
}
