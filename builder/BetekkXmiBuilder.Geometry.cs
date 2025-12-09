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
    /// Partial class containing geometry processing methods.
    /// Handles points, lines, arcs, segments, connections, and curve members.
    /// </summary>
    public partial class BetekkXmiBuilder
    {
        /// <summary>
        /// Gets or creates a deduplicated XmiPoint3d.
        /// Uses coordinate-based key with tolerance of 1e-10 mm.
        /// </summary>
        private XmiPoint3d GetOrCreatePoint3D(XYZ revitPoint, string fallbackId)
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
            if (_pointCache.TryGetValue(key, out XmiPoint3d existingPoint))
            {
                return existingPoint;
            }

            // Create new point
            string id = Guid.NewGuid().ToString();
            string name = fallbackId ?? id;

            XmiPoint3d newPoint = _model.CreateXmiPoint3d(
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
            _pointCount++;
            return newPoint;
        }

        private XmiPoint3d GetOrReusePoint3D(XYZ revitPoint, string fallbackId)
        {
            // Convert to millimeters
            double x = Converters.ConvertValueToMillimeter(revitPoint.X);
            double y = Converters.ConvertValueToMillimeter(revitPoint.Y);
            double z = Converters.ConvertValueToMillimeter(revitPoint.Z);

            // First try to reuse any existing point using schema equality
            foreach (XmiPoint3d existing in _model.Entities.OfType<XmiPoint3d>())
            {
                if (existing.Equals(new XmiPoint3d(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, x, y, z)))
                {
                    return existing;
                }
            }

            // Fallback to cache/tolerance-based creation
            return GetOrCreatePoint3D(revitPoint, fallbackId);
        }

        /// <summary>
        /// Gets or creates a deduplicated XmiStructuralPointConnection.
        /// Reuses existing connection at same coordinate.
        /// </summary>
        private XmiStructuralPointConnection GetOrCreateXmiStructuralPointConnection(
            XYZ revitPoint,
            string fallbackId,
            string fallbackName,
            string nativeId,
            XmiStorey storey,
            XmiPoint3d point)
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

            XmiStructuralPointConnection connection = _model.CreateXmiStructurePointConnection(
                id,
                fallbackName ?? id,
                string.Empty, // ifcGuid (empty - synthetic analytical node, not a Revit element)
                $"synthetic:connection:{nativeId}",  // nativeId (synthetic - analytical node, not a Revit element)
                string.Empty, // description
                storey,       // XmiStorey (can be null)
                point         // XmiPoint3d
            );

            _connectionCache[key] = connection;
            _connectionCount++;
            return connection;
        }

        /// <summary>
        /// Creates XmiStructuralCurveMember (analytical representation) with optional cross-section.
        /// </summary>
        private XmiStructuralCurveMember CreateXmiStructuralCurveMember(
            string id,
            string name,
            string ifcGuid,
            string nativeId,
            XmiStorey storey,
            bool isColumn,
            XmiStructuralPointConnection startConnection,
            XmiStructuralPointConnection endConnection,
            XmiPoint3d startPoint,
            XmiPoint3d endPoint,
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

            // Default axis values (no rotation) - use XmiAxis (unit vectors)
            XmiAxis localAxisX = new XmiAxis(1, 0, 0);
            XmiAxis localAxisY = new XmiAxis(0, 1, 0);
            XmiAxis localAxisZ = new XmiAxis(0, 0, 1);

            XmiMaterial? material = null;
            List<XmiSegment> segments = BuildSegmentsFromCurve(curve, startPoint, endPoint, nativeId, name);

            XmiStructuralCurveMember member = _model.CreateXmiStructuralCurveMember(
                id,
                name,
                ifcGuid,
                nativeId,
                string.Empty,    // description
                material,        // material (optional)
                crossSection,
                storey,          // storey (can be null)
                memberType,
                nodes,
                segments,
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

            if (crossSection != null)
            {
                XmiHasCrossSection hasCrossSection = new XmiHasCrossSection(member, crossSection);
                _model.AddXmiHasCrossSection(hasCrossSection);
            }

            return member;
        }

        private List<XmiSegment> BuildSegmentsFromCurve(
            Curve curve,
            XmiPoint3d startPoint,
            XmiPoint3d endPoint,
            string nativeId,
            string name)
        {
            List<XmiSegment> segments = new List<XmiSegment>();

            if (curve is Line)
            {
                XmiLine3d line = GetOrCreateLine3d(startPoint, endPoint, nativeId, name);
                XmiSegment segment = GetOrCreateSegment(nativeId, name, XmiSegmentTypeEnum.Line);
                AddLineRelationshipIfMissing(segment, line);
                segments.Add(segment);
            }
            else if (curve is Arc arc)
            {
                XYZ center = arc.Center;
                XmiPoint3d centerPoint = GetOrReusePoint3D(center, $"{nativeId}_center_point");
                float radiusMm = (float)Converters.ConvertValueToMillimeter(arc.Radius);

                XmiArc3d arc3d = GetOrCreateArc3d(startPoint, endPoint, centerPoint, radiusMm, nativeId, name);
                XmiSegment segment = GetOrCreateSegment(nativeId, name, XmiSegmentTypeEnum.CircularArc);
                AddArcRelationshipIfMissing(segment, arc3d);
                segments.Add(segment);
            }
            else
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"Unsupported curve type '{curve.GetType().Name}' for element {nativeId} ({name}). Segment geometry will not be exported.");
            }

            return segments;
        }

        private XmiLine3d GetOrCreateLine3d(
            XmiPoint3d startPoint,
            XmiPoint3d endPoint,
            string nativeId,
            string name)
        {
            // Log entry point with element details
            ModelInfoBuilder.WriteErrorLogToFile(
                $"[GetOrCreateLine3d] Called for element: nativeId='{nativeId}', name='{name}'");
            ModelInfoBuilder.WriteErrorLogToFile(
                $"[GetOrCreateLine3d]   Candidate StartPoint: ({startPoint.X:F6}, {startPoint.Y:F6}, {startPoint.Z:F6}) ID={startPoint.Id}");
            ModelInfoBuilder.WriteErrorLogToFile(
                $"[GetOrCreateLine3d]   Candidate EndPoint:   ({endPoint.X:F6}, {endPoint.Y:F6}, {endPoint.Z:F6}) ID={endPoint.Id}");

            // Create a candidate line to compare against existing lines
            XmiLine3d candidateLine = _model.CreateXmiLine3d(
                Guid.NewGuid().ToString(),
                $"{name}_line",
                string.Empty,
                $"{nativeId}_line",
                string.Empty,
                startPoint,
                endPoint);

            // Get all existing lines for comparison
            var existingLines = _model.Entities.OfType<XmiLine3d>().ToList();
            ModelInfoBuilder.WriteErrorLogToFile(
                $"[GetOrCreateLine3d]   Found {existingLines.Count} existing XmiLine3d entities in model");

            // Use XmiLine3d.IsCoincident() to check if an equivalent line already exists
            // IsCoincident() checks if lines occupy the same space regardless of direction (A→B equals B→A)
            XmiLine3d? existingLine = null;
            for (int i = 0; i < existingLines.Count; i++)
            {
                var line = existingLines[i];
                bool isCoincident = line.IsCoincident(candidateLine);

                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[GetOrCreateLine3d]   Comparing with existing line [{i}]: " +
                    $"ID={line.Id}, NativeId={line.NativeId}, Name={line.Name}");
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[GetOrCreateLine3d]     Existing StartPoint: ({line.StartPoint.X:F6}, {line.StartPoint.Y:F6}, {line.StartPoint.Z:F6}) ID={line.StartPoint.Id}");
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[GetOrCreateLine3d]     Existing EndPoint:   ({line.EndPoint.X:F6}, {line.EndPoint.Y:F6}, {line.EndPoint.Z:F6}) ID={line.EndPoint.Id}");
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[GetOrCreateLine3d]     IsCoincident result: {isCoincident}");

                if (isCoincident)
                {
                    existingLine = line;
                    ModelInfoBuilder.WriteErrorLogToFile(
                        $"[GetOrCreateLine3d]   ✓ MATCH FOUND! Reusing existing line ID={existingLine.Id}, NativeId={existingLine.NativeId}");
                    break;
                }
            }

            if (existingLine != null)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[GetOrCreateLine3d] RESULT: Reused existing XmiLine3d (ID={existingLine.Id}, NativeId={existingLine.NativeId})");
                return existingLine;
            }

            // No coincident line found, add the candidate to the model
            _model.AddXmiLine3d(candidateLine);
            ModelInfoBuilder.WriteErrorLogToFile(
                $"[GetOrCreateLine3d] RESULT: Created NEW XmiLine3d (ID={candidateLine.Id}, NativeId={candidateLine.NativeId})");
            ModelInfoBuilder.WriteErrorLogToFile(
                $"[GetOrCreateLine3d]   Total XmiLine3d count in model: {_model.Entities.OfType<XmiLine3d>().Count()}");
            ModelInfoBuilder.WriteErrorLogToFile(""); // Blank line for readability

            return candidateLine;
        }

        private XmiArc3d GetOrCreateArc3d(
            XmiPoint3d startPoint,
            XmiPoint3d endPoint,
            XmiPoint3d centerPoint,
            float radiusMm,
            string nativeId,
            string name)
        {
            // Create a candidate arc to compare against existing arcs
            XmiArc3d candidateArc = _model.CreateXmiArc3d(
                Guid.NewGuid().ToString(),
                $"{name}_arc",
                string.Empty,
                $"{nativeId}_arc",
                string.Empty,
                startPoint,
                endPoint,
                centerPoint,
                radiusMm);

            // Manual comparison since XmiArc3d doesn't have IsCoincident or Equals methods
            // Check if an arc with the same start, end, center points and radius already exists
            const float radiusTolerance = 0.001f; // 0.001mm tolerance for radius comparison
            XmiArc3d? existingArc = _model.Entities
                .OfType<XmiArc3d>()
                .FirstOrDefault(arc =>
                    arc.StartPoint.Equals(candidateArc.StartPoint) &&
                    arc.EndPoint.Equals(candidateArc.EndPoint) &&
                    arc.CenterPoint.Equals(candidateArc.CenterPoint) &&
                    Math.Abs(arc.Radius - candidateArc.Radius) < radiusTolerance);

            if (existingArc != null)
            {
                return existingArc;
            }

            // No matching arc found, add the candidate to the model
            _model.AddXmiArc3d(candidateArc);
            return candidateArc;
        }

        private XmiSegment GetOrCreateSegment(
            string nativeId,
            string name,
            XmiSegmentTypeEnum segmentType)
        {
            string key = $"segment:{nativeId}:{segmentType}";
            if (_segmentCache.TryGetValue(key, out XmiSegment existingSegment))
            {
                return existingSegment;
            }

            XmiSegment segment = new XmiSegment(
                Guid.NewGuid().ToString(),
                $"{name}_segment",
                string.Empty,
                key,
                string.Empty,
                0f,
                segmentType);

            _model.AddXmiSegment(segment);
            _segmentCache[key] = segment;
            return segment;
        }

        private void AddLineRelationshipIfMissing(XmiSegment segment, XmiLine3d line)
        {
            bool exists = _model.Relationships
                .OfType<XmiHasLine3d>()
                .Any(r => ReferenceEquals(r.Source, segment) && ReferenceEquals(r.Target, line));

            if (!exists)
            {
                _model.AddXmiHasLine3d(new XmiHasLine3d(segment, line));
            }
        }

        private void AddArcRelationshipIfMissing(XmiSegment segment, XmiArc3d arc)
        {
            bool exists = _model.Relationships
                .OfType<XmiHasArc3d>()
                .Any(r => ReferenceEquals(r.Source, segment) && ReferenceEquals(r.Target, arc));

            if (!exists)
            {
                _model.AddXmiHasArc3d(new XmiHasArc3d(segment, arc));
            }
        }

        private string FormatPointKey(XmiPoint3d point)
        {
            return $"{Math.Round(point.X, 10):F10}_{Math.Round(point.Y, 10):F10}_{Math.Round(point.Z, 10):F10}";
        }

    }
}
