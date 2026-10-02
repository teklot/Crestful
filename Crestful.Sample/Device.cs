using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Crestful;

namespace Crestful.Sample;

/// <summary>
/// A sample resource. Crestful derives the full CRUD API (<c>/api/devices</c>) from this type.
/// </summary>
public sealed class Device : IResource, ISoftDeletable, IAuditable, IHasRowVersion
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Model { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// The concurrency token. The ETag header is the only channel clients need, so it is kept out of
    /// the representation body. Initialized because this sample runs on EF Core's in-memory provider,
    /// which does not generate row versions.
    /// </summary>
    [JsonIgnore]
    public byte[] RowVersion { get; set; } = [];

    public List<Reading> Readings { get; set; } = [];
}
