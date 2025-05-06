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
    }
}
    