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
    /// Coordinates extraction of structural framing elements (beams, columns) from Revit
    /// and builds dual-representation XMI model (physical + analytical domains).
    /// Includes materials, cross-sections, and geometry.
    /// This class is split into multiple partial classes for maintainability.
    /// </summary>
    public partial class BetekkXmiBuilder
    {
        private readonly IXmiManager _manager;
        private XmiModel _model;
        private const int ModelIndex = 0;

        // Point deduplication cache (tolerance: 1e-10)
        private readonly Dictionary<string, XmiPoint3d> _pointCache;

        // Storey cache to avoid duplicates
        private readonly Dictionary<string, XmiStorey> _storeyCache;

        // StructuralPointConnection cache
        private readonly Dictionary<string, XmiStructuralPointConnection> _connectionCache;

        // Material cache (keyed by Revit Material ElementId)
        private readonly Dictionary<string, XmiMaterial> _materialCache;

        // CrossSection cache (keyed by Revit FamilySymbol/Type ElementId)
        private readonly Dictionary<string, XmiCrossSection> _crossSectionCache;

        // Geometry caches
        private readonly Dictionary<string, XmiSegment> _segmentCache;

        // Physical element caches
        private readonly Dictionary<string, XmiBeam> _beamCache;
        private readonly Dictionary<string, XmiColumn> _columnCache;

        // Deferred physical→analytical mappings
        private readonly List<(XmiBeam beam, string analyticalNativeId)> _beamToAnalyticalLinks;
        private readonly List<(XmiColumn column, string analyticalNativeId)> _columnToAnalyticalLinks;

        // Cache for analytical members (keyed by Revit ElementId as string)
        // Stores XmiStructuralCurveMember entities created from AnalyticalMember elements
        private readonly Dictionary<string, XmiStructuralCurveMember> _analyticalMemberCache;

        // Tolerance for point deduplication (1e-10 in mm)
        private const double PointTolerance = 1e-10;

        // Export counters
        private int _storeyCount = 0;
        private int _beamCount = 0;
        private int _columnCount = 0;
        private int _wallCount = 0;
        private int _slabCount = 0;
        private int _analyticalMemberCount = 0;
        private int _materialCount = 0;
        private int _crossSectionCount = 0;
        private int _pointCount = 0;
        private int _connectionCount = 0;
        private int _segmentCount = 0;
        private int _lineCount = 0;
        private int _arcCount = 0;

        public BetekkXmiBuilder()
        {
            _manager = new XmiManager();
            _model = new XmiModel();
            _manager.Models = new List<XmiModel> { _model };

            _pointCache = new Dictionary<string, XmiPoint3d>();
            _storeyCache = new Dictionary<string, XmiStorey>();
            _connectionCache = new Dictionary<string, XmiStructuralPointConnection>();
            _materialCache = new Dictionary<string, XmiMaterial>();
            _crossSectionCache = new Dictionary<string, XmiCrossSection>();
            _segmentCache = new Dictionary<string, XmiSegment>();
            _beamCache = new Dictionary<string, XmiBeam>();
            _columnCache = new Dictionary<string, XmiColumn>();
            _beamToAnalyticalLinks = new List<(XmiBeam, string)>();
            _columnToAnalyticalLinks = new List<(XmiColumn, string)>();
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
            ProcessStructuralColumnsElements(doc);

            // Phase 4: Gather materials from other structural elements to ensure they appear in the material list
            ProcessFloorMaterials(doc);
            ProcessWallMaterials(doc);

            // Phase 5: Create physical→analytical relationships after all physical elements are processed
            ProcessingPhysicalToAnalytical();

            // Debug: Log final XmiLine3d summary
            LogXmiLine3dSummary();
        }

        /// <summary>
        /// Logs a summary of all XmiLine3d entities in the model for debugging duplication issues.
        /// </summary>
        private void LogXmiLine3dSummary()
        {
            var allLines = _model.Entities.OfType<XmiLine3d>().ToList();
            ModelInfoBuilder.WriteErrorLogToFile("");
            ModelInfoBuilder.WriteErrorLogToFile("========================================");
            ModelInfoBuilder.WriteErrorLogToFile($"[XmiLine3d Summary] Total count: {allLines.Count}");
            ModelInfoBuilder.WriteErrorLogToFile("========================================");

            for (int i = 0; i < allLines.Count; i++)
            {
                var line = allLines[i];
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[{i + 1}] ID={line.Id}, NativeId={line.NativeId}, Name={line.Name}");
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"    StartPoint: ({line.StartPoint.X:F6}, {line.StartPoint.Y:F6}, {line.StartPoint.Z:F6}) PointID={line.StartPoint.Id}");
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"    EndPoint:   ({line.EndPoint.X:F6}, {line.EndPoint.Y:F6}, {line.EndPoint.Z:F6}) PointID={line.EndPoint.Id}");
            }

            // Check for potential duplicates by comparing all lines
            ModelInfoBuilder.WriteErrorLogToFile("");
            ModelInfoBuilder.WriteErrorLogToFile("[XmiLine3d Summary] Checking for coincident lines...");
            int duplicateCount = 0;
            for (int i = 0; i < allLines.Count; i++)
            {
                for (int j = i + 1; j < allLines.Count; j++)
                {
                    if (allLines[i].IsCoincident(allLines[j]))
                    {
                        duplicateCount++;
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"⚠ DUPLICATE FOUND: Line[{i}] (ID={allLines[i].Id}) is coincident with Line[{j}] (ID={allLines[j].Id})");
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"  Line[{i}]: NativeId={allLines[i].NativeId}, Name={allLines[i].Name}");
                        ModelInfoBuilder.WriteErrorLogToFile(
                            $"  Line[{j}]: NativeId={allLines[j].NativeId}, Name={allLines[j].Name}");
                    }
                }
            }

            if (duplicateCount == 0)
            {
                ModelInfoBuilder.WriteErrorLogToFile("[XmiLine3d Summary] ✓ No duplicate lines found!");
            }
            else
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[XmiLine3d Summary] ⚠ Found {duplicateCount} duplicate line(s)!");
            }
            ModelInfoBuilder.WriteErrorLogToFile("========================================");
            ModelInfoBuilder.WriteErrorLogToFile("");
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
        /// Gets export statistics containing counts of all exported entities.
        /// </summary>
        /// <returns>Export statistics object.</returns>
        public ExportStatistics GetExportStatistics()
        {
            _storeyCount = _model.Entities.OfType<XmiStorey>().Count();
            _beamCount = _model.Entities.OfType<XmiBeam>().Count();
            _columnCount = _model.Entities.OfType<XmiColumn>().Count();
            _analyticalMemberCount = _model.Entities.OfType<XmiStructuralCurveMember>().Count();
            _materialCount = _model.Entities.OfType<XmiMaterial>().Count();
            _crossSectionCount = _model.Entities.OfType<XmiCrossSection>().Count();
            _pointCount = _model.Entities.OfType<XmiPoint3d>().Count();
            _connectionCount = _model.Entities.OfType<XmiStructuralPointConnection>().Count();
            _segmentCount = _model.Entities.OfType<XmiSegment>().Count();
            _lineCount = _model.Entities.OfType<XmiLine3d>().Count();
            _arcCount = _model.Entities.OfType<XmiArc3d>().Count();

            return new ExportStatistics
            {
                StoreyCount = _storeyCount,
                BeamCount = _beamCount,
                ColumnCount = _columnCount,
                AnalyticalMemberCount = _analyticalMemberCount,
                MaterialCount = _materialCount,
                CrossSectionCount = _crossSectionCount,
                PointCount = _pointCount,
                ConnectionCount = _connectionCount,
                SegmentCount = _segmentCount,
                LineCount = _lineCount,
                ArcCount = _arcCount
            };
        }
    }

    /// <summary>
    /// Contains statistics about exported entities.
    /// </summary>
    public class ExportStatistics
    {
        public int StoreyCount { get; set; }
        public int BeamCount { get; set; }
        public int ColumnCount { get; set; }
        public int AnalyticalMemberCount { get; set; }
        public int MaterialCount { get; set; }
        public int CrossSectionCount { get; set; }
        public int PointCount { get; set; }
        public int ConnectionCount { get; set; }
        public int SegmentCount { get; set; }
        public int LineCount { get; set; }
        public int ArcCount { get; set; }
    }
}
