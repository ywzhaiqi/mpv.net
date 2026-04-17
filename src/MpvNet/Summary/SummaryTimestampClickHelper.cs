namespace MpvNet.Summary;

public static class SummaryTimestampClickHelper
{
    public static bool TryGetClickedSeconds(string text, int charIndex, out int seconds)
    {
        seconds = 0;

        if (string.IsNullOrEmpty(text) || charIndex < 0 || charIndex >= text.Length)
            return false;

        var startBracket = text.LastIndexOf('[', charIndex);
        var endBracket = text.IndexOf(']', charIndex);

        if (startBracket < 0 || endBracket <= startBracket)
            return false;

        var marker = text.Substring(startBracket + 1, endBracket - startBracket - 1);
        var separatorIndex = marker.LastIndexOf(':');

        if (separatorIndex <= 0 || separatorIndex >= marker.Length - 1)
            return false;

        var timestampText = marker[..separatorIndex];
        var secondsText = marker[(separatorIndex + 1)..];

        if (!int.TryParse(secondsText, out seconds))
            return false;

        return timestampText.Count(static c => c == ':') is 1 or 2;
    }
}
