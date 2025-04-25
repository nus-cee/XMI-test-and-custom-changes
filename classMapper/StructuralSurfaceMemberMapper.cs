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
//    internal class StructuralSurfaceMemberMapper: StructuralBaseEntityMapper
//    {
//        /// <summary>
//        /// 将 Revit 元素映射为 XmiStructuralPointConnection
//        /// </summary>
//        public static XmiStructuralPointConnection Map(Element element)
//        {
//            string id = $"node_{element.Id}";
//            string name = element.Name;
//            string ifcGuid = element.UniqueId;
//            string nativeId = element.Id.ToString();
//            string description = element.LookupParameter("Description")?.AsString() ?? "";

//            var material = new XmiStructuralMaterial(id,name,ifcGuid,nativeId,description,) ;


//            // 模拟构建 storey（你可以用 StoreyMapper.Map(...) 替代）
//            var storey = new XmiStructuralStorey("storey1", "Storey1", "", "", "", 3.5f, 5000, "RX", "RY", "RZ");

//            // 提取几何中某一点作为连接点（这里只是示例）
//            XYZ position = GetRepresentativePoint(element);

//            var point = new XmiPoint3D(
//                $"p_{element.Id}", $"Point_{element.Id}", "", "", "",
//                (float)position.X,
//                (float)position.Y,
//                (float)position.Z
//            );

//            return new XmiStructuralPointConnection(
//                id,
//                name,
//                ifcGuid,
//                nativeId,
//                description,
//                storey,
//                point
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
//}
