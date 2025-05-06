using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using ClassMapper;
using XmiCore;
using Lists;
using Autodesk.Revit.DB.Structure;

namespace Utils
{
    internal class Looper
    {
        public static void StructuralPointConnectionLooper(Document doc)
        {
            // 提取所有分析节点（ReferencePoint）
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> nodes = collector
                .OfClass(typeof(ReferencePoint))
                .OfCategory(BuiltInCategory.OST_AnalyticalNodes)
                .WhereElementIsNotElementType()
                .ToElements();

            // 清空旧数据，防止重复添加
            StructuralDataContext.StructuralPointConnectionList.Clear();

            // 映射并添加到全局列表
            foreach (Element pointConnection in nodes)
            {
                XmiStructuralPointConnection mapped = StructuralPointConnectionMapper.Map(pointConnection);
                StructuralDataContext.StructuralPointConnectionList.Add(mapped);
            }
        }

        public static void StructuralStoreyLooper(Document doc)
        {

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> nodes = collector
            .OfClass(typeof(Level))
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .ToElements();


            StructuralDataContext.StructuralStoreyList.Clear();
            // 映射并添加到全局列表
            foreach (Element level in nodes)
            {
                XmiStructuralStorey mapped = StructuralStoreyMapper.Map(level);
                StructuralDataContext.StructuralStoreyList.Add(mapped);
            }
        }

        public static void StructuralMaterialLooper(Document doc)
        {

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> nodes = collector
            .OfClass(typeof(Material))
            .OfCategory(BuiltInCategory.OST_Materials)
            .WhereElementIsNotElementType()
            .ToElements();


            StructuralDataContext.StructuralMaterialList.Clear();
            // 映射并添加到全局列表
            foreach (Element material in nodes)
            {
                XmiStructuralMaterial mapped = StructuralMaterialMapper.Map(material);
                StructuralDataContext.StructuralMaterialList.Add(mapped);
            }
        }





        public static void StructuralCurveMemberLooper(Document doc)
        {
            // 获取所有 AnalyticalMember（分析曲线构件）
            var members = new FilteredElementCollector(doc)
                .OfClass(typeof(AnalyticalMember))
                .Cast<AnalyticalMember>();

            // 清空原有数据
            StructuralDataContext.StructuralCurveMemberList.Clear();

            // 遍历并映射
            foreach (var member in members)
            {
                var mapped = StructuralCurveMemberMapper.Map(member); // 这里的 Map 要接收 AnalyticalMember
                if (mapped != null)
                {
                    StructuralDataContext.StructuralCurveMemberList.Add(mapped);
                }
            }
        }


        //public static void StructuralCurveMemberBeamsLooper(Document doc)
        //{
        //    FilteredElementCollector collector = new FilteredElementCollector(doc);
        //    IList<Element> nodes = collector
        //        .OfClass(typeof(FamilyInstance))
        //        .OfCategory(BuiltInCategory.OST_StructuralFraming)
        //        .WhereElementIsNotElementType()
        //        .ToElements();
        //    StructuralDataContext.StructuralCurveMemberBeamsList.Clear();
        //    // 映射并添加到全局列表
        //    foreach (Element beams in nodes)
        //    {
        //        XmiStructuralCurveMember mapped = StructuralCurveMemberMapper.Map(beams);
        //        StructuralDataContext.StructuralCurveMemberBeamsList.Add(mapped);
        //    }
        //}


        //public static void StructuralSurfaceMemberWallsLooper_nouse(Document doc)
        //{
        //    FilteredElementCollector collector = new FilteredElementCollector(doc);
        //    IList<Element> walls = collector.OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType().ToElements();

        //}


        //public static void StructuralCrossSectionLooper(Document doc)
        //{
        //    FilteredElementCollector collector = new FilteredElementCollector(doc);
        //    IList<Element> nodes = collector.
        //        OfCategory(BuiltInCategory.OST_WallAnalytical)
        //        .WhereElementIsNotElementType()
        //        .ToElements();

        //    StructuralDataContext.StructuralCrossSectionList.Clear();
        //    // 映射并添加到全局列表
        //    foreach (Element crossSections in nodes)
        //    {
        //        XmiStructuralCrossSection mapped = StructuralCrossSectionMapper.Map(crossSections);
        //        StructuralDataContext.StructuralCrossSectionList.Add(mapped);
        //    }
        //}



        public static void StructuralSurfaceMemberWallsLooper(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> nodes = collector.
                OfCategory(BuiltInCategory.OST_WallAnalytical)
                .WhereElementIsNotElementType()
                .ToElements();

            StructuralDataContext.StructuralSurfaceMemberWallsList.Clear();
            // 映射并添加到全局列表
            foreach (Element walls in nodes)
            {
                XmiStructuralSurfaceMember mapped = StructuralSurfaceMemberMapper.Map(walls);
                StructuralDataContext.StructuralSurfaceMemberWallsList.Add(mapped);
            }
        }


        public static void StructuralSurfaceMemberSlabsLooper(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> nodes = collector
                .OfCategory(BuiltInCategory.OST_FloorAnalytical)
                .WhereElementIsNotElementType()
                .ToElements();

            StructuralDataContext.StructuralSurfaceMemberSlabsList.Clear();
            // 映射并添加到全局列表
            foreach (Element slabs in nodes)
            {
                XmiStructuralSurfaceMember mapped = StructuralSurfaceMemberMapper.Map(slabs);
                StructuralDataContext.StructuralSurfaceMemberSlabsList.Add(mapped);
            }

        }


        // other looper

    }
}
