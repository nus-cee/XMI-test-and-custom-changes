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
    /// Partial class containing Revit element processing methods.
    /// Handles extraction of structural elements (beams, columns, floors, walls, storeys).
    /// </summary>
    public partial class BetekkXmiBuilder
    {
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

                        XmiStorey storey = _model.CreateXmiStorey(
                            id,
                            name,
                            ifcGuid,
                            nativeId,
                            string.Empty,  // description
                            elevationMm,
                            0.0            // mass
                        );

                        _storeyCache[nativeId] = storey;
                        _storeyCount++;
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
                        ProcessSingleBeamElement(doc, familyInstance);
                    }
                    catch (Exception ex)
                    {
                        string elementId = familyInstance.Id?.ToString() ?? "unknown";
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"[BetekkXmiBuilder] Failed to process beam element {elementId}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Processes structural columns separately using the OST_StructuralColumns category.
        /// Keeps the column logic aligned with the Revit 2026 API expectations.
        /// </summary>
        private void ProcessStructuralColumnsElements(Document doc)
        {
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
                        ProcessSingleColumnElement(doc, familyInstance);
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

        private void ProcessFloorMaterials(Document doc)
        {
            FilteredElementCollector floorCollector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType();

            foreach (Element element in floorCollector)
            {
                try
                {
                    ExportMaterialsForElement(doc, element);
                }
                catch (Exception ex)
                {
                    string elementId = element?.Id?.ToString() ?? "unknown";
                    ModelInfoBuilder.WriteErrorLogToFile(
                        $"[BetekkXmiBuilder] Failed to process floor element {elementId} for materials: {ex.Message}");
                }
            }
        }

        private void ProcessWallMaterials(Document doc)
        {
            FilteredElementCollector wallCollector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType();

            foreach (Element element in wallCollector)
            {
                try
                {
                    ExportMaterialsForElement(doc, element);
                }
                catch (Exception ex)
                {
                    string elementId = element?.Id?.ToString() ?? "unknown";
                    ModelInfoBuilder.WriteErrorLogToFile(
                        $"[BetekkXmiBuilder] Failed to process wall element {elementId} for materials: {ex.Message}");
                }
            }
        }

        private void ProcessWallElements(Document doc)
        {
            FilteredElementCollector wallCollector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall))
                .WhereElementIsNotElementType();

            foreach (Element element in wallCollector)
            {
                if (element is Wall wall)
                {
                    try
                    {
                        ProcessSingleWallElement(doc, wall);
                    }
                    catch (Exception ex)
                    {
                        string elementId = wall.Id?.ToString() ?? "unknown";
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"[BetekkXmiBuilder] Failed to process wall element {elementId}: {ex.Message}");
                    }
                }
            }
        }

        private void ProcessSingleWallElement(Document doc, Wall wall)
        {
            string id = Guid.NewGuid().ToString();
            string name = wall.Name ?? id;
            string ifcGuid = GetIfcGuidFromElement(wall);
            string nativeId = wall.Id.ToString();

            ExportMaterialsForElement(doc, wall);

            if (wall.Location is not LocationCurve locationCurve || locationCurve.Curve == null)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Wall {nativeId} has unsupported location type {wall.Location?.GetType().Name ?? "null"}");
                return;
            }

            Curve curve = locationCurve.Curve;
            XYZ startPoint = curve.GetEndPoint(0);
            XYZ endPoint = curve.GetEndPoint(1);

            XmiPoint3d startXmiPoint = GetOrReusePoint3D(startPoint, $"{id}_start_point");
            XmiPoint3d endXmiPoint = GetOrReusePoint3D(endPoint, $"{id}_end_point");

            List<XmiSegment> segments = BuildSegmentsFromCurve(curve, startXmiPoint, endXmiPoint, nativeId, name);

            XmiMaterial? physicalMaterial = GetOrCreateXmiMaterial(doc, GetMaterialIdFromWall(wall));

            double baseOffsetFeet = wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0.0;
            double zOffsetMm = Converters.ConvertValueToMillimeter(baseOffsetFeet);

            double heightFeet = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 0.0;
            if (heightFeet <= 0)
            {
                BoundingBoxXYZ? bbox = wall.get_BoundingBox(null);
                if (bbox != null)
                {
                    heightFeet = Math.Max(0.0, bbox.Max.Z - bbox.Min.Z);
                }
            }

            double heightMm = Converters.ConvertValueToMillimeter(heightFeet);
            XmiAxis localAxisX = new XmiAxis(1, 0, 0);
            XmiAxis localAxisY = new XmiAxis(0, 1, 0);
            XmiAxis localAxisZ = new XmiAxis(0, 0, 1);

            XmiWall xmiWall = _model.CreateXmiWall(
                id,
                name,
                ifcGuid,
                nativeId,
                string.Empty,
                physicalMaterial,
                segments,
                zOffsetMm,
                localAxisX,
                localAxisY,
                localAxisZ,
                heightMm);

            _wallCache[nativeId] = xmiWall;
            _wallCount++;

            _model.AddXmiHasPoint3d(new XmiHasPoint3d(xmiWall, startXmiPoint, XmiPoint3dTypeEnum.Start));
            _model.AddXmiHasPoint3d(new XmiHasPoint3d(xmiWall, endXmiPoint, XmiPoint3dTypeEnum.End));
        }

        /// <summary>
        /// Process a single structural framing element (beam):
        /// - Extract geometry from LocationCurve
        /// - Create dual representation (physical + analytical)
        /// </summary>
        /// <param name="doc">Active Revit document</param>
        /// <param name="familyInstance">Structural beam element to process</param>
        private void ProcessSingleBeamElement(Document doc, FamilyInstance familyInstance)
        {
            // Extract basic properties
            string id = Guid.NewGuid().ToString();
            string name = familyInstance.Name ?? id;
            string ifcGuid = GetIfcGuidFromElement(familyInstance);
            string nativeId = familyInstance.Id.ToString();

            // Export material attached to this element to ensure it appears in the material list
            ExportMaterialsForElement(doc, familyInstance);

            // Get geometry from Location; columns can be point- or curve-based
            Location location = familyInstance.Location;
            Curve curve = null;
            // Try robust geometry extraction first (works even without LocationCurve)
            if (TryGetColumnEndPoints(familyInstance, out XYZ axisStart, out XYZ axisEnd))
            {
                curve = Line.CreateBound(axisStart, axisEnd);
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Column {nativeId}: derived axis from solid vertices.");
            }
            else if (location is LocationCurve locationCurve)
            {
                curve = locationCurve.Curve;
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Column {nativeId}: using LocationCurve for geometry.");
            }
            else if (TryGetColumnAxisFromGeometry(familyInstance, out Line axisFromGeom))
            {
                curve = axisFromGeom;
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Column {nativeId}: derived axis from solid geometry.");
            }
            else if (location is LocationPoint locationPoint)
            {
                // Some columns store only a point; build a vertical line using the bounding box extents
                BoundingBoxXYZ bbox = familyInstance.get_BoundingBox(null);
                if (bbox == null)
                {
                    ModelInfoBuilder.WriteErrorLogToFile(
                        $"[BetekkXmiBuilder] Element {nativeId} has LocationPoint but no bounding box");
                    return;
                }

                XYZ basePoint = new XYZ(locationPoint.Point.X, locationPoint.Point.Y, bbox.Min.Z);
                XYZ topPoint = new XYZ(locationPoint.Point.X, locationPoint.Point.Y, bbox.Max.Z);
                curve = Line.CreateBound(basePoint, topPoint);
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Column {nativeId}: fell back to bounding-box vertical line.");
            }
            else
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Element {nativeId} has unsupported Location type {location?.GetType().Name ?? "null"}");
                return;
            }

            if (curve == null)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Element {nativeId} Location has no Curve after all fallbacks");
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

            // Get analytical element ID using the new Revit 2023+ API (returns null if no association exists)
            string? analyticalNativeId = GetAnalyticalElementId(doc, familyInstance);

            // Generate separate IDs for physical and analytical entities
            string physicalId = Guid.NewGuid().ToString();

            // Create deduplicated Point3D entities using schema equality
            XmiPoint3d startXmiPoint = GetOrReusePoint3D(startPoint, $"{physicalId}_start_point");
            XmiPoint3d endXmiPoint = GetOrReusePoint3D(endPoint, $"{physicalId}_end_point");

            // Create physical entity (XmiBeam)
            double lengthMm = Converters.ConvertValueToMillimeter(curve.Length);
            XmiAxis localAxisX = new XmiAxis(1, 0, 0);
            XmiAxis localAxisY = new XmiAxis(0, 1, 0);
            XmiAxis localAxisZ = new XmiAxis(0, 0, 1);

            // Material (optional) pulled from instance
            XmiMaterial? physicalMaterial = GetOrCreateXmiMaterial(doc, GetMaterialIdFromFamilyInstance(familyInstance));

            XmiBeam beam = _model.CreateXmiBeam(
                physicalId,
                name,
                ifcGuid,
                nativeId,
                string.Empty,
                physicalMaterial,
                null, // segments not generated for physical beams
                XmiSystemLineEnum.TopMiddle,
                lengthMm,
                localAxisX,
                localAxisY,
                localAxisZ,
                0, 0, 0, 0, 0, 0);

            _beamCount++;
            _beamCache[nativeId] = beam;

            // Point relationships
            _model.AddXmiHasPoint3d(new XmiHasPoint3d(beam, startXmiPoint, XmiPoint3dTypeEnum.Start));
            _model.AddXmiHasPoint3d(new XmiHasPoint3d(beam, endXmiPoint, XmiPoint3dTypeEnum.End));

            // Cross-section relationship
            XmiCrossSection? crossSection = GetOrCreateXmiCrossSection(doc, familyInstance);
            if (crossSection != null)
            {
                _model.AddXmiHasCrossSection(new XmiHasCrossSection(beam, crossSection));
            }

            // Defer physical→analytical linkage
            if (!string.IsNullOrEmpty(analyticalNativeId))
            {
                _beamToAnalyticalLinks.Add((beam, analyticalNativeId));
            }
        }

        /// <summary>
        /// Process a single structural column element:
        /// - Extract geometry from LocationCurve
        /// - Create dual representation (physical + analytical)
        /// </summary>
        /// <param name="doc">Active Revit document</param>
        /// <param name="familyInstance">Structural column element to process</param>
        private void ProcessSingleColumnElement(Document doc, FamilyInstance familyInstance)
        {
            // Extract basic properties
            string id = Guid.NewGuid().ToString();
            string name = familyInstance.Name ?? id;
            string ifcGuid = GetIfcGuidFromElement(familyInstance);
            string nativeId = familyInstance.Id.ToString();

            // Export material attached to this element to ensure it appears in the material list
            ExportMaterialsForElement(doc, familyInstance);

            // Get geometry from Location; columns are point-based so use level params or fallbacks
            Location location = familyInstance.Location;
            Curve curve = null;
            //if (TryGetColumnCurveFromParameters(doc, familyInstance, out Line levelBasedLine))
            //{
            //    curve = levelBasedLine;
            //    ModelInfoBuilder.WriteErrorLogToFile(
            //        $"[BetekkXmiBuilder] Column {nativeId}: built axis from base/top level parameters.");
            //}
            //else if (TryGetColumnEndPoints(familyInstance, out XYZ axisStart, out XYZ axisEnd))
            //{
            //    curve = Line.CreateBound(axisStart, axisEnd);
            //    ModelInfoBuilder.WriteErrorLogToFile(
            //        $"[BetekkXmiBuilder] Column {nativeId}: derived axis from solid vertices.");
            //}
            //else if (TryGetColumnAxisFromGeometry(familyInstance, out Line axisFromGeom))
            //{
            //    curve = axisFromGeom;
            //    ModelInfoBuilder.WriteErrorLogToFile(
            //        $"[BetekkXmiBuilder] Column {nativeId}: derived axis from solid geometry.");
            //}
            if (location is LocationPoint locationPoint)
            {
                // Build a vertical line using the bounding box extents around the location point
                BoundingBoxXYZ bbox = familyInstance.get_BoundingBox(null);
                if (bbox == null)
                {
                    ModelInfoBuilder.WriteErrorLogToFile(
                        $"[BetekkXmiBuilder] Element {nativeId} has LocationPoint but no bounding box");
                    return;
                }

                XYZ basePoint = new XYZ(locationPoint.Point.X, locationPoint.Point.Y, bbox.Min.Z);
                XYZ topPoint = new XYZ(locationPoint.Point.X, locationPoint.Point.Y, bbox.Max.Z);
                curve = Line.CreateBound(basePoint, topPoint);
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Column {nativeId}: fell back to bounding-box vertical line.");
            }
            else
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Element {nativeId} has unsupported Location type {location?.GetType().Name ?? "null"}");
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

            // Generate separate IDs for physical and analytical entities
            string physicalId = Guid.NewGuid().ToString();

            // Create deduplicated Point3D entities using schema equality
            XmiPoint3d startXmiPoint = GetOrReusePoint3D(startPoint, $"{physicalId}_start_point");
            XmiPoint3d endXmiPoint = GetOrReusePoint3D(endPoint, $"{physicalId}_end_point");

            double lengthMm = Converters.ConvertValueToMillimeter(curve.Length);
            XmiAxis localAxisX = new XmiAxis(1, 0, 0);
            XmiAxis localAxisY = new XmiAxis(0, 1, 0);
            XmiAxis localAxisZ = new XmiAxis(0, 0, 1);

            XmiMaterial? physicalMaterial = GetOrCreateXmiMaterial(doc, GetMaterialIdFromFamilyInstance(familyInstance));

            List<XmiSegment> segments = BuildSegmentsFromCurve(curve, startXmiPoint, endXmiPoint, nativeId, name);

            XmiColumn column = _model.CreateXmiColumn(
                physicalId,
                name,
                ifcGuid,
                nativeId,
                string.Empty,
                physicalMaterial,
                segments,
                XmiSystemLineEnum.MiddleMiddle,
                lengthMm,
                localAxisX,
                localAxisY,
                localAxisZ,
                0, 0, 0, 0, 0, 0);

            _columnCount++;
            _columnCache[nativeId] = column;

            _model.AddXmiHasPoint3d(new XmiHasPoint3d(column, startXmiPoint, XmiPoint3dTypeEnum.Start));
            _model.AddXmiHasPoint3d(new XmiHasPoint3d(column, endXmiPoint, XmiPoint3dTypeEnum.End));

            XmiCrossSection? crossSection = GetOrCreateXmiCrossSection(doc, familyInstance);
            if (crossSection != null)
            {
                _model.AddXmiHasCrossSection(new XmiHasCrossSection(column, crossSection));
            }
            
            // Get analytical element ID using the new Revit 2023+ API (returns null if no association exists)
            string? analyticalNativeId = GetAnalyticalElementId(doc, familyInstance);

            // Defer physical→analytical linkage
            if (!string.IsNullOrEmpty(analyticalNativeId))
            {
                _columnToAnalyticalLinks.Add((column, analyticalNativeId));
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
    }
}
