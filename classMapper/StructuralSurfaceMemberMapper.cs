using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Betekk.RevitXmiExporter.ClassMapper.Base;
using Betekk.RevitXmiExporter.Utils;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Enums;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Models.Entities.StructuralAnalytical;
using XmiSchema.Core.Utils;

namespace Betekk.RevitXmiExporter.ClassMapper
{
    internal class StructuralSurfaceMemberMapper : StructuralBaseEntityMapper
    {
        public static XmiStructuralSurfaceMember Map(IXmiManager manager, int modelIndex, Element element)
        {
            try
            {
                var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

                XmiMaterial material = ResolveMaterial(manager, modelIndex, element);
                XmiStorey storey = ResolveStorey(manager, modelIndex, element);

                (List<XmiStructuralPointConnection> nodes, List<XmiSegment> segments) = BuildSurfaceTopology(
                    manager,
                    modelIndex,
                    element,
                    storey,
                    id,
                    name,
                    nativeId);

                XmiStructuralSurfaceMemberTypeEnum surfaceMemberType = DetermineSurfaceType(element);
                XmiStructuralSurfaceMemberSystemPlaneEnum systemPlane = XmiStructuralSurfaceMemberSystemPlaneEnum.Unknown;
                double area = ExtractArea(element);
                double height = CalculateHeight(nodes);
                double thickness = ExtractThickness(element);
                const string localAxisX = "1,0,0";
                const string localAxisY = "0,1,0";
                const string localAxisZ = "0,0,1";
                const double zOffset = 0;

                XmiStructuralSurfaceMember surfaceMember = manager.CreateStructuralSurfaceMember(
                    modelIndex,
                    id,
                    name,
                    ifcGuid,
                    nativeId,
                    description,
                    material,
                    surfaceMemberType,
                    thickness,
                    systemPlane,
                    nodes,
                    storey,
                    segments,
                    area,
                    zOffset,
                    localAxisX,
                    localAxisY,
                    localAxisZ,
                    height);

                if (surfaceMember == null)
                {
                    ModelInfoBuilder.WriteErrorLogToFile($"[StructuralSurfaceMemberMapper] Failed to create surface member {id}");
                }

                return surfaceMember;
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[StructuralSurfaceMemberMapper] {ex}");
                throw;
            }
        }

        private static (List<XmiStructuralPointConnection> Nodes, List<XmiSegment> Segments) BuildSurfaceTopology(
            IXmiManager manager,
            int modelIndex,
            Element element,
            XmiStorey storey,
            string ownerId,
            string ownerName,
            string ownerNativeId)
        {
            List<XmiStructuralPointConnection> nodes = new();
            List<XmiSegment> segments = new();

            if (element is not AnalyticalPanel panel)
            {
                return (nodes, segments);
            }

            IList<CurveLoop> loops = GetPanelLoops(panel);
            if (loops == null || loops.Count == 0)
            {
                ModelInfoBuilder.WriteErrorLogToFile(
                    $"[StructuralSurfaceMemberMapper] No analytical loops found for element {element.Id}");
                return (nodes, segments);
            }

            Dictionary<string, XmiStructuralPointConnection> nodeLookup =
                new(StringComparer.OrdinalIgnoreCase);
            int nodeIndex = 1;

            foreach (CurveLoop loop in loops)
            {
                List<Curve> edges = loop.ToList();
                foreach (Curve edge in edges)
                {
                    if (edge == null)
                    {
                        continue;
                    }

                    AddNode(manager, modelIndex, storey, ownerId, ownerName, ownerNativeId, nodes, nodeLookup, ref nodeIndex, edge.GetEndPoint(0));
                }

                if (edges.Count > 0)
                {
                    Curve lastEdge = edges[^1];
                    AddNode(manager, modelIndex, storey, ownerId, ownerName, ownerNativeId, nodes, nodeLookup, ref nodeIndex, lastEdge.GetEndPoint(1));
                }

                segments.AddRange(StructuralSegmentMapper.MapLoopSegments(ownerId, ownerName, ownerNativeId, edges));
            }

            return (nodes, segments);
        }

        private static IList<CurveLoop> GetPanelLoops(AnalyticalPanel panel)
        {
            IList<CurveLoop> reflectiveLoops = TryInvokePanelLoops(panel);
            if (reflectiveLoops != null && reflectiveLoops.Count > 0)
            {
                return reflectiveLoops;
            }

            return ExtractLoopsFromGeometry(panel);
        }

        private static IList<CurveLoop> TryInvokePanelLoops(AnalyticalPanel panel)
        {
            MethodInfo getLoopsMethod = typeof(AnalyticalPanel).GetMethod("GetLoops", BindingFlags.Public | BindingFlags.Instance);
            if (getLoopsMethod == null)
            {
                return null;
            }

            try
            {
                object result = getLoopsMethod.Invoke(panel, new object[] { AnalyticalLoopType.External });
                return result as IList<CurveLoop>;
            }
            catch
            {
                return null;
            }
        }

        private static IList<CurveLoop> ExtractLoopsFromGeometry(AnalyticalPanel panel)
        {
            List<CurveLoop> loops = new();
            Options options = new Options
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geometry = panel.get_Geometry(options);
            if (geometry == null)
            {
                return loops;
            }

            CollectLoopsFromGeometry(geometry, loops);
            return loops;
        }

        private static void CollectLoopsFromGeometry(GeometryElement geometry, List<CurveLoop> loops)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        AddLoopsFromFace(face, loops);
                    }
                }
                else if (obj is GeometryInstance instance)
                {
                    GeometryElement instanceGeometry = instance.GetInstanceGeometry();
                    if (instanceGeometry != null)
                    {
                        CollectLoopsFromGeometry(instanceGeometry, loops);
                    }
                }
                else if (obj is Face face)
                {
                    AddLoopsFromFace(face, loops);
                }
            }
        }

        private static void AddLoopsFromFace(Face face, List<CurveLoop> loops)
        {
            if (face is not PlanarFace planarFace)
            {
                return;
            }

            foreach (CurveLoop loop in planarFace.GetEdgesAsCurveLoops())
            {
                if (loop != null && loop.Any())
                {
                    loops.Add(loop);
                }
            }
        }

        private static void AddNode(
            IXmiManager manager,
            int modelIndex,
            XmiStorey storey,
            string ownerId,
            string ownerName,
            string ownerNativeId,
            List<XmiStructuralPointConnection> nodes,
            Dictionary<string, XmiStructuralPointConnection> nodeLookup,
            ref int nodeIndex,
            XYZ point)
        {
            if (point == null)
            {
                return;
            }

            string nodeId = $"{ownerId}_node_{nodeIndex}";
            string nodeName = $"{ownerName} Node {nodeIndex}";
            string nodeNativeId = $"{ownerNativeId}_NODE_{nodeIndex}";
            XmiStructuralPointConnection connection = StructuralPointConnectionMapper.Map(
                manager,
                modelIndex,
                nodeId,
                nodeName,
                nodeNativeId,
                storey,
                point);

            if (connection == null)
            {
                return;
            }

            string key = string.IsNullOrWhiteSpace(connection.NativeId) ? connection.Id : connection.NativeId;
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (!nodeLookup.ContainsKey(key))
            {
                nodeLookup[key] = connection;
                nodes.Add(connection);
            }

            nodeIndex++;
        }

        private static XmiMaterial ResolveMaterial(IXmiManager manager, int modelIndex, Element element)
        {
            ICollection<ElementId> materialIds = null;
            if (element is FamilyInstance familyInstance && familyInstance.Symbol != null)
            {
                materialIds = familyInstance.Symbol.GetMaterialIds(false);
            }
            else if (element is ElementType typeElement)
            {
                materialIds = typeElement.GetMaterialIds(false);
            }

            if (materialIds == null || materialIds.Count == 0)
            {
                return null;
            }

            Material materialElement = element.Document.GetElement(materialIds.First()) as Material;
            return materialElement != null ? StructuralMaterialMapper.Map(manager, modelIndex, materialElement) : null;
        }

        private static XmiStorey ResolveStorey(IXmiManager manager, int modelIndex, Element element)
        {
            if (element.LevelId == null || element.LevelId == ElementId.InvalidElementId)
            {
                return null;
            }

            Level levelElement = element.Document.GetElement(element.LevelId) as Level;
            if (levelElement == null)
            {
                return null;
            }

            XmiStorey storey = StructuralStoreyMapper.Map(manager, modelIndex, levelElement);
            if (storey == null)
            {
                return null;
            }

            XmiStorey existingStorey = manager.GetEntitiesOfType<XmiStorey>(modelIndex)
                .FirstOrDefault(s => s.Id == storey.Id);
            return existingStorey ?? storey;
        }

        private static XmiStructuralSurfaceMemberTypeEnum DetermineSurfaceType(Element element)
        {
            string surfaceTypeStr = "Unknown";
            if (element.Category?.Name.Contains("wall", StringComparison.OrdinalIgnoreCase) == true)
            {
                surfaceTypeStr = "Wall";
            }
            else if (element.Category?.Name.Contains("floor", StringComparison.OrdinalIgnoreCase) == true)
            {
                surfaceTypeStr = "Slab";
            }

            return ExtensionEnumHelper.FromEnumValue<XmiStructuralSurfaceMemberTypeEnum>(surfaceTypeStr)
                ?? XmiStructuralSurfaceMemberTypeEnum.Unknown;
        }

        private static double ExtractThickness(Element element)
        {
            Parameter thicknessParam = element.LookupParameter("Thickness");
            if (thicknessParam?.StorageType == StorageType.Double)
            {
                return Converters.ConvertValueToMillimeter(thicknessParam.AsDouble());
            }

            return 0;
        }

        private static double ExtractArea(Element element)
        {
            Parameter areaParameter = element.LookupParameter("Area");
            if (areaParameter?.StorageType == StorageType.Double)
            {
                return Converters.SquareFeetToSquareMillimeter(areaParameter.AsDouble());
            }

            return 0;
        }

        private static double CalculateHeight(List<XmiStructuralPointConnection> nodes)
        {
            if (nodes.Count >= 2)
            {
                List<double> zValues = nodes.Select(n => n.Point.Z).ToList();
                return zValues.Max() - zValues.Min();
            }

            return 0;
        }
    }

    public class XYZEqualityComparer : IEqualityComparer<XYZ>
    {
        private const double Tolerance = 1e-6;

        public bool Equals(XYZ a, XYZ b)
        {
            return a != null && b != null && a.IsAlmostEqualTo(b, Tolerance);
        }

        public int GetHashCode(XYZ obj)
        {
            return obj == null ? 0 : obj.X.GetHashCode() ^ obj.Y.GetHashCode() ^ obj.Z.GetHashCode();
        }
    }
}
