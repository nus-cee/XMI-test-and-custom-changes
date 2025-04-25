using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Test;
using Utils;

//namespace RevittoXMI
//{
//    [Transaction(TransactionMode.Manual)]
//    public class ExportCommand : IExternalCommand
//    {
//        public Result Execute(
//            ExternalCommandData commandData,
//            ref string message,
//            ElementSet elements)
//        {
//            UIDocument uidoc = commandData.Application.ActiveUIDocument;
//            Document doc = uidoc.Document;

//            SaveFileDialog saveDialog = new SaveFileDialog
//            {
//                Title = "请选择导出位置",
//                FileName = "StructuredAnalyticalModel.json",
//                DefaultExt = "json",
//                Filter = "JSON 文件 (*.json)|*.json"
//            };

//            if (saveDialog.ShowDialog() != DialogResult.OK)
//            {
//                Autodesk.Revit.UI.TaskDialog.Show("操作取消", "未选择导出路径，操作已取消。");
//                return Result.Cancelled;
//            }

//            string savePath = saveDialog.FileName;

//            string json = EntitiesAnalyzer.GenerateStructuredModelJson(doc);
//            File.WriteAllText(savePath, json, Encoding.UTF8);

//            Autodesk.Revit.UI.TaskDialog.Show("导出成功", $"已将分析模型分类导出为 JSON 文件：\n{savePath}");
//            return Result.Succeeded;
//        }

//    }
//}
namespace RevittoXMI
{
    [Transaction(TransactionMode.Manual)]
    public class ExportCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Title = "请选择导出位置（将导出两个文件）",
                FileName = "StructuredAnalyticalModel.json",
                DefaultExt = "json",
                Filter = "JSON 文件 (*.json)|*.json"
            };

            if (saveDialog.ShowDialog() != DialogResult.OK)
            {
                Autodesk.Revit.UI.TaskDialog.Show("操作取消", "未选择导出路径，操作已取消。");
                return Result.Cancelled;
            }

            // 获取路径（不带扩展名）
            string basePath = Path.Combine(
                Path.GetDirectoryName(saveDialog.FileName),
                Path.GetFileNameWithoutExtension(saveDialog.FileName));

            string json1 = JsonBuilder.BuildJson(doc);
            string path1 = basePath + "_xmi_export.json";
            File.WriteAllText(path1, json1, Encoding.UTF8);

            string json2 = TestJsonGenerator.GenerateStructuredModelJson(doc);
            string path2 = basePath + "_test.json";
            File.WriteAllText(path2, json2, Encoding.UTF8);


            Autodesk.Revit.UI.TaskDialog.Show("导出成功", $"已成功导出以下两个文件：\n\n{path1}\n{path2}");
            return Result.Succeeded;
        }
    }
}