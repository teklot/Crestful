using Microsoft.EntityFrameworkCore;

namespace Crest.Samples.Devices;

public sealed class DeviceDbContext : DbContext
{
    public DeviceDbContext(DbContextOptions<DeviceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();
}
