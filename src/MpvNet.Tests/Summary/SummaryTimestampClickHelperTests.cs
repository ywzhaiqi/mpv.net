using MpvNet.Summary;
using Xunit;

namespace MpvNet.Tests.Summary;

public class SummaryTimestampClickHelperTests
{
    [Fact]
    public void TryGetClickedSeconds_ParsesMinutesSecondsMarker()
    {
        var text = "Intro [00:10:10] here";
        var charIndex = text.IndexOf("00:10", StringComparison.Ordinal);

        var result = SummaryTimestampClickHelper.TryGetClickedSeconds(text, charIndex, out var seconds);

        Assert.True(result);
        Assert.Equal(10, seconds);
    }

    [Fact]
    public void TryGetClickedSeconds_ParsesHoursMinutesSecondsMarker()
    {
        var text = "Intro [01:23:45:5025] here";
        var charIndex = text.IndexOf("01:23:45", StringComparison.Ordinal);

        var result = SummaryTimestampClickHelper.TryGetClickedSeconds(text, charIndex, out var seconds);

        Assert.True(result);
        Assert.Equal(5025, seconds);
    }
}
