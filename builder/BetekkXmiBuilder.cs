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

namespace Betekk.RevitXmiExporter.Builder
{
    /// <summary>
    /// Coordinates extraction of structural framing elements (beams, columns) from Revit
    /// and builds dual-representation XMI model (physical + analytical domains).
    /// Phase 1: Geometry only - no cross-sections or materials.
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
        }

        /// <summary>
        /// Main orchestration method that processes Revit document and builds XMI model.
        /// </summary>
        /// <param name="doc">Active Revit document to inspect.</param>
        public void BuildModel(Document doc)
        {
            // Phase 1: Process levels/storeys
            ProcessStoreys(doc);

            // Phase 2: Process structural framing elements (beams and columns)
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
                physicalId,
                name,
                ifcGuid,
                nativeId,
                isColumn,
                curve,
                startXmiPoint,
                endXmiPoint,
                out XmiBasePhysicalEntity physicalEntity);

            // Only create analytical representation if an analytical element exists in Revit
            if (!string.IsNullOrEmpty(analyticalNativeId))
            {
                string analyticalId = Guid.NewGuid().ToString();

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

                // Create analytical representation (XmiStructuralCurveMember)
                XmiStructuralCurveMember analyticalMember = CreateStructuralCurveMember(
                    analyticalId,
                    name,
                    ifcGuid,
                    analyticalNativeId,  // Use analytical element's NativeId from Revit
                    storey,
                    isColumn,
                    startConnection,
                    endConnection,
                    curve);

                // Create relationship: Physical Element → Analytical Member
                CreatePhysicalToAnalyticalRelationship(physicalEntity, analyticalMember);
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
        /// Creates XmiStructuralCurveMember (analytical representation).
        /// Phase 1: Simplified - no cross-section, no segments, basic properties only.
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
            Curve curve)
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
                null,            // crossSection (Phase 1: deferred)
                storey,          // storey (can be null)
                memberType,
                nodes,
                null,            // segments (Phase 1: deferred)
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
        /// Creates physical element (XmiBeam or XmiColumn) and relationships to Point3D geometry.
        /// Uses description field to indicate "startNode" or "endNode" for point relationships.
        /// </summary>
        private void CreatePhysicalElement(
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
    }
}
