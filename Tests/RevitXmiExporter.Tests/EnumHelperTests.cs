using Betekk.RevitXmiExporter.Utils;
using XmiSchema.Entities.Bases;
using Xunit;

namespace RevitXmiExporter.Tests;

/// <summary>
/// Test enum with EnumValueAttribute for testing EnumHelper
/// </summary>
public enum TestMaterialType
{
    [EnumValue("steel")]
    Steel,

    [EnumValue("concrete")]
    Concrete,

    [EnumValue("timber")]
    Timber
}

public class EnumHelperTests
{
    [Fact]
    public void FromEnumValue_WithValidValue_ReturnsCorrectEnum()
    {
        var result = EnumHelper.FromEnumValue<TestMaterialType>("steel");

        Assert.NotNull(result);
        Assert.Equal(TestMaterialType.Steel, result);
    }

    [Fact]
    public void FromEnumValue_WithAnotherValidValue_ReturnsCorrectEnum()
    {
        var result = EnumHelper.FromEnumValue<TestMaterialType>("concrete");

        Assert.NotNull(result);
        Assert.Equal(TestMaterialType.Concrete, result);
    }

    [Fact]
    public void FromEnumValue_WithInvalidValue_ReturnsNull()
    {
        var result = EnumHelper.FromEnumValue<TestMaterialType>("nonexistent_material");

        Assert.Null(result);
    }

    [Fact]
    public void FromEnumValue_IsCaseInsensitive()
    {
        var lowercase = EnumHelper.FromEnumValue<TestMaterialType>("steel");
        var uppercase = EnumHelper.FromEnumValue<TestMaterialType>("STEEL");
        var mixedCase = EnumHelper.FromEnumValue<TestMaterialType>("Steel");

        Assert.Equal(lowercase, uppercase);
        Assert.Equal(uppercase, mixedCase);
    }

    [Fact]
    public void FromEnumValue_WithEmptyString_ReturnsNull()
    {
        var result = EnumHelper.FromEnumValue<TestMaterialType>("");

        Assert.Null(result);
    }

    [Fact]
    public void FromEnumValue_WithWhitespace_ReturnsNull()
    {
        var result = EnumHelper.FromEnumValue<TestMaterialType>("   ");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("timber", TestMaterialType.Timber)]
    [InlineData("TIMBER", TestMaterialType.Timber)]
    [InlineData("Timber", TestMaterialType.Timber)]
    public void FromEnumValue_AllCasesWork(string input, TestMaterialType expected)
    {
        var result = EnumHelper.FromEnumValue<TestMaterialType>(input);

        Assert.NotNull(result);
        Assert.Equal(expected, result);
    }
}
