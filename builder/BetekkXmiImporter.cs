using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Betekk.RevitXmiExporter.Utils;
using Newtonsoft.Json.Linq;

namespace Betekk.RevitXmiExporter.Builder
{
    /// <summary>
    /// Imports structural elements into a Revit document from an XMI graph JSON file
    /// containing nodes and edges arrays.
    /// </summary>
    public class BetekkXmiImporter
    {
        private const double MillimetersPerFoot = 304.8;

        private static readonly HashSet<string> SupportedEntities =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "XmiColumn",
                "XmiWall"
            };

        /// <summary>
        /// Imports structural elements from an XMI graph JSON string into the Revit document.
        /// The JSON must contain top-level "nodes" and "edges" arrays.
        /// </summary>
        /// <param name="doc">Active Revit document.</param>
        /// <param name="json">JSON content with nodes/edges graph structure.</param>
        /// <returns>Number of elements successfully created.</returns>
        public int Import(Document doc, string json)
        {
            return ImportWithDiagnostics(doc, json).CreatedCount;
        }

        public XmiImportResult ImportWithDiagnostics(Document doc, string json)
        {
            XmiImportPlan plan = XmiImportPlanner.BuildPlan(json, SupportedEntities);

            Dictionary<string, Func<JToken, XmiNodeImportResult>> handlers =
                new Dictionary<string, Func<JToken, XmiNodeImportResult>>(StringComparer.Ordinal)
            {
                ["XmiColumn"] = node =>
                {
                    CreateColumnFromGraph(doc, node, plan.NodeIndex, plan.Edges, _columnType!, _baseLevel!);
                    return XmiNodeImportResult.Created();
                },
                ["XmiWall"] = node =>
                {
                    CreateWallFromGraph(doc, node, plan.NodeIndex, plan.Edges, _wallType!, _baseLevel!);
                    return XmiNodeImportResult.Created();
                }
            };

            Func<JToken, XmiNodeImportResult> unsupportedHandler = node =>
            {
                string entityName = node["EntityName"]?.Value<string>() ?? "<missing>";
                string nodeId = node["Id"]?.Value<string>() ?? "<missing-id>";
                string nodeName = node["Name"]?.Value<string>() ?? "<unnamed>";

                return XmiNodeImportResult.Skipped(
                    $"Unsupported entity '{entityName}' (node Id='{nodeId}', Name='{nodeName}').");
            };

            int supportedNodeCount = plan.Nodes.Count(node =>
                handlers.ContainsKey(node["EntityName"]?.Value<string>() ?? string.Empty));

            if (supportedNodeCount == 0)
            {
                XmiImportNodeRouter.RouteNodes(plan.Nodes, plan.Diagnostics, handlers, unsupportedHandler);
                WriteDiagnosticsToErrorLog(plan.Diagnostics);
                return new XmiImportResult(plan.Diagnostics);
            }

            _columnType = FindStructuralColumnType(doc);
            _wallType = FindWallType(doc);
            _baseLevel = FindOrCreateBaseLevel(doc);

            using (Transaction tx = new Transaction(doc, "Import XMI Graph"))
            {
                tx.Start();

                if (!_columnType.IsActive)
                {
                    _columnType.Activate();
                    doc.Regenerate();
                }

                XmiImportNodeRouter.RouteNodes(plan.Nodes, plan.Diagnostics, handlers, unsupportedHandler);

                tx.Commit();
            }

            WriteDiagnosticsToErrorLog(plan.Diagnostics);
            return new XmiImportResult(plan.Diagnostics);
        }

        private FamilySymbol? _columnType;
        private WallType? _wallType;
        private Level? _baseLevel;

        /// <summary>
        /// Creates a single structural column by resolving its geometry and cross-section
        /// from the graph relationships.
        /// </summary>
        private static void CreateColumnFromGraph(
            Document doc,
            JToken columnNode,
            IReadOnlyDictionary<string, JToken> nodeIndex,
            IReadOnlyList<XmiGraphEdge> edges,
            FamilySymbol columnType,
            Level baseLevel)
        {
            string columnId = columnNode["Id"]!.Value<string>()!;

            // Resolve start/end points via XmiHasPoint3d edges from this column
            List<JToken> pointNodes = ResolveTargets(columnId, "XmiHasPoint3d",
                edges, nodeIndex, "XmiPoint3d");

            if (pointNodes.Count < 2)
            {
                throw new InvalidOperationException(
                    $"Column '{columnId}' needs at least 2 XmiPoint3d nodes, found {pointNodes.Count}.");
            }

            // Sort by Z to determine start (bottom) and end (top)
            pointNodes.Sort((a, b) =>
                (a["Z"]?.Value<double>() ?? 0).CompareTo(b["Z"]?.Value<double>() ?? 0));

            JToken startPt = pointNodes.First();
            JToken endPt = pointNodes.Last();

            double startX = startPt["X"]?.Value<double>() ?? 0;
            double startY = startPt["Y"]?.Value<double>() ?? 0;
            double startZ = startPt["Z"]?.Value<double>() ?? 0;
            double endZ = endPt["Z"]?.Value<double>() ?? 0;

            // Convert mm to feet
            XYZ location = new XYZ(
                startX / MillimetersPerFoot,
                startY / MillimetersPerFoot,
                startZ / MillimetersPerFoot);

            double heightFeet = (endZ - startZ) / MillimetersPerFoot;
            if (heightFeet <= 0)
            {
                throw new InvalidOperationException(
                    $"Column '{columnId}' has non-positive height ({heightFeet}).");
            }

            FamilyInstance column = doc.Create.NewFamilyInstance(
                location,
                columnType,
                baseLevel,
                StructuralType.Column);

            // Set robust offsets/height (do not misuse top offset as full height).
            double startOffsetFeet = (startZ / MillimetersPerFoot) - baseLevel.Elevation;
            double topOffsetFeet = startOffsetFeet + heightFeet;

            Parameter? baseOffsetParam =
                column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
            if (baseOffsetParam != null && !baseOffsetParam.IsReadOnly)
            {
                baseOffsetParam.Set(startOffsetFeet);
            }

            bool heightSet = SetParameterIfExists(column, BuiltInParameter.INSTANCE_LENGTH_PARAM, heightFeet);

            if (!heightSet)
            {
                Parameter? topLevelParam =
                    column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                if (topLevelParam != null && !topLevelParam.IsReadOnly)
                {
                    topLevelParam.Set(baseLevel.Id);
                }

                Parameter? topOffsetParam =
                    column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
                {
                    topOffsetParam.Set(topOffsetFeet);
                }
            }

            // Resolve cross-section via XmiHasCrossSection edge
            List<JToken> crossSections = ResolveTargets(columnId, "XmiHasCrossSection",
                edges, nodeIndex, "XmiCrossSection");

            if (crossSections.Count > 0)
            {
                JToken cs = crossSections[0];
                JToken? parameters = cs["Parameters"];

                double? width = parameters?["B"]?.Value<double>();
                double? height = parameters?["H"]?.Value<double>();

                if (width.HasValue)
                {
                    SetParameterIfExists(column, BuiltInParameter.FAMILY_WIDTH_PARAM,
                        width.Value / MillimetersPerFoot);
                }
                if (height.HasValue)
                {
                    SetParameterIfExists(column, BuiltInParameter.FAMILY_HEIGHT_PARAM,
                        height.Value / MillimetersPerFoot);
                }
            }
        }

        /// <summary>
        /// Creates a wall by resolving baseline points from XmiHasPoint3d graph relationships.
        /// </summary>
        private static void CreateWallFromGraph(
            Document doc,
            JToken wallNode,
            IReadOnlyDictionary<string, JToken> nodeIndex,
            IReadOnlyList<XmiGraphEdge> edges,
            WallType wallType,
            Level baseLevel)
        {
            string wallId = wallNode["Id"]!.Value<string>()!;

            List<JToken> pointNodes = ResolveTargets(wallId, "XmiHasPoint3d",
                edges, nodeIndex, "XmiPoint3d");

            if (pointNodes.Count < 2)
            {
                throw new InvalidOperationException(
                    $"Wall '{wallId}' needs at least 2 XmiPoint3d nodes, found {pointNodes.Count}.");
            }

            JToken startPt = pointNodes[0];
            JToken endPt = pointNodes[1];

            double startX = startPt["X"]?.Value<double>() ?? 0;
            double startY = startPt["Y"]?.Value<double>() ?? 0;
            double startZ = startPt["Z"]?.Value<double>() ?? 0;
            double endX = endPt["X"]?.Value<double>() ?? 0;
            double endY = endPt["Y"]?.Value<double>() ?? 0;
            double endZ = endPt["Z"]?.Value<double>() ?? 0;

            double baseOffsetFeet;
            if (wallNode["ZOffset"]?.Value<double>() is double zOffsetMm)
            {
                baseOffsetFeet = zOffsetMm / MillimetersPerFoot;
            }
            else
            {
                double baseZFeet = Math.Min(startZ, endZ) / MillimetersPerFoot;
                baseOffsetFeet = baseZFeet - baseLevel.Elevation;
            }

            XYZ start = new XYZ(
                startX / MillimetersPerFoot,
                startY / MillimetersPerFoot,
                baseLevel.Elevation + baseOffsetFeet);

            XYZ end = new XYZ(
                endX / MillimetersPerFoot,
                endY / MillimetersPerFoot,
                baseLevel.Elevation + baseOffsetFeet);

            if (start.DistanceTo(end) <= 1e-9)
            {
                throw new InvalidOperationException($"Wall '{wallId}' has zero-length baseline.");
            }

            Line baseline = Line.CreateBound(start, end);

            double heightFeet = (wallNode["Height"]?.Value<double>() ?? 0) / MillimetersPerFoot;
            if (heightFeet <= 0)
            {
                heightFeet = 3000.0 / MillimetersPerFoot;
            }

            Wall.Create(
                doc,
                baseline,
                wallType.Id,
                baseLevel.Id,
                heightFeet,
                baseOffsetFeet,
                false,
                false);
        }

        /// <summary>
        /// Follows edges of a given type from a source node and returns the resolved
        /// target nodes filtered by entity name.
        /// </summary>
        private static List<JToken> ResolveTargets(
            string sourceId,
            string edgeEntityName,
            IReadOnlyList<XmiGraphEdge> edges,
            IReadOnlyDictionary<string, JToken> nodeIndex,
            string targetEntityName)
        {
            List<JToken> results = new List<JToken>();

            foreach (XmiGraphEdge edge in edges)
            {
                if (edge.Source == sourceId && edge.EntityName == edgeEntityName)
                {
                    if (nodeIndex.TryGetValue(edge.Target, out JToken? targetNode))
                    {
                        if (targetNode["EntityName"]?.Value<string>() == targetEntityName)
                        {
                            results.Add(targetNode);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Sets a built-in parameter value if the parameter exists and is writable.
        /// </summary>
        private static bool SetParameterIfExists(
            FamilyInstance instance, BuiltInParameter bip, double value)
        {
            Parameter? param = instance.get_Parameter(bip);
            if (param != null && !param.IsReadOnly)
            {
                param.Set(value);
                return true;
            }

            return false;
        }

        private static void WriteDiagnosticsToErrorLog(XmiImportDiagnostics diagnostics)
        {
            ModelInfoBuilder.WriteErrorLogToFile(
                $"[BetekkXmiImporter][SUMMARY] {diagnostics.BuildSummaryLine()}");

            foreach (string reason in diagnostics.SkipReasons)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[BetekkXmiImporter][SKIP] {reason}");
            }

            foreach (string reason in diagnostics.FailureReasons)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[BetekkXmiImporter][FAIL] {reason}");
            }
        }

        /// <summary>
        /// Finds a suitable structural column FamilySymbol in the document.
        /// </summary>
        private static FamilySymbol FindStructuralColumnType(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilySymbol));

            FamilySymbol? preferred = collector
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs =>
                    fs.FamilyName.Contains("Concrete", StringComparison.OrdinalIgnoreCase) &&
                    fs.FamilyName.Contains("Rectangular", StringComparison.OrdinalIgnoreCase));

            if (preferred != null) return preferred;

            FamilySymbol? fallback = collector.Cast<FamilySymbol>().FirstOrDefault();

            if (fallback != null) return fallback;

            throw new InvalidOperationException(
                "No structural column family types found in the project. " +
                "Please load a structural column family before importing.");
        }

        /// <summary>
        /// Finds a basic wall type suitable for creating imported wall instances.
        /// </summary>
        private static WallType FindWallType(Document doc)
        {
            WallType? preferred = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Kind == WallKind.Basic);

            if (preferred != null) return preferred;

            WallType? fallback = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault();

            if (fallback != null) return fallback;

            throw new InvalidOperationException(
                "No wall types found in the project. Please load a wall type before importing.");
        }

        /// <summary>
        /// Finds the lowest existing level or creates a default level at elevation 0.
        /// </summary>
        private static Level FindOrCreateBaseLevel(Document doc)
        {
            Level? level = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .FirstOrDefault();

            if (level != null) return level;

            return Level.Create(doc, 0);
        }
    }
}
