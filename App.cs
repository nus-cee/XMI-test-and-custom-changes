using System.Windows;
using System.Windows.Media;
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

                ImageSource largeIcon = CreateExportIcon(32);
                ImageSource smallIcon = CreateExportIcon(16);

                PushButtonData buttonData = new PushButtonData(
                    "ExportStructureBtn",
                    "ExportJson",
                    typeof(App).Assembly.Location,
                    "Betekk.RevitXmiExporter.ExportCommand"
                )
                {
                    ToolTip = "Export the structural data set to JSON",
                    LargeImage = largeIcon,
                    Image = smallIcon
                };

                PushButtonData harnessButtonData = new PushButtonData(
                    "SegmentHarnessBtn",
                    "SegmentTests",
                    typeof(App).Assembly.Location,
                    "Betekk.RevitXmiExporter.StructuralSegmentHarnessCommand")
                {
                    ToolTip = "Run StructuralSegmentMapper smoke tests and report the results"
                };

                panel.AddItem(buttonData);
                panel.AddItem(harnessButtonData);
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

        private static ImageSource CreateExportIcon(double size)
        {
            SolidColorBrush backgroundBrush = new SolidColorBrush(Color.FromRgb(19, 68, 116));
            backgroundBrush.Freeze();

            SolidColorBrush panelBrush = new SolidColorBrush(Color.FromRgb(100, 181, 246));
            panelBrush.Freeze();

            SolidColorBrush accentBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            accentBrush.Freeze();

            DrawingGroup drawingGroup = new DrawingGroup();
            drawingGroup.Children.Add(new GeometryDrawing(
                backgroundBrush,
                null,
                new RectangleGeometry(new Rect(0, 0, size, size))));

            double panelWidth = size * 0.65;
            double panelHeight = size * 0.35;
            double panelLeft = (size - panelWidth) / 2d;
            double panelTop = size * 0.15;

            drawingGroup.Children.Add(new GeometryDrawing(
                panelBrush,
                null,
                new RectangleGeometry(new Rect(panelLeft, panelTop, panelWidth, panelHeight))));

            double lineMargin = panelWidth * 0.12;
            double lineWidth = panelWidth - (2 * lineMargin);
            double lineHeight = size * 0.05;
            double firstLineTop = panelTop + lineHeight * 0.3;
            double secondLineTop = firstLineTop + lineHeight + (size * 0.04);

            drawingGroup.Children.Add(new GeometryDrawing(
                accentBrush,
                null,
                new RectangleGeometry(new Rect(panelLeft + lineMargin, firstLineTop, lineWidth, lineHeight))));

            drawingGroup.Children.Add(new GeometryDrawing(
                accentBrush,
                null,
                new RectangleGeometry(new Rect(panelLeft + lineMargin, secondLineTop, lineWidth * 0.75, lineHeight))));

            drawingGroup.Children.Add(new GeometryDrawing(
                accentBrush,
                null,
                BuildArrowGeometry(size)));

            DrawingImage image = new DrawingImage(drawingGroup);
            image.Freeze();
            return image;
        }

        private static Geometry BuildArrowGeometry(double size)
        {
            double centerX = size / 2d;
            double tipY = size * 0.45;
            double headY = size * 0.65;
            double bottomY = size * 0.9;
            double halfShaft = size * 0.08;
            double headHalfWidth = size * 0.18;

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(centerX, tipY), true, true);
                ctx.LineTo(new Point(centerX + headHalfWidth, headY), true, false);
                ctx.LineTo(new Point(centerX + halfShaft, headY), true, false);
                ctx.LineTo(new Point(centerX + halfShaft, bottomY), true, false);
                ctx.LineTo(new Point(centerX - halfShaft, bottomY), true, false);
                ctx.LineTo(new Point(centerX - halfShaft, headY), true, false);
                ctx.LineTo(new Point(centerX - headHalfWidth, headY), true, false);
            }

            geometry.Freeze();
            return geometry;
        }
    }
}
