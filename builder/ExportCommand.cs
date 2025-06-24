using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Test;
using Revit_to_XMI.utils;
using JsonExporter;
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
            string lastExportPath = null;
            try
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

                lastExportPath = saveDialog.FileName;

                // 设置日志目录
                Revit_to_XMI.utils.ModelInfoBuilder.SetLogDirectory(Path.GetDirectoryName(lastExportPath));

                // 获取路径（不带扩展名）
                string basePath = Path.Combine(
                    Path.GetDirectoryName(saveDialog.FileName),
                    Path.GetFileNameWithoutExtension(saveDialog.FileName));

                JsonExporter.JsonExporter exporter = new JsonExporter.JsonExporter();
                string json1 = exporter.Export(doc);
                string path1 = basePath + "_xmi_export.json";
                File.WriteAllText(path1, json1, Encoding.UTF8);

                Autodesk.Revit.UI.TaskDialog td = new Autodesk.Revit.UI.TaskDialog("导出成功");
                td.MainInstruction = "已成功导出文件。";
                td.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Revit_to_XMI.utils.ModelInfoBuilder.WriteErrorLogToFile($"[ExportCommand] Top-level Error: {ex}");
                Autodesk.Revit.UI.TaskDialog.Show("错误", "导出过程中发生异常，详情请查看 error_log.txt");
                throw;
            }
        }
    }
}