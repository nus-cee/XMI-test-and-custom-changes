using XmiSchema.Core.Parameters;
using Xunit;

namespace RevitXmiExporter.Tests;

public class CrossSectionMapperTests
{
    [Fact]
    public void RectangularShapeParameters_WithValidDimensions_CreatesSuccessfully()
    {
        // Arrange
        double height = 300.0; // mm
        double width = 200.0;  // mm

        // Act
        var shapeParams = new RectangularShapeParameters(height, width);

        // Assert
        Assert.NotNull(shapeParams);
        Assert.IsAssignableFrom<IXmiShapeParameters>(shapeParams);
    }

    [Fact]
    public void UnknownShapeParameters_WithEmptyDictionary_CreatesSuccessfully()
    {
        // Arrange
        var paramDict = new Dictionary<string, double>();

        // Act
        var shapeParams = new UnknownShapeParameters(paramDict);

        // Assert
        Assert.NotNull(shapeParams);
        Assert.IsAssignableFrom<IXmiShapeParameters>(shapeParams);
    }

    [Fact]
    public void UnknownShapeParameters_WithWidthAndHeight_CreatesSuccessfully()
    {
        // Arrange
        var paramDict = new Dictionary<string, double>
        {
            { "width", 200.0 },
            { "height", 300.0 }
        };

        // Act
        var shapeParams = new UnknownShapeParameters(paramDict);

        // Assert
        Assert.NotNull(shapeParams);
        Assert.IsAssignableFrom<IXmiShapeParameters>(shapeParams);
    }

    [Theory]
    [InlineData(0, 0, typeof(UnknownShapeParameters))]
    [InlineData(100, 0, typeof(UnknownShapeParameters))]
    [InlineData(0, 200, typeof(UnknownShapeParameters))]
    [InlineData(100, 200, typeof(RectangularShapeParameters))]
    public void ShapeParameters_CorrectTypeBasedOnDimensions(double width, double height, Type expectedType)
    {
        // Arrange & Act
        IXmiShapeParameters shapeParameters;
        if (width > 0 && height > 0)
        {
            shapeParameters = new RectangularShapeParameters(height, width);
        }
        else
        {
            var paramDict = new Dictionary<string, double>();
            if (width > 0) paramDict["width"] = width;
            if (height > 0) paramDict["height"] = height;
            shapeParameters = new UnknownShapeParameters(paramDict);
        }

        // Assert
        Assert.IsType(expectedType, shapeParameters);
    }
}
