using System.Globalization;

namespace FellowshipAnalyzer.Core.Utility;

/// <summary>
/// Display formatting for durations, clock offsets, and dates.
/// </summary>
public static class TimeFormat
{
    /// <summary>
    /// Formats a millisecond offset as [-]hh:mm:ss.fff.
    /// </summary>
    /// <param name="milliseconds">The offset to format.</param>
    /// <param name="precision">Fractional-second digits, max 3.</param>
    public static string Timestamp(long milliseconds, int precision = 0)
    {
        var sign = milliseconds < 0 ? "-" : string.Empty;
        var magnitude = Math.Abs(milliseconds);
        var fraction = precision > 0
            ? "." + (magnitude % 1000).ToString("D3", CultureInfo.InvariantCulture)[..Math.Min(precision, 3)]
            : string.Empty;

        return magnitude < 60_000
            ? $"{sign}{magnitude / 1000}{fraction}s"
            : $"{sign}{magnitude / 60_000}:{magnitude / 1000 % 60:D2}{fraction}";
    }

    /// <summary>
    /// Formats a millisecond offset as [-]hh:mm:ss.fff.
    /// </summary>
    /// <param name="milliseconds">The offset to format.</param>
    /// <param name="precision">Fractional-second digits, max 3.</param>
    public static string Timestamp(int milliseconds, int precision = 0) =>
        Timestamp((long)milliseconds, precision);

    /// <summary>Formats a millisecond duration as seconds with a fixed number of decimals (<c>4s</c>, or <c>4.5s</c> with <paramref name="precision"/> = 1).</summary>
    /// <param name="milliseconds">The duration to format.</param>
    /// <param name="precision">The number of decimals to show.</param>
    public static string Seconds(double milliseconds, int precision = 0) =>
        $"{(milliseconds / 1000d).ToString($"F{Math.Max(0, precision)}")}s";

    /// <summary>
    /// Formats a Unix time as a local calendar date (<c>3 Feb 2026</c>).
    /// Values below 10 billion are read as seconds, larger values as milliseconds.
    /// Returns an empty string when the value is out of range.
    /// </summary>
    public static string Date(double unixTime)
    {
        var milliseconds = unixTime < 10_000_000_000 ? unixTime * 1000 : unixTime;

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds).LocalDateTime.ToString("d MMM yyyy");
        }
        catch (ArgumentOutOfRangeException)
        {
            return string.Empty;
        }
    }

    /// <summary>Formats the time elapsed from <paramref name="at"/> to <paramref name="now"/> (<c>just now</c>, <c>5m ago</c>, <c>3h ago</c>, <c>2d ago</c>).</summary>
    public static string Age(DateTimeOffset at, DateTimeOffset now)
    {
        var age = now - at;

        if (age.TotalMinutes < 1) return "just now";
        if (age.TotalHours < 1) return $"{(int)age.TotalMinutes}m ago";
        if (age.TotalDays < 1) return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }
}
