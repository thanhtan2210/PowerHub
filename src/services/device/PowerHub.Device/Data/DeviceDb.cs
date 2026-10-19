using Microsoft.EntityFrameworkCore;

namespace PowerHub.Device.Data;

public sealed class DeviceDb(DbContextOptions<DeviceDb> options) : DbContext(options)
{
    public DbSet<DeviceRecord> Devices => Set<DeviceRecord>();

    public DbSet<DeviceMember> Members => Set<DeviceMember>();

    public DbSet<DeviceCredential> Credentials => Set<DeviceCredential>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    public DbSet<OutboxDelivery> Deliveries => Set<OutboxDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceRecord>(device =>
        {
            device.ToTable("devices");
            device.Property(d => d.Name).HasMaxLength(100);
            device.Property(d => d.Kind).HasMaxLength(50);
            device.Property(d => d.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<DeviceMember>(member =>
        {
            member.ToTable("device_members");
            member.HasKey(m => new { m.DeviceId, m.UserId });
            member.HasIndex(m => new { m.UserId, m.DeviceId });
            member.HasOne<DeviceRecord>().WithMany().HasForeignKey(m => m.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceCredential>(credential =>
        {
            credential.ToTable("device_credentials");
            credential.HasIndex(c => c.DeviceId);
            credential.HasOne<DeviceRecord>().WithMany().HasForeignKey(c => c.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditEvent>(audit =>
        {
            audit.ToTable("audit_events");
            audit.Property(a => a.Action).HasMaxLength(100);
            audit.Property(a => a.TargetType).HasMaxLength(50);
            audit.Property(a => a.TargetId).HasMaxLength(100);
            audit.Property(a => a.Result).HasMaxLength(20);
            audit.Property(a => a.Source).HasMaxLength(64);
            audit.Property(a => a.CorrelationId).HasMaxLength(64);
            audit.HasIndex(a => a.OccurredAt);
            audit.HasIndex(a => new { a.TargetType, a.TargetId });
        });

        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable("outbox_messages");
            outbox.Property(o => o.EventType).HasMaxLength(100);
            outbox.Property(o => o.Subject).HasMaxLength(100);
            outbox.Property(o => o.Payload).HasColumnType("jsonb");
        });

        modelBuilder.Entity<OutboxDelivery>(delivery =>
        {
            delivery.ToTable("outbox_deliveries");
            delivery.HasKey(d => new { d.Destination, d.MessageId });
            delivery.Property(d => d.Destination).HasMaxLength(50);
            delivery.Property(d => d.LastError).HasMaxLength(500);
            delivery.HasOne<OutboxMessage>().WithMany().HasForeignKey(d => d.MessageId).OnDelete(DeleteBehavior.Cascade);
            // The relay only ever scans deliveries that are still pending.
            delivery.HasIndex(d => d.NextAttemptAt).HasFilter("delivered_at IS NULL AND dead_lettered_at IS NULL");
        });
    }
}
