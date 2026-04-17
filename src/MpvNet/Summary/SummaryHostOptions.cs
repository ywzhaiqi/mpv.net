namespace MpvNet.Summary;

/// <summary>
/// 摘要主机配置选项
/// </summary>
public static class SummaryHostOptions
{
    /// <summary>
    /// 检查路径是否匹配配置的摘要主机
    /// </summary>
    /// <param name="configuredHost">配置的主机名</param>
    /// <param name="path">要检查的路径</param>
    /// <returns>是否匹配</returns>
    public static bool Matches(string configuredHost, string? path)
    {
        if (string.IsNullOrWhiteSpace(configuredHost) || string.IsNullOrWhiteSpace(path))
            return false;

        if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
            return false;

        return string.Equals(uri.Host, configuredHost, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 规范化摘要主机配置值
    /// </summary>
    /// <param name="configuredHost">配置的主机名</param>
    /// <param name="useDefaultWhenEmpty">空值时是否使用默认值</param>
    /// <returns>规范化后的主机名</returns>
    public static string Normalize(string? configuredHost, bool useDefaultWhenEmpty)
    {
        var value = (configuredHost ?? "").Trim();

        if (value.Length == 0)
            return useDefaultWhenEmpty ? "pan2.871015.xyz" : "";

        return value;
    }
}
