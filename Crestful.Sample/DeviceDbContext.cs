using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace Crestful.Sample;

public sealed class DeviceDbContext : DbContext
{
    public DeviceDbContext(DbContextOptions<DeviceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<Reading> Readings => Set<Reading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // On SQL Server this would be .IsRowVersion(), which makes the database maintain the token.
        // The in-memory provider used here supports neither generation nor a null value, so the token
        // is configured as a plain concurrency token and rotated in SaveChanges instead.
        modelBuilder.Entity<Device>()
            .Property(d => d.RowVersion)
            .IsConcurrencyToken();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RotateRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RotateRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RotateRowVersions()
    {
        foreach (var entry in ChangeTracker.Entries<Device>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var token = new byte[8];
            RandomNumberGenerator.Fill(token);
            entry.Property(d => d.RowVersion).CurrentValue = token;
        }
    }
}