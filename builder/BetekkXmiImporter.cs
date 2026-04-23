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

        /// <summary>
        /// Imports structural elements from an XMI graph JSON string into the Revit document.
        /// The JSON must contain top-level "nodes" and "edges" arrays.
        /// </summary>
        /// <param name="doc">Active Revit document.</param>
        /// <param name="json">JSON content with nodes/edges graph structure.</param>
        /// <returns>Number of elements successfully created.</returns>
        public int Import(Document doc, string json)
        {
            JObject root = JObject.Parse(json);

            JArray? nodesArray = root["nodes"] as JArray;
            JArray? edgesArray = root["edges"] as JArray;

            if (nodesArray == null || nodesArray.Count == 0)
            {
                throw new InvalidOperationException("No nodes found in the JSON file.");
            }

            // Index nodes by Id for fast lookup
            Dictionary<string, JToken> nodeIndex = new Dictionary<string, JToken>();
            foreach (JToken node in nodesArray)
            {
                string? id = node["Id"]?.Value<string>();
                if (!string.IsNullOrEmpty(id))
                {
                    nodeIndex[id] = node;
                }
            }

            // Build edge lookup: Source -> list of (EntityName, TargetId)
            List<(string Source, string Target, string EntityName)> edges =
                new List<(string, string, string)>();

            if (edgesArray != null)
            {
                foreach (JToken edge in edgesArray)
                {
                    string? source = edge["Source"]?.Value<string>();
                    string? target = edge["Target"]?.Value<string>();
                    string? entityName = edge["EntityName"]?.Value<string>();

                    if (!string.IsNullOrEmpty(source) &&
                        !string.IsNullOrEmpty(target) &&
                        !string.IsNullOrEmpty(entityName))
                    {
                        edges.Add((source, target, entityName));
                    }
                }
            }

            // Find all XmiColumn nodes
            List<JToken> columnNodes = nodesArray
                .Where(n => n["EntityName"]?.Value<string>() == "XmiColumn")
                .ToList();

            if (columnNodes.Count == 0)
            {
                throw new InvalidOperationException(
                    "No XmiColumn entities found in the nodes array.");
            }

            FamilySymbol columnType = FindStructuralColumnType(doc);
            int createdCount = 0;

            using (Transaction tx = new Transaction(doc, "Import XMI Graph"))
            {
                tx.Start();

                if (!columnType.IsActive)
                {
                    columnType.Activate();
                    doc.Regenerate();
                }

                Level baseLevel = FindOrCreateBaseLevel(doc);

                foreach (JToken columnNode in columnNodes)
                {
                    try
                    {
                        CreateColumnFromGraph(doc, columnNode, nodeIndex, edges,
                            columnType, baseLevel);
                        createdCount++;
                    }
                    catch (Exception ex)
                    {
                        string colName = columnNode["Name"]?.Value<string>() ?? "unknown";
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"[BetekkXmiImporter] Failed to create column '{colName}': {ex.Message}");
                    }
                }

                tx.Commit();
            }

            return createdCount;
        }

        /// <summary>
        /// Creates a single structural column by resolving its geometry and cross-section
        /// from the graph relationships.
        /// </summary>
        private static void CreateColumnFromGraph(
            Document doc,
            JToken columnNode,
            Dictionary<string, JToken> nodeIndex,
            List<(string Source, string Target, string EntityName)> edges,
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

            FamilyInstance column = doc.Create.NewFamilyInstance(
                location,
                columnType,
                baseLevel,
                StructuralType.Column);

            // Set column height
            Parameter topOffsetParam =
                column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
            if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
            {
                topOffsetParam.Set(heightFeet);
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
        /// Follows edges of a given type from a source node and returns the resolved
        /// target nodes filtered by entity name.
        /// </summary>
        private static List<JToken> ResolveTargets(
            string sourceId,
            string edgeEntityName,
            List<(string Source, string Target, string EntityName)> edges,
            Dictionary<string, JToken> nodeIndex,
            string targetEntityName)
        {
            List<JToken> results = new List<JToken>();

            foreach (var edge in edges)
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
        private static void SetParameterIfExists(
            FamilyInstance instance, BuiltInParameter bip, double value)
        {
            Parameter? param = instance.get_Parameter(bip);
            if (param != null && !param.IsReadOnly)
            {
                param.Set(value);
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
