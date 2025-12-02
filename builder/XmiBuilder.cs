using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using ClassMapper;
using System.Collections.Generic;
using System.Linq;
using XmiSchema.Core.Entities;
using XmiSchema.Core.Geometries;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Models;
using XmiSchema.Core.Relationships;

namespace JsonExporter
{
    public class XmiBuilder
    {
        private readonly IXmiManager _manager;
        private const int _modelIndex = 0;

        public XmiBuilder()
        {
            _manager = new XmiManager();
            XmiModel model = new XmiModel();
            _manager.Models = new List<XmiModel> { model };
        }

        public void BuildModel(Document doc)
        {
            StructuralPointConnectionLooper(doc);
            StructuralStoreyLooper(doc);
            //StructuralMaterialLooper(doc);
            StructuralCurveMemberLooper(doc);
            //StructuralCrossSectionLooper(doc);
            // StructuralSurfaceMemberLooper(doc);
            StructuralSurfaceMemberLooper(doc);
        }

        public string GetJson()
        {
            return _manager.BuildJson(_modelIndex);
        }

        public void StructuralPointConnectionLooper(Document doc)
        {
            var nodes = new FilteredElementCollector(doc)
                .OfClass(typeof(ReferencePoint))
                .OfCategory(BuiltInCategory.OST_AnalyticalNodes)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element pointConnection in nodes)
            {
                StructuralPointConnectionMapper.Map(_manager, _modelIndex, pointConnection);
            }
        }

        public void StructuralStoreyLooper(Document doc)
        {
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element level in levels)
            {
                StructuralStoreyMapper.Map(_manager, _modelIndex, level);
            }
        }

        public void StructuralMaterialLooper(Document doc)
        {
            var materials = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .OfCategory(BuiltInCategory.OST_Materials)
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element material in materials)
            {
                StructuralMaterialMapper.Map(_manager, _modelIndex, (Material)material);
            }
        }

        public void StructuralCurveMemberLooper(Document doc)
        {
            var analyticalMembers = new FilteredElementCollector(doc)
                .OfClass(typeof(AnalyticalMember))
                .Cast<AnalyticalMember>();

            foreach (var member in analyticalMembers)
            {
                StructuralCurveMemberMapper.Map(_manager, _modelIndex, member);
            }
        }

        public void StructuralCrossSectionLooper(Document doc)
        {
            var elementTypes = new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                        .WherePasses(new ElementMulticlassFilter(new[]
        {
            typeof(FloorType),
            typeof(WallType),
            typeof(FamilySymbol), // 包含梁、柱、桁架等族类型
            typeof(RoofType),
            typeof(CeilingType)
        }))
                .ToElements();

            foreach (var element in elementTypes)
            {
                StructuralCrossSectionMapper.Map(_manager, _modelIndex, element);
            }
        }
        public void StructuralSurfaceMemberLooper(Document doc)
        {
            // 注意：Revit 2025 中，AnalyticalPanel 是面状结构分析构件
            var analyticalSurfaces = new FilteredElementCollector(doc)
                .OfClass(typeof(AnalyticalPanel)) // Revit 2024+ 引入的新分析面构件
                .Cast<AnalyticalPanel>();

            foreach (var surface in analyticalSurfaces)
            {
                StructuralSurfaceMemberMapper.Map(_manager, _modelIndex, surface);
            }
        }
    }
}
