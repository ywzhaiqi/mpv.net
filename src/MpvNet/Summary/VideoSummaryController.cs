namespace MpvNet.Summary;

/// <summary>
/// 视频摘要控制器
/// </summary>
public class VideoSummaryController
{
    private readonly IVideoSummaryClient _client;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="client">摘要客户端</param>
    public VideoSummaryController(IVideoSummaryClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 异步加载摘要
    /// </summary>
    /// <param name="mediaPath">媒体路径</param>
    /// <param name="summaryHost">摘要主机</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>摘要视图模型</returns>
    public async System.Threading.Tasks.Task<VideoSummaryViewModel> LoadAsync(
        string? mediaPath, 
        string summaryHost, 
        System.Threading.CancellationToken cancellationToken)
    {
        Terminal.WriteError($"[SummaryController] LoadAsync called with path: {mediaPath}, host: {summaryHost}");

        if (string.IsNullOrWhiteSpace(mediaPath))
        {
            Terminal.WriteError("[SummaryController] mediaPath is null or empty, returning empty result");
            return new VideoSummaryViewModel("", []);
        }

        var candidates = VideoSummaryPathResolver.BuildCandidates(mediaPath, summaryHost);
        Terminal.WriteError($"[SummaryController] Found {candidates.Count} candidates");

        foreach (var c in candidates)
        {
            Terminal.WriteError($"[SummaryController] Candidate: {c.Kind} -> {c.Url}");
        }

        var sections = new List<VideoSummarySectionViewModel>();

        foreach (var candidate in candidates)
        {
            Terminal.WriteError($"[SummaryController] Fetching: {candidate.Url}");
            var fetch = await _client.FetchAsync(candidate, cancellationToken);
            Terminal.WriteError($"[SummaryController] Fetch result: IsSuccess={fetch.IsSuccess}, HasMarkdown={!string.IsNullOrWhiteSpace(fetch.Markdown)}, Error={fetch.Error}");

            if (!fetch.IsSuccess || string.IsNullOrWhiteSpace(fetch.Markdown))
                continue;

            var blocks = SummaryMarkdownParser.Parse(fetch.Markdown);
            Terminal.WriteError($"[SummaryController] Parsed {blocks.Count} blocks");
            sections.Add(new VideoSummarySectionViewModel(candidate.Kind, candidate.Url, blocks));
        }

        Terminal.WriteError($"[SummaryController] Returning {sections.Count} sections");
        return new VideoSummaryViewModel(mediaPath, sections);
    }
}
