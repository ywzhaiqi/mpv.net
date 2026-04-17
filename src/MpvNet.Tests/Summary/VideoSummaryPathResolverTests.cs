using Xunit;
using MpvNet.Summary;

namespace MpvNet.Tests.Summary;

/// <summary>
/// VideoSummaryPathResolver 测试类
/// </summary>
public class VideoSummaryPathResolverTests
{
    [Fact]
    public void BuildCandidates_BuildsMdAndBdUrlsForVideoUrl()
    {
        var candidates = VideoSummaryPathResolver.BuildCandidates(
            "https://pan2.871015.xyz/d/a/b/c.mp4",
            "pan2.871015.xyz");

        Assert.Collection(candidates,
            item => Assert.Equal("https://pan2.871015.xyz/d/a/b/summary/c.md", item.Url),
            item => Assert.Equal("https://pan2.871015.xyz/d/a/b/summary/c_bd.md", item.Url));
    }

    [Fact]
    public void BuildCandidates_ReturnsEmptyForHostMismatch()
    {
        Assert.Empty(VideoSummaryPathResolver.BuildCandidates(
            "https://other.example/d/a/b/c.mp4",
            "pan2.871015.xyz"));
    }

    [Fact]
    public void IsMissingSummaryPayload_ReturnsTrueForObjectNotFoundJson()
    {
        const string payload = "{\"code\":500,\"message\":\"failed link: failed to get file: object not found\",\"data\":null}";
        Assert.True(VideoSummaryPathResolver.IsMissingSummaryPayload(payload, "application/json"));
    }

    [Fact]
    public void IsMissingSummaryPayload_ReturnsFalseForValidMarkdown()
    {
        const string payload = "# Summary\n\nThis is a valid summary.";
        Assert.False(VideoSummaryPathResolver.IsMissingSummaryPayload(payload, "text/markdown"));
    }

    [Fact]
    public void BuildCandidates_ReturnsEmptyForNonVideoExtension()
    {
        Assert.Empty(VideoSummaryPathResolver.BuildCandidates(
            "https://pan2.871015.xyz/d/a/b/c.txt",
            "pan2.871015.xyz"));
    }
}
