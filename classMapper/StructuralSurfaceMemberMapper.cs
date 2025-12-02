using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Betekk.RevitXmiExporter.Utils;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Enums;
using XmiSchema.Core.Geometries;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Relationships;
using XmiSchema.Core.Utils;

namespace Betekk.RevitXmiExporter.ClassMapper
{
    internal class StructuralSurfaceMemberMapper : BaseMapper
    {
        public static XmiStructuralSurfaceMember Map(IXmiManager manager, int modelIndex, Element element)
        {
            try
            {
                var (id, name, ifcGuid, nativeId, description) = ExtractBasicProperties(element);

                XmiStructuralMaterial material = ResolveMaterial(manager, modelIndex, element);
                XmiStructuralStorey storey = ResolveStorey(manager, modelIndex, element);

                List<XmiStructuralPointConnection> nodes = new List<XmiStructuralPointConnection>();
                List<XmiSegment> segments = new List<XmiSegment>();

                XmiStructuralSurfaceMemberTypeEnum surfaceMemberType = DetermineSurfaceType(element);
                XmiStructuralSurfaceMemberSystemPlaneEnum systemPlane = XmiStructuralSurfaceMemberSystemPlaneEnum.Unknown;
                double area = ExtractArea(element);
                double height = CalculateHeight(nodes);
                double thickness = ExtractThickness(element);
                const string localAxisX = "1,0,0";
                const string localAxisY = "0,1,0";
                const string localAxisZ = "0,0,1";
                const double zOffset = 0;

                return manager.CreateStructuralSurfaceMember(
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
            }
            catch (Exception ex)
            {
                ModelInfoBuilder.WriteErrorLogToFile($"[StructuralSurfaceMemberMapper] {ex}");
                throw;
            }
        }

        private static XmiStructuralMaterial ResolveMaterial(IXmiManager manager, int modelIndex, Element element)
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

        private static XmiStructuralStorey ResolveStorey(IXmiManager manager, int modelIndex, Element element)
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

            XmiStructuralStorey storey = StructuralStoreyMapper.Map(manager, modelIndex, levelElement);
            if (storey == null)
            {
                return null;
            }

            XmiStructuralStorey existingStorey = manager.GetEntitiesOfType<XmiStructuralStorey>(modelIndex)
                .FirstOrDefault(s => s.NativeId == storey.NativeId);
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
                return Converters.ConvertValueToMillimeter(areaParameter.AsDouble());
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
