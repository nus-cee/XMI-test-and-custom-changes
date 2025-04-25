//using Autodesk.Revit.DB;
//using Autodesk.Revit.ApplicationServices;
//using Converters;

//namespace ClassMapper
//{
//    public static class StructuralAreaSupportMapper
//    {
//        public static List<Dictionary<string, object>> GetStructuralAreaSupport(Document doc)
//        {
//            List<Dictionary<String, Object>> structuralAreaSupportList = new List<Dictionary<String, Object>>();

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

//            structuralAreaSupportList.Add(data);

//            return structuralAreaSupportList;

//        }
//    }
//}

