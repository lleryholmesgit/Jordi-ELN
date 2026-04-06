using ElectronicLabNotebook.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ElectronicLabNotebook.Data;

public sealed class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<ExperimentRecord> ExperimentRecords => Set<ExperimentRecord>();
    public DbSet<RecordTemplate> RecordTemplates => Set<RecordTemplate>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<RecordInstrumentLink> RecordInstrumentLinks => Set<RecordInstrumentLink>();
    public DbSet<ExperimentAttachment> ExperimentAttachments => Set<ExperimentAttachment>();
    public DbSet<ReviewAction> ReviewActions => Set<ReviewAction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Instrument>()
            .HasIndex(x => x.Code)
            .IsUnique();

        builder.Entity<Instrument>()
            .HasIndex(x => x.QrCodeToken)
            .IsUnique();

        builder.Entity<ExperimentRecord>()
            .HasIndex(x => x.ExperimentCode)
            .IsUnique();

        builder.Entity<ApplicationSetting>()
            .HasIndex(x => x.Key)
            .IsUnique();

        builder.Entity<RecordInstrumentLink>()
            .HasKey(x => new { x.ExperimentRecordId, x.InstrumentId });

        builder.Entity<RecordInstrumentLink>()
            .HasOne(x => x.ExperimentRecord)
            .WithMany(x => x.InstrumentLinks)
            .HasForeignKey(x => x.ExperimentRecordId);

        builder.Entity<RecordInstrumentLink>()
            .HasOne(x => x.Instrument)
            .WithMany(x => x.RecordLinks)
            .HasForeignKey(x => x.InstrumentId);
    }
}
