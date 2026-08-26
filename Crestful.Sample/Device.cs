using System.ComponentModel.DataAnnotations;
using Crestful;

namespace Crestful.Sample;

/// <summary>
/// A sample resource. Crestful derives the full CRUD API (<c>/api/devices</c>) from this type.
/// </summary>
public sealed class Device : IResource, ISoftDeletable
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Model { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<Reading> Readings { get; set; } = [];
}
