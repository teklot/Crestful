using System.ComponentModel.DataAnnotations;
using Crest;

namespace Crest.Sample;

/// <summary>
/// A telemetry reading for a <see cref="Device"/>. Uses a <c>Guid</c> key to show a second key
/// convention alongside <c>Device.Id</c>.
/// </summary>
public sealed class Reading : IResource
{
    public Guid Id { get; set; }

    [Required]
    public int DeviceId { get; set; }

    [Required]
    public double Value { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
