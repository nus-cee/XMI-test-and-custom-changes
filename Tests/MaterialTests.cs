using XmiSchema.Core.Entities;
using XmiSchema.Core.Enums;
using XmiSchema.Core.Manager;
using XmiSchema.Core.Models;
using Xunit;

namespace RevitXmiExporter.Tests;

public class MaterialTests
{
    [Fact]
    public void CreateMaterial_WithValidParameters_ReturnsMaterial()
    {
        // Arrange
        var manager = new XmiManager();
        manager.Models = new List<XmiModel> { new XmiModel() };
        int modelIndex = 0;

        // Act
        var material = manager.CreateMaterial(
            modelIndex,
            id: "mat-1",
            name: "Concrete C30/37",
            ifcGuid: "3P3Gs$u5Y8Ag9OPfx4GOr2",
            nativeId: "67890",
            description: "Structural concrete grade C30/37",
            materialType: XmiMaterialTypeEnum.Concrete,
            grade: 30.0,
            unitWeight: 2500.0,
            elasticModulus: "(30000, 30000, 30000)",
            shearModulus: "(12500, 12500, 12500)",
            poissonRatio: "(0.2, 0.2, 0.2)",
            thermalExpansionCoefficient: 0.00001
        );

        // Assert
        Assert.NotNull(material);
        Assert.Equal("mat-1", material.Id);
        Assert.Equal("Concrete C30/37", material.Name);
        Assert.Equal(XmiMaterialTypeEnum.Concrete, material.Type);
        Assert.Equal(30.0, material.Grade);
        Assert.Equal(2500.0, material.UnitWeight);
    }

    [Theory]
    [InlineData(XmiMaterialTypeEnum.Concrete)]
    [InlineData(XmiMaterialTypeEnum.Steel)]
    [InlineData(XmiMaterialTypeEnum.Timber)]
    [InlineData(XmiMaterialTypeEnum.Unknown)]
    public void CreateMaterial_WithDifferentTypes_CreatesSuccessfully(XmiMaterialTypeEnum materialType)
    {
        // Arrange
        var manager = new XmiManager();
        manager.Models = new List<XmiModel> { new XmiModel() };
        int modelIndex = 0;

        // Act
        var material = manager.CreateMaterial(
            modelIndex,
            $"mat-{materialType}",
            $"Material {materialType}",
            "3P3Gs$u5Y8Ag9OPfx4GOr3",
            "12345",
            $"Test material of type {materialType}",
            materialType,
            0.0,
            0.0,
            string.Empty,
            string.Empty,
            string.Empty,
            0.0
        );

        // Assert
        Assert.NotNull(material);
        Assert.Equal(materialType, material.Type);
    }

    [Fact]
    public void CreateMaterial_WithZeroGrade_AcceptsValue()
    {
        // Arrange
        var manager = new XmiManager();
        manager.Models = new List<XmiModel> { new XmiModel() };

        // Act
        var material = manager.CreateMaterial(
            0,
            "mat-zero",
            "Material with zero grade",
            "guid",
            "native-id",
            "desc",
            XmiMaterialTypeEnum.Unknown,
            grade: 0.0,
            0.0,
            string.Empty,
            string.Empty,
            string.Empty,
            0.0
        );

        // Assert
        Assert.Equal(0.0, material.Grade);
    }
}
