namespace Crestful;

/// <summary>
/// Marker interface for resources that support soft delete. When a resource implements this
/// interface and soft delete is enabled, DELETE operations set <see cref="DeletedAt"/> instead
/// of removing the record, and read operations automatically filter out soft-deleted items.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// The timestamp when the resource was soft-deleted, or <c>null</c> if the resource is active.
    /// </summary>
    DateTimeOffset? DeletedAt { get; set; }
}
