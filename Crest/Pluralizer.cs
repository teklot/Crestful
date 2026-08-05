namespace Crest;

/// <summary>
/// A minimal English pluralizer used to derive default route names (e.g. <c>Device</c> →
/// <c>/api/devices</c>). Override per resource via <see cref="ResourceOptions.Name"/>.
/// </summary>
internal static class Pluralizer
{
    public static string Pluralize(string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return word;
        }

        if (word.EndsWith("y", StringComparison.OrdinalIgnoreCase) && word.Length > 1 && !IsVowel(word[^2]))
        {
            return word[..^1] + "ies";
        }

        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("z", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
        {
            return word + "es";
        }

        return word + "s";
    }

    private static bool IsVowel(char c) => "aeiou".IndexOf(char.ToLowerInvariant(c)) >= 0;
}
