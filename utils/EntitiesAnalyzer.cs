//using ClassMapper;
//using Newtonsoft.Json;
//using Autodesk.Revit.DB;
//using 

//namespace RevittoXMI
//{
//    public class EntitiesAnalyzer
//    {
//        public static string GenerateStructuredModelJson(Document doc)
//        {
//            var JsonData = new Dictionary<string, object>();

//            // 只构建 StructuralModel，目前一个字段
//            JsonData["StructuralModel"] = StructuralModelMapper.GetStructuralModel(doc);
//            JsonData["StructuralMaterial"] = StructuralMaterialMapper.GetStructuralMaterial(doc);



//            // 将整个对象转成格式化的 JSON 字符串
//            string json = JsonConvert.SerializeObject(JsonData, Formatting.Indented);
//            return json;
//        }
//    }
//}

