using System.Text.RegularExpressions;

namespace MpvNet.Summary;

/// <summary>
/// Markdown 摘要解析器
/// </summary>
public static class SummaryMarkdownParser
{
    private static readonly Regex TimestampRegex = new(@"(?<!\d)(?:\d{1,2}:)?\d{2}:\d{2}(?!\d)", RegexOptions.Compiled);
    private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

    /// <summary>
    /// 解析 Markdown 文本为摘要块列表
    /// </summary>
    /// <param name="markdown">Markdown 文本</param>
    /// <returns>摘要块列表</returns>
    public static IReadOnlyList<SummaryBlock> Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var blocks = new List<SummaryBlock>();
        var lines = markdown.Split('\n');
        var i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            // 跳过空行
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // 代码块
            if (line.TrimStart().StartsWith("```"))
            {
                var codeBlock = ParseCodeBlock(lines, ref i);
                if (codeBlock != null)
                    blocks.Add(codeBlock);
                continue;
            }

            // 标题
            if (line.TrimStart().StartsWith("#"))
            {
                var heading = ParseHeading(line);
                if (heading != null)
                    blocks.Add(heading);
                i++;
                continue;
            }

            // 列表
            if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
            {
                var listBlock = ParseList(lines, ref i);
                if (listBlock != null)
                    blocks.Add(listBlock);
                continue;
            }

            // 段落
            var paragraph = ParseParagraph(lines, ref i);
            if (paragraph != null)
                blocks.Add(paragraph);
        }

        return blocks;
    }

    /// <summary>
    /// 解析时间戳字符串为秒数
    /// </summary>
    /// <param name="value">时间戳字符串 (如 "01:23" 或 "01:23:45")</param>
    /// <returns>秒数</returns>
    public static int ParseTimestampSeconds(string value)
    {
        var parts = value.Split(':').Select(int.Parse).ToArray();
        return parts.Length == 2 
            ? parts[0] * 60 + parts[1] 
            : parts[0] * 3600 + parts[1] * 60 + parts[2];
    }

    private static SummaryCodeBlock? ParseCodeBlock(string[] lines, ref int index)
    {
        var startLine = lines[index];
        var language = startLine.Trim()[3..].Trim();
        var codeLines = new List<string>();
        index++;

        while (index < lines.Length && !lines[index].TrimStart().StartsWith("```"))
        {
            codeLines.Add(lines[index]);
            index++;
        }

        if (index < lines.Length)
            index++; // 跳过结束标记

        return new SummaryCodeBlock(string.Join("\n", codeLines));
    }

    private static SummaryHeadingBlock? ParseHeading(string line)
    {
        var trimmed = line.TrimStart();
        var level = 0;
        while (level < trimmed.Length && trimmed[level] == '#')
            level++;

        if (level > 0 && level <= 6)
        {
            var text = trimmed[level..].Trim();
            return new SummaryHeadingBlock(level, ParseInlines(text));
        }

        return null;
    }

    private static SummaryListBlock? ParseList(string[] lines, ref int index)
    {
        var items = new List<IReadOnlyList<SummaryInline>>();

        while (index < lines.Length)
        {
            var line = lines[index];
            var trimmed = line.TrimStart();

            if (!trimmed.StartsWith("- ") && !trimmed.StartsWith("* "))
                break;

            var text = trimmed[2..].Trim();
            items.Add(ParseInlines(text));
            index++;
        }

        return items.Count > 0 ? new SummaryListBlock(items) : null;
    }

    private static SummaryParagraphBlock? ParseParagraph(string[] lines, ref int index)
    {
        var textLines = new List<string>();

        while (index < lines.Length)
        {
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
                break;

            if (line.TrimStart().StartsWith("#") ||
                line.TrimStart().StartsWith("- ") ||
                line.TrimStart().StartsWith("* ") ||
                line.TrimStart().StartsWith("```"))
                break;

            textLines.Add(line);
            index++;
        }

        var text = string.Join(" ", textLines.Select(l => l.Trim()));
        var inlines = ParseInlines(text);

        return inlines.Count > 0 ? new SummaryParagraphBlock(inlines) : null;
    }

    private static IReadOnlyList<SummaryInline> ParseInlines(string text)
    {
        var inlines = new List<SummaryInline>();
        var matches = BoldRegex.Matches(text);

        if (matches.Count == 0)
        {
            AppendTimestampAwareInlines(text, inlines, false);
            return inlines;
        }

        var lastIndex = 0;
        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
                AppendTimestampAwareInlines(text[lastIndex..match.Index], inlines, false);

            var boldText = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(boldText))
                AppendTimestampAwareInlines(boldText, inlines, true);

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
            AppendTimestampAwareInlines(text[lastIndex..], inlines, false);

        return inlines;
    }

    private static void AppendTimestampAwareInlines(string text, List<SummaryInline> inlines, bool isBold)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var matches = TimestampRegex.Matches(text);

        if (matches.Count == 0)
        {
            inlines.Add(new SummaryTextInline(text, isBold));
            return;
        }

        var lastIndex = 0;
        foreach (Match match in matches)
        {
            // 添加时间戳前的文本
            if (match.Index > lastIndex)
            {
                var beforeText = text[lastIndex..match.Index];
                if (!string.IsNullOrEmpty(beforeText))
                    inlines.Add(new SummaryTextInline(beforeText, isBold));
            }

            // 添加时间戳
            var timestampText = match.Value;
            var seconds = ParseTimestampSeconds(timestampText);
            inlines.Add(new SummaryTimestampInline(timestampText, seconds, isBold));

            lastIndex = match.Index + match.Length;
        }

        // 添加剩余文本
        if (lastIndex < text.Length)
        {
            var afterText = text[lastIndex..];
            if (!string.IsNullOrEmpty(afterText))
                inlines.Add(new SummaryTextInline(afterText, isBold));
        }
    }
}
