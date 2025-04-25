using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using ClassMapper;
using XmiCore;
using Lists;

namespace Utils
{
    internal class Looper
    {
        public static void Point3DLooper(Document doc)
        {
            // 提取所有分析节点（ReferencePoint）
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> nodes = collector
                .OfClass(typeof(ReferencePoint))
                .OfCategory(BuiltInCategory.OST_AnalyticalNodes)
                .WhereElementIsNotElementType()
                .ToElements();

            // 清空旧数据，防止重复添加
            StructuralDataContext.Point3DList.Clear();

            // 映射并添加到全局列表
            foreach (Element point3D in nodes)
            {
                XmiPoint3D mapped = Point3DMapper.Map(point3D);
                StructuralDataContext.Point3DList.Add(mapped);
            }
        }

        public static void StructrualStoreyLooper(Document doc) { 

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

        public static void StructrualMaterialLooper(Document doc)
        {

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            IList<Element> nodes = collector
            .OfClass(typeof(Level))
            .OfCategory(BuiltInCategory.OST_Levels)
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

        // other looper



    }
}
