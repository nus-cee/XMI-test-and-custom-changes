using Betekk.RevitXmiExporter.Utils;
using Xunit;

namespace RevitXmiExporter.Tests;

public class ConvertersTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 304.8)]
    [InlineData(10, 3048)]
    [InlineData(0.5, 152.4)]
    [InlineData(-1, -304.8)]
    public void ConvertValueToMillimeter_ConvertsCorrectly(double feet, double expectedMm)
    {
        var result = Converters.ConvertValueToMillimeter(feet);

        Assert.Equal(expectedMm, result, precision: 1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 92903.04)]
    [InlineData(10, 929030.4)]
    [InlineData(0.5, 46451.52)]
    public void SquareFeetToSquareMillimeter_ConvertsCorrectly(double sqFeet, double expectedSqMm)
    {
        var result = Converters.SquareFeetToSquareMillimeter(sqFeet);

        Assert.Equal(expectedSqMm, result, precision: 2);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 35.3147)] // 1 kg/ft^3 = ~35.3147 kg/m^3
    [InlineData(10, 353.147)]
    public void KilogramsPerCubicFootToKilogramsPerCubicMeter_ConvertsCorrectly(double kgPerFt3, double expectedKgPerM3)
    {
        var result = Converters.KilogramsPerCubicFootToKilogramsPerCubicMeter(kgPerFt3);

        Assert.Equal(expectedKgPerM3, result, precision: 2);
    }

    [Fact]
    public void ConvertValueToMillimeter_RoundsToOneDecimalPlace()
    {
        // 1/3 foot = 101.6 mm (rounded from 101.6...)
        var result = Converters.ConvertValueToMillimeter(1.0 / 3.0);

        // Check that the result is rounded to 1 decimal place
        var roundedResult = Math.Round(result, 1);
        Assert.Equal(roundedResult, result);
    }
}