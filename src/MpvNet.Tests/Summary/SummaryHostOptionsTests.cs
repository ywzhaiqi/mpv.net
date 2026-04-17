using Xunit;
using MpvNet.Summary;

namespace MpvNet.Tests.Summary;

/// <summary>
/// SummaryHostOptions 测试类
/// </summary>
public class SummaryHostOptionsTests
{
    [Theory]
    [InlineData("", "https://pan2.871015.xyz/d/a.mp4", false)]
    [InlineData("pan2.871015.xyz", "https://pan2.871015.xyz/d/a.mp4", true)]
    [InlineData("pan2.871015.xyz", "https://other.example/d/a.mp4", false)]
    [InlineData("pan2.871015.xyz", "not-a-url", false)]
    public void MatchesSummaryHost_OnlyMatchesExactHost(string configuredHost, string path, bool expected)
    {
        var result = SummaryHostOptions.Matches(configuredHost, path);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_UsesDefaultHostWhenConfigMissing()
    {
        Assert.Equal("pan2.871015.xyz", SummaryHostOptions.Normalize(null, useDefaultWhenEmpty: true));
    }

    [Fact]
    public void Normalize_AllowsExplicitEmptyToDisableFeature()
    {
        Assert.Equal("", SummaryHostOptions.Normalize("", useDefaultWhenEmpty: false));
    }
}
