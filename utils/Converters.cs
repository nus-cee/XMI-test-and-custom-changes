using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils
{
    public class Converters
    {
        public static double ConvertValueToMillimeter(double ImperialUnitValue) //Imperial to Metric converter
        {
            return System.Math.Round(ImperialUnitValue * 304.8, 1);  //to 1 decimal places
        }
        public static double SquareFeetToSquareMillimeter(double squareFeet)
        {
            return squareFeet * 92903.04; // 1 ft² = 92903.04 mm²
        }
        public static double KilogramsPerCubicFootToKilogramsPerCubicMeter(double kgPerFt3)
        {
            return kgPerFt3 / 0.0283168; // 1 kg/ft³ -> 0.0283168 kg/m³
        }
    }
}
    