using XmiSchema.Core.Entities;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Models;
using Xunit;

namespace RevitXmiExporter.Tests;

public class StoreyTests
{
    [Fact]
    public void CreateStorey_WithValidParameters_ReturnsStorey()
    {
        // Arrange
        var manager = new XmiManager();
        manager.Models = new List<XmiModel> { new XmiModel() };
        int modelIndex = 0;

        // Act
        var storey = manager.CreateStorey(
            modelIndex,
            id: "storey-1",
            name: "Level 1",
            ifcGuid: "2O2Fr$t4X7Zf8NOew3FNr1",
            nativeId: "12345",
            description: "Ground floor level",
            storeyElevation: 0.0,
            storeyMass: 0.0
        );

        // Assert
        Assert.NotNull(storey);
        Assert.Equal("storey-1", storey.Id);
        Assert.Equal("Level 1", storey.Name);
        Assert.Equal("12345", storey.NativeId);
        Assert.Equal(0.0, storey.Elevation);
    }

    [Fact]
    public void CreateStorey_WithElevation_StoresCorrectValue()
    {
        // Arrange
        var manager = new XmiManager();
        manager.Models = new List<XmiModel> { new XmiModel() };
        int modelIndex = 0;
        double elevation = 3500.0; // 3.5 meters in mm

        // Act
        var storey = manager.CreateStorey(
            modelIndex,
            "storey-2",
            "Level 2",
            "2O2Fr$t4X7Zf8NOew3FNr2",
            "12346",
            "Second floor",
            elevation,
            0.0
        );

        // Assert
        Assert.Equal(elevation, storey.Elevation);
    }

    [Fact]
    public void XmiStorey_Constructor_CreatesValidStorey()
    {
        // Arrange & Act
        var storey = new XmiStorey(
            id: "test-storey",
            name: "Test Level",
            ifcGuid: "2O2Fr$t4X7Zf8NOew3FNr3",
            nativeId: "54321",
            description: "Test storey description",
            storeyElevation: 1000.0,
            storeyMass: 5000.0
        );

        // Assert
        Assert.NotNull(storey);
        Assert.Equal("test-storey", storey.Id);
        Assert.Equal("Test Level", storey.Name);
        Assert.Equal(1000.0, storey.Elevation);
        Assert.Equal(5000.0, storey.Mass);
    }
}
