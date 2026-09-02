namespace Crestful;

/// <summary>
/// Marker interface for resources that support automatic auditing. When a resource implements this
/// interface and auditing is enabled, the framework automatically populates <see cref="CreatedAt"/>,
/// <see cref="UpdatedAt"/>, <see cref="CreatedBy"/>, and <see cref="UpdatedBy"/> on create and update operations.
/// </summary>
public interface IAuditable
{
    /// <summary>
    /// The timestamp when the resource was created. Set automatically on create.
    /// </summary>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The timestamp when the resource was last updated. Set automatically on create and update.
    /// </summary>
    DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The identity of the user who created the resource. Set from <c>HttpContext.User.Identity?.Name</c> on create.
    /// </summary>
    string? CreatedBy { get; set; }

    /// <summary>
    /// The identity of the user who last updated the resource. Set from <c>HttpContext.User.Identity?.Name</c> on update.
    /// </summary>
    string? UpdatedBy { get; set; }
}
