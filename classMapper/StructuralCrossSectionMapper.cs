//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Autodesk.Revit.DB;
//using ClassMapper;
//using XmiCore;

//namespace Revit_to_XMI.classMapper
//{

// internal class StructuralCrossSectionMapper : StructuralBaseEntityMapper
//    {
//        /// <summary>
//        /// 将 Revit 元素映射为 XmiStructuralPointConnection
//        /// </summary>
//        public static XmiStructuralCrossSection Map(Element element)
//        {
//            string id = $"node_{element.Id}";
//            string name = element.Name;
//            string ifcGuid = element.UniqueId;
//            string nativeId = element.Id.ToString();
//            string description = element.LookupParameter("Description")?.AsString() ?? "";

//            var material = new XmiStructuralMaterial("storey1", "Storey1", "", "", "", 3.5f, 5000, "RX", "RY", "RZ");



//            string[] parameters = new string[1];

//            float SecondMomentOfAreaXAxis = 1;
//            float SecondMomentOfAreaYAxis = 1;
//            float RadiusOfGyrationXAxis = 1;
//            float RadiusOfGyrationYAxis = 1;
//            float ElasticModulusXAxis = 1;
//            float ElasticModulusYAxis = 1;
//            float PlasticModulusXAxis = 1;
//            float PlasticModulusYAxis = 1;
//            float TorsionalConstant = 1;

//            return new XmiStructuralCrossSection(
//                id,
//                name,
//                ifcGuid,
//                nativeId,
//                description,
//                parameters,
//                SecondMomentOfAreaXAxis, 
//                SecondMomentOfAreaYAxis,
//                RadiusOfGyrationXAxis,
//                RadiusOfGyrationYAxis,
//                ElasticModulusXAxis,
//                ElasticModulusYAxis,
//                TorsionalConstant
//            );
//        }

//        /// <summary>
//        /// 可选：批量收集所有连接点（用在 ExportCommand 等集中处理场景）
//        /// </summary>
//        public static List<XmiStructuralPointConnection> Collect(Document doc)
//        {
//            return new FilteredElementCollector(doc)
//                .OfCategory(BuiltInCategory.OST_StructuralFraming)
//                .WhereElementIsNotElementType()
//                .Select(Map)
//                .ToList();
//        }

//        /// <summary>
//        /// 提取几何中心点或任意代表点（你也可以换成自己的逻辑）
//        /// </summary>
//        private static XYZ GetRepresentativePoint(Element element)
//        {
//            Location location = element.Location;
//            if (location is LocationPoint lp)
//            {
//                return lp.Point;
//            }
//            else if (location is LocationCurve lc)
//            {
//                return lc.Curve.Evaluate(0.5, true); // 中点
//            }

//            return XYZ.Zero;
//        }
//    }
