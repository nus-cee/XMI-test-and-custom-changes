using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using Newtonsoft.Json;

namespace Betekk.RevitXmiExporter.Utils
{
    public static class ModelInfoBuilder
    {
        private static string _logDirectory = Directory.GetCurrentDirectory();

        public static void SetLogDirectory(string directory)
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _logDirectory = directory;
            }
        }

        public static string BuildModelInfoJson(Document doc)
        {
            Dictionary<string, object> modelInfo = new Dictionary<string, object>
            {
                ["Name"] = doc.Title,
                ["Path"] = doc.PathName,
                ["ISSVersion"] = "1.0.0"
            };

            XYZ basePoint = BasePoint.GetProjectBasePoint(doc).Position;
            XYZ metric = doc.Application.Create.NewXYZ(
                ConvertValueToMillimeter(basePoint.X),
                ConvertValueToMillimeter(basePoint.Y),
                ConvertValueToMillimeter(basePoint.Z));

            modelInfo["GlobalReferenceCoordinate"] = $"{metric.X},{metric.Y},{metric.Z}";
            modelInfo["ModelAuthoringTool"] = doc.Application.Product.ToString();
            modelInfo["ModelAuthoringToolVersion"] = $"{doc.Application.VersionName} - {doc.Application.VersionNumber}";

            List<Dictionary<string, object>> modelList = new List<Dictionary<string, object>> { modelInfo };
            return "\"StructuralModel\": " + JsonConvert.SerializeObject(modelList, Formatting.Indented);
        }

        private static double ConvertValueToMillimeter(double value)
        {
            return UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.Millimeters);
        }

        public static void WriteErrorLogToFile(string errorMessage)
        {
            Directory.CreateDirectory(_logDirectory);
            string logPath = Path.Combine(_logDirectory, "error_log.txt");
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {errorMessage}{Environment.NewLine}";
            File.AppendAllText(logPath, logEntry);
        }
    }
}
