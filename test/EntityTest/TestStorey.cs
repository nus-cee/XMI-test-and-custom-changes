using XmiSchema.Core.Entities;

namespace Betekk.RevitXmiExporter.Test.EntityTest;

public static class TestStorey
{
    public static XmiStorey Dummy => new XmiStorey(
        "Optional", // id
        "Optional", // name
        "Optional", // ifcGuid
        "Optional", // nativeId
        "Optional", // description
        0.0,        // elevation
        0.0);       // storeyMass
}
