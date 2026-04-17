using Xunit;
using MpvNet.Summary;

namespace MpvNet.Tests.Summary;

/// <summary>
/// SummaryMarkdownParser 测试类
/// </summary>
public class SummaryMarkdownParserTests
{
    [Fact]
    public void Parse_ConvertsTimestampsToSeekSegments()
    {
        var blocks = SummaryMarkdownParser.Parse("Intro 00:10\n\n## Part\nAt 01:23:45 jump.");
        
        Assert.True(blocks.Count >= 2);
        var paragraph = Assert.IsType<SummaryParagraphBlock>(blocks[0]);
        var heading = Assert.IsType<SummaryHeadingBlock>(blocks[1]);

        Assert.Contains(paragraph.Inlines, it => it is SummaryTimestampInline ts && ts.Seconds == 10);
        Assert.Contains(heading.Inlines, it => it is SummaryTextInline text && text.Text == "Part");
    }

    [Fact]
    public void Parse_ConvertsTimestampsInsideHeading()
    {
        var blocks = SummaryMarkdownParser.Parse("### 二、短线与中线锚点区分（03:24）");
        var heading = Assert.IsType<SummaryHeadingBlock>(Assert.Single(blocks));

        var timestamp = Assert.Single(heading.Inlines.OfType<SummaryTimestampInline>());
        Assert.Equal("03:24", timestamp.Text);
        Assert.Equal(204, timestamp.Seconds);
    }

    [Fact]
    public void Parse_PreservesCodeBlocksWithoutTimestampLinks()
    {
        var blocks = SummaryMarkdownParser.Parse("```\nseek 00:10\n```");
        var code = Assert.IsType<SummaryCodeBlock>(Assert.Single(blocks));
        Assert.Equal("seek 00:10", code.Text.Trim());
    }

    [Fact]
    public void ParseTimestampSeconds_ParsesMinutesSeconds()
    {
        Assert.Equal(70, SummaryMarkdownParser.ParseTimestampSeconds("01:10"));
        Assert.Equal(600, SummaryMarkdownParser.ParseTimestampSeconds("10:00"));
    }

    [Fact]
    public void ParseTimestampSeconds_ParsesHoursMinutesSeconds()
    {
        Assert.Equal(5025, SummaryMarkdownParser.ParseTimestampSeconds("01:23:45"));
        Assert.Equal(3661, SummaryMarkdownParser.ParseTimestampSeconds("01:01:01"));
    }

    [Fact]
    public void Parse_HandlesMultipleTimestampsInParagraph()
    {
        var blocks = SummaryMarkdownParser.Parse("At 00:10 intro, at 01:20 main part");
        var paragraph = Assert.IsType<SummaryParagraphBlock>(Assert.Single(blocks));
        
        var timestamps = paragraph.Inlines.OfType<SummaryTimestampInline>().ToList();
        Assert.Equal(2, timestamps.Count);
        Assert.Equal(10, timestamps[0].Seconds);
        Assert.Equal(80, timestamps[1].Seconds);
    }

    [Fact]
    public void Parse_HandlesEmptyInput()
    {
        var blocks = SummaryMarkdownParser.Parse("");
        Assert.Empty(blocks);
    }

    [Fact]
    public void Parse_HandlesLists()
    {
        var blocks = SummaryMarkdownParser.Parse("- Item 1\n- Item 2 at 00:30");
        var list = Assert.IsType<SummaryListBlock>(Assert.Single(blocks));
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void Parse_RendersBoldText()
    {
        var blocks = SummaryMarkdownParser.Parse("**电池板块**");
        var paragraph = Assert.IsType<SummaryParagraphBlock>(Assert.Single(blocks));
        var text = Assert.IsType<SummaryTextInline>(Assert.Single(paragraph.Inlines));

        Assert.Equal("电池板块", text.Text);
        Assert.True(text.IsBold);
    }

    [Fact]
    public void Parse_PreservesTimestampInsideBoldText()
    {
        var blocks = SummaryMarkdownParser.Parse("**电池板块（03:24）**");
        var paragraph = Assert.IsType<SummaryParagraphBlock>(Assert.Single(blocks));
        var timestamp = Assert.Single(paragraph.Inlines.OfType<SummaryTimestampInline>());

        Assert.Equal("03:24", timestamp.Text);
        Assert.Equal(204, timestamp.Seconds);
        Assert.True(timestamp.IsBold);
    }
}
