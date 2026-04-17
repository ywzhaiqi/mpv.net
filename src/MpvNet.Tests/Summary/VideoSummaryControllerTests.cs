using Xunit;
using MpvNet.Summary;

namespace MpvNet.Tests.Summary;

/// <summary>
/// 假摘要客户端，用于测试
/// </summary>
public class FakeSummaryClient : IVideoSummaryClient
{
    private readonly Dictionary<string, string> _responses;

    public FakeSummaryClient(params (string url, string content)[] responses)
    {
        _responses = responses.ToDictionary(r => r.url, r => r.content);
    }

    public System.Threading.Tasks.Task<SummaryFetchResult> FetchAsync(
        SummaryCandidate candidate, 
        System.Threading.CancellationToken cancellationToken)
    {
        if (_responses.TryGetValue(candidate.Url, out var content))
        {
            return System.Threading.Tasks.Task.FromResult(new SummaryFetchResult(true, content, null));
        }

        return System.Threading.Tasks.Task.FromResult(new SummaryFetchResult(false, null, "Not found"));
    }
}

/// <summary>
/// VideoSummaryController 测试类
/// </summary>
public class VideoSummaryControllerTests
{
    [Fact]
    public async System.Threading.Tasks.Task LoadAsync_ReturnsTwoSectionsWhenBothSummariesExist()
    {
        var client = new FakeSummaryClient(
            ("https://pan2.871015.xyz/d/a/b/summary/c.md", "# A"),
            ("https://pan2.871015.xyz/d/a/b/summary/c_bd.md", "# B"));

        var controller = new VideoSummaryController(client);
        var result = await controller.LoadAsync("https://pan2.871015.xyz/d/a/b/c.mp4", "pan2.871015.xyz", System.Threading.CancellationToken.None);

        Assert.True(result.ShouldShowSidebar);
        Assert.Equal(2, result.Sections.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadAsync_ReturnsHiddenWhenAllCandidatesMissing()
    {
        var client = new FakeSummaryClient();
        var controller = new VideoSummaryController(client);
        var result = await controller.LoadAsync("https://pan2.871015.xyz/d/a/b/c.mp4", "pan2.871015.xyz", System.Threading.CancellationToken.None);

        Assert.False(result.ShouldShowSidebar);
        Assert.Empty(result.Sections);
    }

    [Theory]
    [InlineData(10, "seek 10 absolute exact")]
    [InlineData(5025, "seek 5025 absolute exact")]
    public void BuildSeekCommand_UsesAbsoluteExact(int seconds, string expected)
    {
        Assert.Equal(expected, VideoSummaryViewModel.BuildSeekCommand(seconds));
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadAsync_ReturnsEmptyForEmptyPath()
    {
        var client = new FakeSummaryClient();
        var controller = new VideoSummaryController(client);
        var result = await controller.LoadAsync("", "pan2.871015.xyz", System.Threading.CancellationToken.None);

        Assert.False(result.ShouldShowSidebar);
        Assert.Empty(result.Sections);
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadAsync_ReturnsEmptyForNullPath()
    {
        var client = new FakeSummaryClient();
        var controller = new VideoSummaryController(client);
        var result = await controller.LoadAsync(null, "pan2.871015.xyz", System.Threading.CancellationToken.None);

        Assert.False(result.ShouldShowSidebar);
        Assert.Empty(result.Sections);
    }
}
