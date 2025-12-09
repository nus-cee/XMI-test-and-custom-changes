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
    /// Partial class containing column-specific geometry extraction methods.
    /// Handles extraction of column axes from solids, vertices, and parameters.
    /// </summary>
    public partial class BetekkXmiBuilder
    {
        private bool TryGetColumnAxisFromGeometry(FamilyInstance column, out Line axis)
        {
            axis = null;
            if (column == null)
            {
                return false;
            }

            Options opt = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geomElem = column.get_Geometry(opt);
            if (geomElem == null)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Column {column?.Id} geometry element is null.");
                return false;
            }

            Solid mainSolid = GetMainSolid(geomElem);
            if (mainSolid == null)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Column {column?.Id} has no solid geometry.");
                return false;
            }

            List<XYZ> verts = ExtractVertices(mainSolid);
            if (verts.Count == 0)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Column {column?.Id} has zero vertices after triangulation.");
                return false;
            }

            axis = ComputeLongestAxis(verts);
            return axis != null;
        }

        private bool TryGetColumnEndPoints(FamilyInstance column, out XYZ start, out XYZ end)
        {
            start = null;
            end = null;

            if (column == null)
            {
                return false;
            }

            if (!TryGetColumnAxisFromGeometry(column, out Line axis) || axis == null)
            {
                return false;
            }

            start = axis.GetEndPoint(0);
            end = axis.GetEndPoint(1);
            return start != null && end != null;
        }

        private Solid GetMainSolid(GeometryElement geomElem)
        {
            foreach (GeometryObject obj in geomElem)
            {
                if (obj is Solid solid && solid.Volume > 1e-6)
                {
                    return solid;
                }

                if (obj is GeometryInstance inst)
                {
                    GeometryElement symbolGeom = inst.GetInstanceGeometry();
                    foreach (GeometryObject instObj in symbolGeom)
                    {
                        if (instObj is Solid instSolid && instSolid.Volume > 1e-6)
                        {
                            return instSolid;
                        }
                    }
                }
            }

            return null;
        }

        private List<XYZ> ExtractVertices(Solid solid)
        {
            List<XYZ> verts = new List<XYZ>();
            foreach (Face face in solid.Faces)
            {
                Mesh mesh = face.Triangulate();
                foreach (XYZ v in mesh.Vertices)
                {
                    verts.Add(v);
                }
            }

            return verts;
        }

        private Line ComputeLongestAxis(List<XYZ> pts)
        {
            double maxDist = 0.0;
            XYZ p1 = null;
            XYZ p2 = null;

            for (int i = 0; i < pts.Count; i++)
            {
                for (int j = i + 1; j < pts.Count; j++)
                {
                    double d = pts[i].DistanceTo(pts[j]);
                    if (d > maxDist)
                    {
                        maxDist = d;
                        p1 = pts[i];
                        p2 = pts[j];
                    }
                }
            }

            if (p1 == null || p2 == null)
            {
                return null;
            }

            return Line.CreateBound(p1, p2);
        }

        private bool TryGetColumnCurveFromParameters(Document doc, FamilyInstance column, out Line line)
        {
            line = null;
            try
            {
                LocationPoint locPoint = column.Location as LocationPoint;
                if (locPoint == null)
                {
                    return false;
                }

                ElementId baseLevelId = column.get_Parameter(BuiltInParameter.SCHEDULE_BASE_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId;
                ElementId topLevelId = column.get_Parameter(BuiltInParameter.SCHEDULE_TOP_LEVEL_PARAM)?.AsElementId() ?? ElementId.InvalidElementId;

                if (baseLevelId == ElementId.InvalidElementId || topLevelId == ElementId.InvalidElementId)
                {
                    return false;
                }

                Level baseLevel = doc.GetElement(baseLevelId) as Level;
                Level topLevel = doc.GetElement(topLevelId) as Level;
                if (baseLevel == null || topLevel == null)
                {
                    return false;
                }

                double baseOffset = column.get_Parameter(BuiltInParameter.SCHEDULE_BASE_LEVEL_OFFSET_PARAM)?.AsDouble() ?? 0.0;
                double topOffset = column.get_Parameter(BuiltInParameter.SCHEDULE_TOP_LEVEL_OFFSET_PARAM)?.AsDouble() ?? 0.0;

                double startZ = baseLevel.ProjectElevation + baseOffset;
                double endZ = topLevel.ProjectElevation + topOffset;

                XYZ start = new XYZ(locPoint.Point.X, locPoint.Point.Y, startZ);
                XYZ end = new XYZ(locPoint.Point.X, locPoint.Point.Y, endZ);

                line = Line.CreateBound(start, end);
                return true;
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[BetekkXmiBuilder] Column {column?.Id}: failed to build curve from parameters - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates relationship linking physical element to its analytical representation.
        /// </summary>
    }
}
