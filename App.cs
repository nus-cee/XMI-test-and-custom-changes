using System.Windows;
using System.Windows.Media;
using Autodesk.Revit.UI;

namespace Betekk.RevitXmiExporter
{
    /// <summary>
    /// Revit entry point that registers the XMI-Schema ribbon tab and buttons, including the
    /// ExportXmi command. ExportXmi delegates to <c>Betekk.RevitXmiExporter.Builder.BetekkExportCommand</c>,
    /// which gathers model data through <c>BetekkXmiBuilder</c>, serializes it with <c>BetekkJsonExporter</c>,
    /// and writes the JSON to disk.
    /// </summary>
    public class App : IExternalApplication
    {
        private const string RibbonTab = "XMI Schema";
        private const string RibbonPanel = "Export XMI";

        /// <summary>
        /// Creates the XMI-Schema ribbon tab/panel in Revit and wires the ExportXmi and
        /// SegmentTests buttons to their corresponding external commands, applying the generated
        /// icon assets for visual clarity.
        /// </summary>
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
                    "Export XMI",
                    typeof(App).Assembly.Location,
                    "Betekk.RevitXmiExporter.Builder.BetekkRevitModelToXmiExportCommand"
                )
                {
                    ToolTip = "Export the revit data into XmiSchema (JSON)",
                    LargeImage = largeIcon,
                    Image = smallIcon
                };

                panel.AddItem(buttonData);
                // panel.AddItem(harnessButtonData);
                return Result.Succeeded;
            }
            catch
            {
                return Result.Failed;
            }
        }

        /// <summary>
        /// No-op shutdown handler; Revit invokes this when unloading the add-in. Stub kept for
        /// completeness in case future cleanup logic is required.
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        /// <summary>
        /// Builds a square vector icon that combines a data panel motif with an export arrow.
        /// </summary>
        /// <param name="size">Desired icon size in device-independent pixels.</param>
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

        /// <summary>
        /// Creates the arrow geometry layered on top of the export icon.
        /// </summary>
        /// <param name="size">Icon size to scale the arrow proportions.</param>
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
