using Autodesk.Revit.UI;

namespace RevittoXMI
{
    public class App : IExternalApplication
    {
        private const string RIBBON_TAB = "tang_plugin";
        private const string RIBBON_PANEL = "结构工具";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // 创建选项卡
                try { application.CreateRibbonTab(RIBBON_TAB); }
                catch { } // 如果已存在就忽略

                // 创建面板
                RibbonPanel panel = application.CreateRibbonPanel(RIBBON_TAB, RIBBON_PANEL);

                // 创建按钮
                PushButtonData buttonData = new PushButtonData(
                    "ExportStructureBtn",               // 按钮 ID
                    "结构导出",                          // 按钮名称
                    typeof(App).Assembly.Location,     // DLL 路径
                    "RevittoXMI.ExportCommand"         // 点击按钮调用的命令类
                );

                buttonData.ToolTip = "导出结构数据到 JSON";
                panel.AddItem(buttonData); // 加到面板中

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
