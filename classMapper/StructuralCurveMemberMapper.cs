//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using XmiCore;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.DB.Structure;
//using System.Xml.Linq;

//namespace ClassMapper
//{
//    internal class StructuralCurveMember
//    {
//        public static List<Dictionary<string, object>> Build(Document doc)
//        {
//            var modelInfoList = new List<Dictionary<string, object>>();

//            // 获取所有 Structural Framing 元素
//            var collector = new FilteredElementCollector(doc)
//                            .OfCategory(BuiltInCategory.OST_StructuralFraming)
//                            .WhereElementIsNotElementType();

//            foreach (var element in collector)
//            {
//                var data = new Dictionary<string, object>();

//                string ID = element.Id.ToString();
//                string Name = element.Name;
//                string Ifcguid = element.UniqueId; // 模拟 IFC GUID
//                string NativeId = element.Id.ToString();
//                string Description = element.LookupParameter("Description")?.AsString() ?? "";

//                // 以下为模拟/空值填充，需替换为你具体逻辑
//                XmiStructuralCrossSection CrossSection = null;
//                XmiStructuralStorey Storey = null;
//                XmiStructuralCurveMemberTypeEnum CurvememberType = ;
//                List<XmiStructuralPointConnection> Nodes = new List<XmiStructuralPointConnection>(); // 你需要计算实际连接点
//                List<XmiBaseEntity> Segments = new List<XmiBaseEntity>(); // 如有分段信息，可添加
//                XmiStructuralCurveMemberSystemLineEnum SystemLine = XmiStructuralCurveMemberSystemLineEnum.Axis;
//                XmiStructuralPointConnection BeginNode = null;
//                XmiStructuralPointConnection EndNode = null;
//                float Length = (float)(element.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0) * 0.3048f; // 转换为米
//                string LocalAxisX = ""; // 可从几何信息推导
//                string LocalAxisY = "";
//                string LocalAxisZ = "";
//                float BeginNodeXOffset = 0;
//                float EndNodeXOffset = 0;
//                float BeginNodeYOffset = 0;
//                float EndNodeYOffset = 0;
//                float BeginNodeZOffset = 0;
//                float EndNodeZOffset = 0;
//                string EndFixityStart = ""; // 可根据连接参数推导

//                 new XmiStructuralCurveMember(ID,Name, Ifcguid.......)


//            }

//            return modelInfoList;
//        }
//    }
//}
