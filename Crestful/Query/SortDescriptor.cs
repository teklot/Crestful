namespace Crestful.Query;

/// <summary>
/// Describes a single sort clause parsed from the <c>?sort=</c> query parameter.
/// A leading <c>-</c> indicates descending order.
/// </summary>
public sealed class SortDescriptor
{
    /// <summary>The property name to sort by.</summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>Whether to sort in descending order.</summary>
    public bool Descending { get; init; }
}
