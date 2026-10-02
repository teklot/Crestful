using System.Globalization;

namespace Crestful;

/// <summary>
/// Formatting and comparison helpers for HTTP conditional request validators. Both <c>If-Match</c> and
/// <c>If-None-Match</c> are compared leniently: surrounding quotes and a <c>W/</c> weak prefix are
/// stripped before an ordinal comparison, so clients may echo the <c>ETag</c> verbatim or send it
/// unquoted or weakly tagged.
/// </summary>
internal static class ResourceConcurrency
{
    /// <summary>
    /// Formats a version token as a strong <c>ETag</c>. A missing token formats as an empty tag rather
    /// than being omitted, so that a resource whose store has not populated the token yet still has a
    /// validator clients can round-trip.
    /// </summary>
    public static string FormatETag(byte[]? rowVersion) => '"' + Token(rowVersion) + '"';

    /// <summary>
    /// Evaluates an <c>If-Match</c> or <c>If-None-Match</c> header value against the current version
    /// token. The wildcard <c>*</c> always matches. Otherwise any single tag in a comma-separated list
    /// matches. Returns <c>false</c> when the header is absent or empty.
    /// </summary>
    public static bool IsSatisfied(string? headerValue, byte[]? currentRowVersion)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var trimmed = headerValue.Trim();
        if (string.Equals(trimmed, "*", StringComparison.Ordinal))
        {
            return true;
        }

        var current = Token(currentRowVersion);
        foreach (var candidate in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(Normalize(candidate), current, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The bare, unquoted token that both sides of a comparison reduce to.</summary>
    private static string Token(byte[]? rowVersion) => Convert.ToBase64String(rowVersion ?? []);

    /// <summary>
    /// Truncates a timestamp to whole seconds. HTTP dates have one-second resolution, so comparing a
    /// sub-second timestamp directly would report a resource as modified when the client already holds
    /// the current validator.
    /// </summary>
    public static DateTimeOffset TruncateToSeconds(DateTimeOffset value)
        => new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, value.Offset);

    /// <summary>Parses an HTTP date header value, returning <c>false</c> if it is malformed.</summary>
    public static bool TryParseHttpDate(string? headerValue, out DateTimeOffset result)
    {
        if (string.IsNullOrWhiteSpace(headerValue) || !DateTimeOffset.TryParse(
                headerValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces,
                out result))
        {
            result = default;
            return false;
        }

        return true;
    }

    private static string Normalize(string candidate)
    {
        var value = candidate.Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..].TrimStart();
        }

        return value.Trim('"');
    }
}