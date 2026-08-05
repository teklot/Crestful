using Microsoft.EntityFrameworkCore;

namespace Crest.Sample;

public sealed class DeviceDbContext : DbContext
{
    public DeviceDbContext(DbContextOptions<DeviceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<Reading> Readings => Set<Reading>();
}
