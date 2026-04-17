namespace MpvNet.Summary;

/// <summary>
/// 摘要候选链接信息
/// </summary>
public sealed record SummaryCandidate(string Kind, string Url);

/// <summary>
/// 摘要获取结果
/// </summary>
public sealed record SummaryFetchResult(bool IsSuccess, string? Markdown, string? Error);

/// <summary>
/// 摘要视图模型
/// </summary>
public sealed record VideoSummaryViewModel(string MediaPath, IReadOnlyList<VideoSummarySectionViewModel> Sections)
{
    /// <summary>
    /// 是否应该显示侧边栏
    /// </summary>
    public bool ShouldShowSidebar => Sections.Count > 0;

    /// <summary>
    /// 构建 seek 命令
    /// </summary>
    public static string BuildSeekCommand(int seconds) => $"seek {seconds} absolute exact";
}

/// <summary>
/// 视频摘要部分视图模型
/// </summary>
public sealed record VideoSummarySectionViewModel(string Kind, string Url, IReadOnlyList<SummaryBlock> Blocks);

/// <summary>
/// 摘要块基类
/// </summary>
public abstract record SummaryBlock;

/// <summary>
/// 标题块
/// </summary>
public sealed record SummaryHeadingBlock(int Level, IReadOnlyList<SummaryInline> Inlines) : SummaryBlock;

/// <summary>
/// 段落块
/// </summary>
public sealed record SummaryParagraphBlock(IReadOnlyList<SummaryInline> Inlines) : SummaryBlock;

/// <summary>
/// 列表块
/// </summary>
public sealed record SummaryListBlock(IReadOnlyList<IReadOnlyList<SummaryInline>> Items) : SummaryBlock;

/// <summary>
/// 代码块
/// </summary>
public sealed record SummaryCodeBlock(string Text) : SummaryBlock;

/// <summary>
/// 行内元素基类
/// </summary>
public abstract record SummaryInline;

/// <summary>
/// 文本行内元素
/// </summary>
public sealed record SummaryTextInline(string Text, bool IsBold = false) : SummaryInline;

/// <summary>
/// 链接行内元素
/// </summary>
public sealed record SummaryLinkInline(string Text, string Url) : SummaryInline;

/// <summary>
/// 时间戳行内元素
/// </summary>
public sealed record SummaryTimestampInline(string Text, int Seconds, bool IsBold = false) : SummaryInline;
