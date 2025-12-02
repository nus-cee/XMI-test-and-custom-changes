namespace Betekk.RevitXmiExporter.Utils
{
    public static class Converters
    {
        public static double ConvertValueToMillimeter(double imperialUnitValue)
        {
            return System.Math.Round(imperialUnitValue * 304.8, 1);
        }

        public static double SquareFeetToSquareMillimeter(double squareFeet)
        {
            return squareFeet * 92903.04; // 1 ft^2 = 92,903.04 mm^2
        }

        public static double KilogramsPerCubicFootToKilogramsPerCubicMeter(double kgPerFt3)
        {
            return kgPerFt3 / 0.0283168; // 1 ft^3 = 0.0283168 m^3
        }
    }
}
