using Xunit;

namespace MpvNet.Tests;

public class ProtocolPathTests
{
    [Theory]
    [InlineData("mpv://https://example.com/video.mp4", "https://example.com/video.mp4")]
    [InlineData("mpv://https%3A%2F%2Fexample.com%2Fvideo.mp4", "https://example.com/video.mp4")]
    [InlineData("mpv:///C:/media/video.mp4", @"C:\media\video.mp4")]
    public void ConvertFilePath_NormalizesMpvProtocolTargets(string input, string expected)
    {
        Assert.Equal(expected, MainPlayer.ConvertFilePath(input));
    }
}
