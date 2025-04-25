//using Autodesk.Revit.DB;
//using Autodesk.Revit.ApplicationServices;
//using RevittoXMI;

//namespace ClassMapper
//{
//    public static class StructuralModelMapper
//    {
//        public static List<Dictionary<string, object>> GetStructuralModel(Document doc)
//        {
//            List<Dictionary<String, Object>> structuralModelList = new List<Dictionary<String, Object>>();

//            //Project Information
//            Dictionary<String, Object> data = new Dictionary<string, object>();

//            string titleName = doc.Title;
//            string path = doc.PathName;

//            XYZ projectBasePointImp = BasePoint.GetProjectBasePoint(doc).Position;
//            XYZ projectBasePointMetric = doc.Application.Create.NewXYZ(Converters.ConvertValueToMillimeter(projectBasePointImp.X),
//                                                                       Converters.ConvertValueToMillimeter(projectBasePointImp.Y),
//                                                                       Converters.ConvertValueToMillimeter(projectBasePointImp.Z));
//            string coordinateString = projectBasePointMetric.X + "," + projectBasePointMetric.Y + "," + projectBasePointMetric.Z;

//            data.Add("Name", titleName);
//            data.Add("Path", path);
//            data.Add("ISSVersion", "1.0.0");

//            data.Add("GlobalReferenceCoordinate", coordinateString);

//            Application application = doc.Application;
//            data.Add("ModelAuthoringTool", application.Product.ToString());
//            data.Add("ModelAuthoringToolVersion", application.VersionName + " - " + application.VersionNumber);

//            structuralModelList.Add(data);

//            return structuralModelList;

//        }
//    }
//}

