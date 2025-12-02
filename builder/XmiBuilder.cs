using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Betekk.RevitXmiExporter.ClassMapper;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Geometries;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Models;
using XmiSchema.Core.Relationships;

namespace Betekk.RevitXmiExporter.Builder
{
    public class XmiBuilder
    {
        private readonly IXmiManager _manager;
        private const int ModelIndex = 0;

        public XmiBuilder()
        {
            _manager = new XmiManager();
            _manager.Models = new List<XmiModel> { new XmiModel() };
        }

        public void BuildModel(Document doc)
        {
            StructuralMaterialLooper(doc);
            StructuralCrossSectionLooper(doc);
            StructuralPointConnectionLooper(doc);
            StructuralStoreyLooper(doc);
            StructuralCurveMemberLooper(doc);
            StructuralSurfaceMemberLooper(doc);
        }

        public string GetJson()
        {
            return _manager.BuildJson(ModelIndex);
        }

        public void StructuralPointConnectionLooper(Document doc)
        {
            IEnumerable<Element> nodes = new FilteredElementCollector(doc)
                .OfClass(typeof(ReferencePoint))
                .OfCategory(BuiltInCategory.OST_AnalyticalNodes)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element pointConnection in nodes)
            {
                StructuralPointConnectionMapper.Map(_manager, ModelIndex, pointConnection);
            }
        }

        public void StructuralStoreyLooper(Document doc)
        {
            IEnumerable<Element> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element level in levels)
            {
                StructuralStoreyMapper.Map(_manager, ModelIndex, level);
            }
        }

        public void StructuralMaterialLooper(Document doc)
        {
            IEnumerable<Element> materials = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .OfCategory(BuiltInCategory.OST_Materials)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Material material in materials)
            {
                StructuralMaterialMapper.Map(_manager, ModelIndex, material);
            }
        }

        public void StructuralCurveMemberLooper(Document doc)
        {
            IEnumerable<AnalyticalMember> analyticalMembers = new FilteredElementCollector(doc)
                .OfClass(typeof(AnalyticalMember))
                .Cast<AnalyticalMember>();

            foreach (AnalyticalMember member in analyticalMembers)
            {
                StructuralCurveMemberMapper.Map(_manager, ModelIndex, member);
            }
        }

        public void StructuralCrossSectionLooper(Document doc)
        {
            ElementMulticlassFilter filter = new ElementMulticlassFilter(new[]
            {
                typeof(FloorType),
                typeof(WallType),
                typeof(FamilySymbol),
                typeof(RoofType),
                typeof(CeilingType)
            });

            IEnumerable<Element> elementTypes = new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                .WherePasses(filter)
                .ToElements();

            foreach (Element element in elementTypes)
            {
                StructuralCrossSectionMapper.Map(_manager, ModelIndex, element);
            }
        }

        public void StructuralSurfaceMemberLooper(Document doc)
        {
            IEnumerable<AnalyticalPanel> analyticalPanels = new FilteredElementCollector(doc)
                .OfClass(typeof(AnalyticalPanel))
                .Cast<AnalyticalPanel>();

            foreach (AnalyticalPanel surface in analyticalPanels)
            {
                StructuralSurfaceMemberMapper.Map(_manager, ModelIndex, surface);
            }
        }
    }
}
