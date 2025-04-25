//using System;
//using System.Collections.Generic;
//using Autodesk.Revit.DB;
//using XmiCore;

//namespace ClassMapper
//{
//    internal class StructuralPointConnectionMapper : StructuralBaseEntityMapper
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
