using Autodesk.Revit.UI;

namespace Betekk.RevitXmiExporter
{
    public class App : IExternalApplication
    {
        private const string RibbonTab = "XMI-Schema";
        private const string RibbonPanel = "ExportJson";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                try { application.CreateRibbonTab(RibbonTab); }
                catch { }

                RibbonPanel panel = application.CreateRibbonPanel(RibbonTab, RibbonPanel);

                PushButtonData buttonData = new PushButtonData(
                    "ExportStructureBtn",
                    "ExportJson",
                    typeof(App).Assembly.Location,
                    "Betekk.RevitXmiExporter.ExportCommand"
                )
                {
                    ToolTip = "Export the structural data set to JSON"
                };

                panel.AddItem(buttonData);
                return Result.Succeeded;
            }
            catch
            {
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
