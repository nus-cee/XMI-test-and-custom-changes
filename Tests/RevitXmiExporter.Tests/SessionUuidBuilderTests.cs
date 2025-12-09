using Betekk.RevitXmiExporter.Utils;
using Xunit;

namespace RevitXmiExporter.Tests;

public class SessionUuidBuilderTests
{
    [Fact]
    public void SessionUuid_ReturnsValidGuidFormat()
    {
        var sessionUuid = SessionUuidBuilder.SessionUuid;

        Assert.True(Guid.TryParse(sessionUuid, out _), "SessionUuid should be a valid GUID format");
    }

    [Fact]
    public void SessionUuid_ReturnsSameValueOnMultipleCalls()
    {
        var firstCall = SessionUuidBuilder.SessionUuid;
        var secondCall = SessionUuidBuilder.SessionUuid;
        var thirdCall = SessionUuidBuilder.SessionUuid;

        Assert.Equal(firstCall, secondCall);
        Assert.Equal(secondCall, thirdCall);
    }

    [Fact]
    public void SessionUuid_IsNotEmpty()
    {
        var sessionUuid = SessionUuidBuilder.SessionUuid;

        Assert.False(string.IsNullOrWhiteSpace(sessionUuid));
        Assert.NotEqual(Guid.Empty.ToString(), sessionUuid);
    }

    [Fact]
    public void SessionUuid_HasCorrectLength()
    {
        var sessionUuid = SessionUuidBuilder.SessionUuid;

        // Standard GUID string format: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx (36 chars)
        Assert.Equal(36, sessionUuid.Length);
    }
}