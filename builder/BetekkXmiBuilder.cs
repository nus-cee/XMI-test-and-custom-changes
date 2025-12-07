using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Betekk.RevitXmiExporter.Utils;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Models;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Models.Entities.StructuralAnalytical;
using XmiSchema.Core.Models.Entities.Physical;
using XmiSchema.Core.Relationships;
using XmiSchema.Core.Geometries;
using XmiSchema.Core.Enums;
using XmiSchema.Core.Parameters;

namespace Betekk.RevitXmiExporter.Builder
{
    /// <summary>
    /// Coordinates extraction of structural framing elements (beams, columns) from Revit
    /// and builds dual-representation XMI model (physical + analytical domains).
    /// Includes materials, cross-sections, and geometry.
    /// </summary>
    public class BetekkXmiBuilder
    {
        private readonly IXmiManager _manager;
        private XmiModel _model;
        private const int ModelIndex = 0;

        // Point deduplication cache (tolerance: 1e-10)
        private readonly Dictionary<string, XmiPoint3D> _pointCache;

        // Storey cache to avoid duplicates
        private readonly Dictionary<string, XmiStorey> _storeyCache;

        // StructuralPointConnection cache
        private readonly Dictionary<string, XmiStructuralPointConnection> _connectionCache;

        // Material cache (keyed by Revit Material ElementId)
        private readonly Dictionary<string, XmiMaterial> _materialCache;

        // CrossSection cache (keyed by Revit FamilySymbol/Type ElementId)
        private readonly Dictionary<string, XmiCrossSection> _crossSectionCache;

        // Cache for analytical members (keyed by Revit ElementId as string)
        // Stores XmiStructuralCurveMember entities created from AnalyticalMember elements
        private readonly Dictionary<string, XmiStructuralCurveMember> _analyticalMemberCache;

        // Placeholder cross-section for analytical members without section types assigned
        // This is a temporary workaround until XmiSchema.Core supports nullable cross-sections
        private XmiCrossSection? _placeholderCrossSection;

        // Tolerance for point deduplication (1e-10 in mm)
        private const double PointTolerance = 1e-10;

        public BetekkXmiBuilder()
        {
            _manager = new XmiManager();
            _model = new XmiModel();
            _manager.Models = new List<XmiModel> { _model };

            _pointCache = new Dictionary<string, XmiPoint3D>();
            _storeyCache = new Dictionary<string, XmiStorey>();
            _connectionCache = new Dictionary<string, XmiStructuralPointConnection>();
            _materialCache = new Dictionary<string, XmiMaterial>();
            _crossSectionCache = new Dictionary<string, XmiCrossSection>();
            _analyticalMemberCache = new Dictionary<string, XmiStructuralCurveMember>();
        }

        /// <summary>
        /// Main orchestration method that processes Revit document and builds XMI model.
        /// </summary>
        /// <param name="doc">Active Revit document to inspect.</param>
        public void BuildModel(Document doc)
        {
            // Phase 1: Process levels/storeys
            ProcessStoreys(doc);

            // Phase 2: Process ALL analytical members first (with or without physical associations)
            ProcessAnalyticalMembers(doc);

            // Phase 3: Process physical elements (beams and columns) and link to analytical members
            ProcessStructuralFramingElements(doc);
        }

        /// <summary>
        /// Serializes the XMI model to JSON string.
        /// </summary>
        /// <returns>JSON representation of the XMI model.</returns>
        public string GetJson()
        {
            return _manager.BuildJson(ModelIndex);
        }

        /// <summary>
        /// Extracts all Level elements and creates XmiStorey entities.
        /// Populates _storeyCache for later reference by structural elements.
        /// </summary>
        private void ProcessStoreys(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                if (element is Level level)
                {
                    string nativeId = level.Id.ToString();

                    // Skip if already processed
                    if (_storeyCache.ContainsKey(nativeId))
                        continue;

                    try
                    {
                        string id = Guid.NewGuid().ToString();
                        string name = string.IsNullOrWhiteSpace(level.Name) ? id : level.Name;
                        string ifcGuid = GetIfcGuidFromElement(level);
                        double elevationMm = Converters.ConvertValueToMillimeter(level.Elevation);

                        XmiStorey storey = _model.CreateStorey(
                            id,
                            name,
                            ifcGuid,
                            nativeId,
                            string.Empty,  // description
                            elevationMm,
                            0.0            // mass
                        );

                        _storeyCache[nativeId] = storey;
                    }
                    catch (Exception ex)
                    {
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"[BetekkXmiBuilder] Failed to process storey {nativeId}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Main processing loop:
        /// 1. Collect all StructuralFraming (beams) and StructuralColumns
        /// 2. Extract geometry from LocationCurve
        /// 3. Create physical entities (XmiBeam or XmiColumn based on category)
        /// 4. Create analytical entities (XmiStructuralCurveMember)
        /// 5. Ensure point deduplication
        /// </summary>
        private void ProcessStructuralFramingElements(Document doc)
        {
            // Collect structural framing elements (beams)
            FilteredElementCollector beamCollector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType();

            foreach (Element element in beamCollector)
            {
                if (element is FamilyInstance familyInstance)
                {
                    try
                    {
                        ProcessSingleFramingElement(doc, familyInstance, isColumn: false);
                    }
                    catch (Exception ex)
                    {
                        string elementId = familyInstance.Id?.ToString() ?? "unknown";
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"[BetekkXmiBuilder] Failed to process beam element {elementId}: {ex.Message}");
                    }
                }
            }

            // Collect structural columns
            FilteredElementCollector columnCollector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType();

            foreach (Element element in columnCollector)
            {
                if (element is FamilyInstance familyInstance)
                {
                    try
                    {
                        ProcessSingleFramingElement(doc, familyInstance, isColumn: true);
                    }
                    catch (Exception ex)
                    {
                        string elementId = familyInstance.Id?.ToString() ?? "unknown";
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"[BetekkXmiBuilder] Failed to process column element {elementId}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Process a single structural element (beam or column):
        /// - Extract geometry from LocationCurve
        /// - Create dual representation (physical + analytical)
        /// </summary>
        /// <param name="doc">Active Revit document</param>
        /// <param name="familyInstance">Structural element to process</param>
        /// <param name="isColumn">True if element is from OST_StructuralColumns, false if from OST_StructuralFraming</param>
        private void ProcessSingleFramingElement(Document doc, FamilyInstance familyInstance, bool isColumn)
        {
            // Extract basic properties
            string id = Guid.NewGuid().ToString();
            string name = familyInstance.Name ?? id;
            string ifcGuid = GetIfcGuidFromElement(familyInstance);
            string nativeId = familyInstance.Id.ToString();

            // Get geometry from LocationCurve
            Location location = familyInstance.Location;
            if (location == null || !(location is LocationCurve locationCurve))
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Element {nativeId} has no LocationCurve");
                return;
            }

            Curve curve = locationCurve.Curve;
            if (curve == null)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Element {nativeId} LocationCurve has no Curve");
                return;
            }

            // Get start and end points
            XYZ startPoint = curve.GetEndPoint(0);
            XYZ endPoint = curve.GetEndPoint(1);

            if (startPoint == null || endPoint == null)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Element {nativeId} has null endpoints");
                return;
            }

            // Get storey reference
            XmiStorey storey = null;
            if (familyInstance.LevelId != null && familyInstance.LevelId != ElementId.InvalidElementId)
            {
                string levelNativeId = familyInstance.LevelId.ToString();
                _storeyCache.TryGetValue(levelNativeId, out storey);
            }

            // Get analytical element ID using the new Revit 2023+ API (returns null if no association exists)
            string? analyticalNativeId = GetAnalyticalElementId(doc, familyInstance);

            // Generate separate IDs for physical and analytical entities
            string physicalId = Guid.NewGuid().ToString();

            // Create deduplicated Point3D entities
            XmiPoint3D startXmiPoint = GetOrCreatePoint3D(startPoint, $"{physicalId}_start_point");
            XmiPoint3D endXmiPoint = GetOrCreatePoint3D(endPoint, $"{physicalId}_end_point");

            // Create physical entity (XmiBeam or XmiColumn)
            CreatePhysicalElement(
                doc,
                familyInstance,
                physicalId,
                name,
                ifcGuid,
                nativeId,
                isColumn,
                curve,
                startXmiPoint,
                endXmiPoint,
                out XmiBasePhysicalEntity physicalEntity);

            // Link to existing analytical member if it was already processed in Phase 2
            if (!string.IsNullOrEmpty(analyticalNativeId))
            {
                // Look up the analytical member from cache (created in Phase 2)
                if (_analyticalMemberCache.TryGetValue(analyticalNativeId, out XmiStructuralCurveMember analyticalMember))
                {
                    // Create relationship: Physical Element → Analytical Member
                    CreatePhysicalToAnalyticalRelationship(physicalEntity, analyticalMember);
                }
            }
        }

        /// <summary>
        /// Gets the IfcGuid from a Revit element if it exists, otherwise returns empty string.
        /// Only reads existing IFC GUID from native Revit - does NOT generate new GUIDs.
        /// Preserves data fidelity from the source software.
        /// </summary>
        private string GetIfcGuidFromElement(Element element)
        {
            if (element == null)
            {
                return string.Empty;
            }
            
            // Check for IfcGUID parameter (set by Revit IFC exporter when "Store IFC GUID" option is enabled)
            Parameter ifcGuidParam = element.get_Parameter(BuiltInParameter.IFC_GUID);
            if (ifcGuidParam != null && ifcGuidParam.HasValue)
            {
                string ifcGuidValue = ifcGuidParam.AsString();
                if (!string.IsNullOrWhiteSpace(ifcGuidValue))
                {
                    return ifcGuidValue;
                }
            }

            // Try to get from shared parameter "IfcGUID" or "IFC GUID" (custom workflows)
            foreach (Parameter param in element.Parameters)
            {
                if (param.Definition != null)
                {
                    string paramName = param.Definition.Name;
                    if ((paramName == "IfcGUID" || paramName == "IFC GUID" || paramName == "IFCGUID")
                        && param.HasValue
                        && param.StorageType == StorageType.String)
                    {
                        string guidValue = param.AsString();
                        if (!string.IsNullOrWhiteSpace(guidValue))
                        {
                            return guidValue;
                        }
                    }
                }
            }

            // No IFC GUID found - return empty string (do NOT generate)
            return string.Empty;
        }


        /// <summary>
        /// Gets or creates a deduplicated XmiPoint3D.
        /// Uses coordinate-based key with tolerance of 1e-10 mm.
        /// </summary>
        private XmiPoint3D GetOrCreatePoint3D(XYZ revitPoint, string fallbackId)
        {
            // Convert to millimeters
            double x = Converters.ConvertValueToMillimeter(revitPoint.X);
            double y = Converters.ConvertValueToMillimeter(revitPoint.Y);
            double z = Converters.ConvertValueToMillimeter(revitPoint.Z);

            // Round to tolerance (1e-10)
            double roundedX = Math.Round(x, 10);
            double roundedY = Math.Round(y, 10);
            double roundedZ = Math.Round(z, 10);

            // Create cache key
            string key = $"{roundedX:F10}_{roundedY:F10}_{roundedZ:F10}";

            // Check cache
            if (_pointCache.TryGetValue(key, out XmiPoint3D existingPoint))
            {
                return existingPoint;
            }

            // Create new point
            string id = Guid.NewGuid().ToString();
            string name = fallbackId ?? id;

            XmiPoint3D newPoint = _model.CreatePoint3D(
                id,
                name,
                string.Empty,  // ifcGuid (empty - synthetic geometry, not a Revit element)
                $"synthetic:point:{key}",  // nativeId (synthetic - not a Revit element)
                string.Empty,  // description
                roundedX,
                roundedY,
                roundedZ
            );

            _pointCache[key] = newPoint;
            return newPoint;
        }

        /// <summary>
        /// Gets or creates a deduplicated XmiStructuralPointConnection.
        /// Reuses existing connection at same coordinate.
        /// </summary>
        private XmiStructuralPointConnection GetOrCreatePointConnection(
            XYZ revitPoint,
            string fallbackId,
            string fallbackName,
            string nativeId,
            XmiStorey storey,
            XmiPoint3D point)
        {
            // Use same coordinate key as point cache
            double x = Math.Round(Converters.ConvertValueToMillimeter(revitPoint.X), 10);
            double y = Math.Round(Converters.ConvertValueToMillimeter(revitPoint.Y), 10);
            double z = Math.Round(Converters.ConvertValueToMillimeter(revitPoint.Z), 10);

            string key = $"{x:F10}_{y:F10}_{z:F10}";

            // Check cache
            if (_connectionCache.TryGetValue(key, out XmiStructuralPointConnection existing))
            {
                return existing;
            }

            // Create new connection
            string id = Guid.NewGuid().ToString();

            XmiStructuralPointConnection connection = _model.CreateStructurePointConnection(
                id,
                fallbackName ?? id,
                string.Empty, // ifcGuid (empty - synthetic analytical node, not a Revit element)
                $"synthetic:connection:{nativeId}",  // nativeId (synthetic - analytical node, not a Revit element)
                string.Empty, // description
                storey,       // XmiStorey (can be null)
                point         // XmiPoint3D
            );

            _connectionCache[key] = connection;
            return connection;
        }

        /// <summary>
        /// Creates XmiStructuralCurveMember (analytical representation) with optional cross-section.
        /// </summary>
        private XmiStructuralCurveMember CreateStructuralCurveMember(
            string id,
            string name,
            string ifcGuid,
            string nativeId,
            XmiStorey storey,
            bool isColumn,
            XmiStructuralPointConnection startConnection,
            XmiStructuralPointConnection endConnection,
            Curve curve,
            XmiCrossSection? crossSection = null)
        {
            // Determine member type
            XmiStructuralCurveMemberTypeEnum memberType = isColumn
                ? XmiStructuralCurveMemberTypeEnum.Column
                : XmiStructuralCurveMemberTypeEnum.Beam;

            // Prepare node list
            List<XmiStructuralPointConnection> nodes = new List<XmiStructuralPointConnection>
            {
                startConnection,
                endConnection
            };

            // Calculate curve length (in Revit internal units, convert to mm)
            double lengthMm = Converters.ConvertValueToMillimeter(curve.Length);

            // Default axis values (no rotation)
            string localAxisX = "1,0,0";
            string localAxisY = "0,1,0";
            string localAxisZ = "0,0,1";

            XmiStructuralCurveMember member = _model.CreateStructuralCurveMember(
                id,
                name,
                ifcGuid,
                nativeId,
                string.Empty,    // description
                crossSection,    // crossSection (optional)
                storey,          // storey (can be null)
                memberType,
                nodes,
                null,            // segments (deferred)
                XmiSystemLineEnum.TopMiddle,  // default system line
                startConnection,
                endConnection,
                lengthMm,
                localAxisX,
                localAxisY,
                localAxisZ,
                0, 0, 0, 0, 0, 0,  // offset parameters (all zero)
                string.Empty,      // endFixityStart
                string.Empty       // endFixityEnd
            );

            return member;
        }

        /// <summary>
        /// Creates physical element (XmiBeam or XmiColumn) and relationships to Point3D geometry and cross-section.
        /// Uses description field to indicate "startNode" or "endNode" for point relationships.
        /// </summary>
        private void CreatePhysicalElement(
            Document doc,
            FamilyInstance familyInstance,
            string id,
            string name,
            string ifcGuid,
            string nativeId,
            bool isColumn,
            Curve curve,
            XmiPoint3D startPoint,
            XmiPoint3D endPoint,
            out XmiBasePhysicalEntity physicalEntity)
        {
            // Calculate curve length and axis values
            double lengthMm = Converters.ConvertValueToMillimeter(curve.Length);
            string localAxisX = "1,0,0";
            string localAxisY = "0,1,0";
            string localAxisZ = "0,0,1";

            if (isColumn)
            {
                XmiColumn column = new XmiColumn(
                    id,
                    name,
                    ifcGuid,
                    nativeId,
                    string.Empty,  // description
                    XmiSystemLineEnum.MiddleMiddle,
                    lengthMm,
                    localAxisX,
                    localAxisY,
                    localAxisZ,
                    0, 0, 0, 0, 0, 0  // all offsets zero
                );

                _model.AddXmiColumn(column);
                physicalEntity = column;
            }
            else
            {
                XmiBeam beam = new XmiBeam(
                    id,
                    name,
                    ifcGuid,
                    nativeId,
                    string.Empty,  // description
                    XmiSystemLineEnum.TopMiddle,
                    lengthMm,
                    localAxisX,
                    localAxisY,
                    localAxisZ,
                    0, 0, 0, 0, 0, 0  // all offsets zero
                );

                _model.AddXmiBeam(beam);
                physicalEntity = beam;
            }

            // Create relationships: Physical Element → Point3D
            // Note: Using description field to store pointType ("startNode"/"endNode")
            // until XmiHasPoint3D gets a dedicated pointType property
            XmiHasPoint3D startPointRel = new XmiHasPoint3D(physicalEntity, startPoint);
            startPointRel.Description = "startNode";
            _model.AddXmiHasPoint3D(startPointRel);

            XmiHasPoint3D endPointRel = new XmiHasPoint3D(physicalEntity, endPoint);
            endPointRel.Description = "endNode";
            _model.AddXmiHasPoint3D(endPointRel);

            // Create cross-section and link via XmiHasCrossSection relationship
            XmiCrossSection? crossSection = GetOrCreateCrossSection(doc, familyInstance);
            if (crossSection != null)
            {
                XmiHasCrossSection hasCrossSection = new XmiHasCrossSection(physicalEntity, crossSection);
                _model.AddXmiHasCrossSection(hasCrossSection);
            }
        }

        /// <summary>
        /// Creates relationship linking physical element to its analytical representation.
        /// </summary>
        private void CreatePhysicalToAnalyticalRelationship(
            XmiBasePhysicalEntity physicalEntity,
            XmiStructuralCurveMember analyticalMember)
        {
            // Cast to base types for relationship
            var physicalBase = physicalEntity;
            var analyticalBase = analyticalMember as XmiBaseStructuralAnalyticalEntity;

            if (physicalBase != null && analyticalBase != null)
            {
                XmiHasStructuralCurveMember relationship = new XmiHasStructuralCurveMember(
                    physicalBase,
                    analyticalBase
                );

                _model.AddXmiHasStructuralCurveMember(relationship);
            }
        }

        /// <summary>
        /// Gets the associated analytical element ID from a physical element using Revit 2023+ API.
        /// Returns null if no analytical association exists.
        /// </summary>
        /// <param name="doc">Revit document</param>
        /// <param name="physicalElement">Physical structural element (beam or column)</param>
        /// <returns>Analytical element ID as string, or null if no analytical model exists</returns>
        private string? GetAnalyticalElementId(Document doc, Element physicalElement)
        {
            try
            {
                AnalyticalToPhysicalAssociationManager manager =
                    AnalyticalToPhysicalAssociationManager.GetAnalyticalToPhysicalAssociationManager(doc);

                if (manager != null)
                {
                    ElementId analyticalElementId = manager.GetAssociatedElementId(physicalElement.Id);

                    if (analyticalElementId != null && analyticalElementId != ElementId.InvalidElementId)
                    {
                        return analyticalElementId.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Failed to get analytical element ID for {physicalElement.Id}: {ex.Message}");
            }

            // Return null if no analytical association exists
            return null;
        }

        /// <summary>
        /// Gets or creates a deduplicated XmiMaterial from a Revit Material.
        /// Uses Revit Material ElementId as cache key.
        /// </summary>
        private XmiMaterial? GetOrCreateMaterial(Document doc, ElementId materialId)
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
            XmiMaterial xmiMaterial = _model.CreateMaterial(
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
        /// Gets or creates a deduplicated XmiCrossSection from a Revit FamilySymbol (Type).
        /// Uses Revit FamilySymbol ElementId as cache key.
        /// </summary>
        private XmiCrossSection? GetOrCreateCrossSection(Document doc, FamilyInstance familyInstance)
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
            ElementId materialId = familyInstance.StructuralMaterialId;
            if (materialId == null || materialId == ElementId.InvalidElementId)
            {
                // Try to get material from type
                materialId = familySymbol.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM)?.AsElementId();
            }

            XmiMaterial? material = GetOrCreateMaterial(doc, materialId);

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
            XmiCrossSection xmiCrossSection = _model.CreateCrossSection(
                id,
                name,
                string.Empty,  // ifcGuid (cross-sections don't have IFC GUIDs from Revit)
                nativeId,
                string.Empty,  // description
                material,      // optional material reference
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
                // Collect all analytical curve members in the model
                // In Revit 2023+, analytical elements are in their own categories
                FilteredElementCollector analyticalCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(AnalyticalMember))
                    .WhereElementIsNotElementType();

                foreach (Element element in analyticalCollector)
                {
                    AnalyticalMember analyticalMember = element as AnalyticalMember;
                    if (analyticalMember == null)
                        continue;

                    string analyticalNativeId = analyticalMember.Id.ToString();

                    // Get the analytical curve
                    Curve curve = analyticalMember.GetCurve();
                    if (curve == null)
                        continue;

                    XYZ startPoint = curve.GetEndPoint(0);
                    XYZ endPoint = curve.GetEndPoint(1);

                    // Get element name
                    string name = analyticalMember.Name ?? $"Analytical_{analyticalNativeId}";

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
                    XmiPoint3D startXmiPoint = GetOrCreatePoint3D(startPoint, $"{analyticalId}_start_point");
                    XmiPoint3D endXmiPoint = GetOrCreatePoint3D(endPoint, $"{analyticalId}_end_point");

                    // Create StructuralPointConnections for analytical domain
                    XmiStructuralPointConnection startConnection = GetOrCreatePointConnection(
                        startPoint,
                        $"{analyticalId}_start_connection",
                        $"{name}_start",
                        $"{analyticalNativeId}_start",
                        storey,
                        startXmiPoint);

                    XmiStructuralPointConnection endConnection = GetOrCreatePointConnection(
                        endPoint,
                        $"{analyticalId}_end_connection",
                        $"{name}_end",
                        $"{analyticalNativeId}_end",
                        storey,
                        endXmiPoint);

                    // Extract Section Type property from analytical member and create cross-section
                    XmiCrossSection? crossSection = GetOrCreateCrossSectionFromAnalyticalMember(doc, analyticalMember);

                    // Create analytical representation (XmiStructuralCurveMember)
                    XmiStructuralCurveMember xmiAnalyticalMember = CreateStructuralCurveMember(
                        analyticalId,
                        name,
                        ifcGuid,
                        analyticalNativeId,  // Use analytical element's NativeId
                        storey,
                        isColumn,
                        startConnection,
                        endConnection,
                        curve,
                        crossSection);  // Pass cross-section (optional)

                    // Store in cache for later lookup by physical elements
                    _analyticalMemberCache[analyticalNativeId] = xmiAnalyticalMember;
                }
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
        private XmiCrossSection? GetOrCreateCrossSectionFromAnalyticalMember(Document doc, AnalyticalMember analyticalMember)
        {
            try
            {
                // Try to get the Section Type parameter from the analytical member
                // In Revit's analytical model, this is typically stored as a parameter
                Parameter sectionTypeParam = analyticalMember.LookupParameter("Section Type");

                if (sectionTypeParam == null || !sectionTypeParam.HasValue)
                {
                    // No section type assigned - return placeholder cross-section
                    // This is a temporary workaround until XmiSchema.Core supports nullable cross-sections
                    return GetOrCreatePlaceholderCrossSection();
                }

                // Get the element ID of the section type
                ElementId sectionTypeId = sectionTypeParam.AsElementId();
                if (sectionTypeId == null || sectionTypeId == ElementId.InvalidElementId)
                {
                    // Invalid section type - return placeholder cross-section
                    return GetOrCreatePlaceholderCrossSection();
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
                    material = GetOrCreateMaterial(doc, materialId);
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
                XmiCrossSection xmiCrossSection = _model.CreateCrossSection(
                    id,
                    name,
                    string.Empty,  // ifcGuid (sections don't have IFC GUIDs)
                    nativeId,
                    string.Empty,  // description
                    material,      // optional material reference
                    shape,
                    parameters,
                    area,
                    0, 0, 0, 0, 0, 0, 0, 0, 0  // section properties (deferred for now)
                );

                _crossSectionCache[cacheKey] = xmiCrossSection;
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

        /// <summary>
        /// Gets or creates a placeholder cross-section for analytical members without section types assigned.
        /// This is a temporary workaround until XmiSchema.Core supports nullable cross-sections.
        /// Creates a single shared placeholder instance to avoid polluting the model with duplicates.
        /// </summary>
        private XmiCrossSection GetOrCreatePlaceholderCrossSection()
        {
            // Return cached instance if it exists
            if (_placeholderCrossSection != null)
            {
                return _placeholderCrossSection;
            }

            // Create placeholder cross-section with clearly identifiable properties
            string id = "placeholder-no-section-type";
            string name = "[PLACEHOLDER] No Section Type Assigned";
            string nativeId = "synthetic:placeholder:no-section-type";
            string description = "TEMPORARY PLACEHOLDER: This analytical member does not have a section type assigned in Revit. " +
                                 "This placeholder exists because XmiSchema.Core v0.9.1 requires non-nullable cross-sections. " +
                                 "Once the library is upgraded to support nullable cross-sections, this placeholder will be removed.";

            // Use Unknown shape with empty parameters
            var unknownParams = new UnknownShapeParameters(new Dictionary<string, double>());

            // Create the placeholder cross-section
            _placeholderCrossSection = _model.CreateCrossSection(
                id,
                name,
                string.Empty,  // ifcGuid
                nativeId,
                description,
                null,          // material (no material for placeholder)
                XmiShapeEnum.Unknown,
                unknownParams,
                0,  // area
                0, 0, 0, 0, 0, 0, 0, 0, 0  // all section properties zero
            );

            return _placeholderCrossSection;
        }
    }
}
