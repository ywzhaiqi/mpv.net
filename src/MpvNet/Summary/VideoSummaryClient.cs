namespace MpvNet.Summary;

/// <summary>
/// 视频摘要客户端接口
/// </summary>
public interface IVideoSummaryClient
{
    /// <summary>
    /// 异步获取摘要
    /// </summary>
    System.Threading.Tasks.Task<SummaryFetchResult> FetchAsync(SummaryCandidate candidate, System.Threading.CancellationToken cancellationToken);
}

/// <summary>
/// 视频摘要客户端实现
/// </summary>
public class VideoSummaryClient : IVideoSummaryClient
{
    private readonly System.Net.Http.HttpClient _httpClient;

    /// <summary>
    /// 构造函数
    /// </summary>
    public VideoSummaryClient(System.Net.Http.HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new System.Net.Http.HttpClient();
    }

    /// <inheritdoc />
    public async System.Threading.Tasks.Task<SummaryFetchResult> FetchAsync(SummaryCandidate candidate, System.Threading.CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(candidate.Url, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var mediaType = response.Content.Headers.ContentType?.MediaType;

            // 检查是否为缺失摘要的响应
            if (VideoSummaryPathResolver.IsMissingSummaryPayload(content, mediaType))
            {
                return new SummaryFetchResult(false, null, "Summary not found");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new SummaryFetchResult(false, null, $"HTTP error: {response.StatusCode}");
            }

            return new SummaryFetchResult(true, content, null);
        }
        catch (System.OperationCanceledException)
        {
            throw;
        }
        catch (System.Exception ex)
        {
            return new SummaryFetchResult(false, null, ex.Message);
        }
    }
}
