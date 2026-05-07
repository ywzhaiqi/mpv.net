namespace MpvNet.Summary;

/// <summary>
/// 视频摘要路径解析器
/// </summary>
public static class VideoSummaryPathResolver
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".webm", ".mov", ".avi", ".ts"
    };

    /// <summary>
    /// 构建摘要候选链接
    /// </summary>
    /// <param name="mediaPath">媒体路径</param>
    /// <param name="configuredHost">配置的主机名</param>
    /// <returns>候选链接列表</returns>
    public static IReadOnlyList<SummaryCandidate> BuildCandidates(string? mediaPath, string configuredHost)
    {
        if (!SummaryHostOptions.Matches(configuredHost, mediaPath))
            return [];

        var uri = new Uri(mediaPath!, UriKind.Absolute);
        
        // 移除末尾的斜杠，处理 ...mp4/ 这种情况
        var absolutePath = uri.AbsolutePath.TrimEnd('/');
        var extension = Path.GetExtension(absolutePath);

        if (!VideoExtensions.Contains(extension))
            return [];

        var fileName = Path.GetFileNameWithoutExtension(absolutePath);
        var directory = absolutePath[..absolutePath.LastIndexOf('/')];

        return
        [
            new("summary", $"{uri.Scheme}://{uri.Authority}{directory}/summary/{fileName}.md"),
            new("summary-bd", $"{uri.Scheme}://{uri.Authority}{directory}/summary/{fileName}_bd.md")
        ];
    }

    /// <summary>
    /// 检查是否为缺失摘要的响应内容
    /// </summary>
    /// <param name="body">响应体</param>
    /// <param name="mediaType">媒体类型</param>
    /// <returns>是否为缺失摘要</returns>
    public static bool IsMissingSummaryPayload(string? body, string? mediaType) =>
        !string.IsNullOrWhiteSpace(body) &&
        (mediaType ?? "").Contains("json", StringComparison.OrdinalIgnoreCase) &&
        body.Contains("\"code\":500", StringComparison.OrdinalIgnoreCase) &&
        body.Contains("object not found", StringComparison.OrdinalIgnoreCase);
}
