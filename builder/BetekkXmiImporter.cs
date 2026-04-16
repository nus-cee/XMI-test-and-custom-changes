using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Betekk.RevitXmiExporter.Utils;
using Newtonsoft.Json.Linq;

namespace Betekk.RevitXmiExporter.Builder
{
    /// <summary>
    /// Imports structural columns into a Revit document from an XMI-compatible JSON file.
    /// Parses column definitions (start/end points, cross-section dimensions) and creates
    /// corresponding structural column family instances in the active model.
    /// </summary>
    public class BetekkXmiImporter
    {
        private const double MillimetersPerFoot = 304.8;

        /// <summary>
        /// Imports columns from a JSON string into the Revit document.
        /// </summary>
        /// <param name="doc">Active Revit document.</param>
        /// <param name="json">JSON content containing column definitions.</param>
        /// <returns>Number of columns successfully created.</returns>
        public int Import(Document doc, string json)
        {
            JObject root = JObject.Parse(json);
            JArray? columns = root["columns"] as JArray;

            if (columns == null || columns.Count == 0)
            {
                throw new InvalidOperationException("No columns found in the JSON file.");
            }

            FamilySymbol columnType = FindStructuralColumnType(doc);

            int createdCount = 0;

            using (Transaction tx = new Transaction(doc, "Import XMI Columns"))
            {
                tx.Start();

                if (!columnType.IsActive)
                {
                    columnType.Activate();
                    doc.Regenerate();
                }

                Level baseLevel = FindOrCreateBaseLevel(doc);

                foreach (JToken columnToken in columns)
                {
                    try
                    {
                        CreateColumnFromJson(doc, columnToken, columnType, baseLevel);
                        createdCount++;
                    }
                    catch (Exception ex)
                    {
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"[BetekkXmiImporter] Failed to create column: {ex.Message}");
                    }
                }

                tx.Commit();
            }

            return createdCount;
        }

        /// <summary>
        /// Creates a single structural column from a JSON token containing position and dimension data.
        /// </summary>
        private static void CreateColumnFromJson(
            Document doc, JToken columnToken, FamilySymbol columnType, Level baseLevel)
        {
            double startX = columnToken["startPoint"]?["x"]?.Value<double>() ?? 0;
            double startY = columnToken["startPoint"]?["y"]?.Value<double>() ?? 0;
            double startZ = columnToken["startPoint"]?["z"]?.Value<double>() ?? 0;
            double endZ = columnToken["endPoint"]?["z"]?.Value<double>() ?? 3000;

            // Convert mm to feet (Revit internal units)
            XYZ location = new XYZ(
                startX / MillimetersPerFoot,
                startY / MillimetersPerFoot,
                startZ / MillimetersPerFoot);

            double heightFeet = (endZ - startZ) / MillimetersPerFoot;

            // Place column at location with base level
            FamilyInstance column = doc.Create.NewFamilyInstance(
                location,
                columnType,
                baseLevel,
                StructuralType.Column);

            // Set the unconnected height so the column extends to the correct top elevation
            Parameter topOffsetParam = column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
            if (topOffsetParam != null && !topOffsetParam.IsReadOnly)
            {
                topOffsetParam.Set(heightFeet);
            }

            // Try to apply cross-section dimensions if provided
            double? width = columnToken["crossSection"]?["width"]?.Value<double>();
            double? height = columnToken["crossSection"]?["height"]?.Value<double>();

            if (width.HasValue)
            {
                SetParameterIfExists(column, BuiltInParameter.FAMILY_WIDTH_PARAM, width.Value / MillimetersPerFoot);
            }
            if (height.HasValue)
            {
                SetParameterIfExists(column, BuiltInParameter.FAMILY_HEIGHT_PARAM, height.Value / MillimetersPerFoot);
            }
        }

        /// <summary>
        /// Sets a built-in parameter value if the parameter exists and is writable.
        /// </summary>
        private static void SetParameterIfExists(FamilyInstance instance, BuiltInParameter bip, double value)
        {
            Parameter? param = instance.get_Parameter(bip);
            if (param != null && !param.IsReadOnly)
            {
                param.Set(value);
            }
        }

        /// <summary>
        /// Finds a suitable structural column FamilySymbol in the document.
        /// Searches for common metric concrete column types first, then falls back to any available structural column.
        /// </summary>
        private static FamilySymbol FindStructuralColumnType(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilySymbol));

            // Prefer concrete rectangular columns
            FamilySymbol? preferred = collector
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs =>
                    fs.FamilyName.Contains("Concrete", StringComparison.OrdinalIgnoreCase) &&
                    fs.FamilyName.Contains("Rectangular", StringComparison.OrdinalIgnoreCase));

            if (preferred != null) return preferred;

            // Fall back to any structural column type
            FamilySymbol? fallback = collector.Cast<FamilySymbol>().FirstOrDefault();

            if (fallback != null) return fallback;

            throw new InvalidOperationException(
                "No structural column family types found in the project. " +
                "Please load a structural column family (e.g., M_Concrete-Rectangular-Column) before importing.");
        }

        /// <summary>
        /// Finds the lowest existing level or creates a default "Level 0" at elevation 0.
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
