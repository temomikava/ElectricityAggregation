using Electricity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Electricity.Infrastructure.Data;

public class ElectricityDbContext : DbContext
{
    public ElectricityDbContext(DbContextOptions<ElectricityDbContext> options) : base(options)
    {
    }

    public DbSet<ElectricityConsumptionRecord> ConsumptionRecords { get; set; }
    public DbSet<DataProcessingLog> ProcessingLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ElectricityConsumptionRecord>(entity =>
        {
            entity.ToTable("consumption_records");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.Region, e.Month })
                .HasDatabaseName("idx_region_month");

            entity.HasIndex(e => e.Month)
                .HasDatabaseName("idx_consumption_month");

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.Region)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("region");

            entity.Property(e => e.BuildingType)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("building_type");

            entity.Property(e => e.Month)
                .IsRequired()
                .HasColumnName("month");

            entity.Property(e => e.TotalConsumption)
                .HasPrecision(18, 2)
                .HasColumnName("total_consumption");

            entity.Property(e => e.RecordCount)
                .HasColumnName("record_count");

            entity.Property(e => e.ProcessedAt)
                .HasColumnName("processed_at");

            entity.Property(e => e.SourceFile)
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnName("source_file");
        });

        modelBuilder.Entity<DataProcessingLog>(entity =>
        {
            entity.ToTable("processing_logs");

            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.StartedAt)
                .HasDatabaseName("idx_logs_started")
                .IsDescending();

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.StartedAt)
                .IsRequired()
                .HasColumnName("started_at");

            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");

            entity.Property(e => e.Month)
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnName("month");

            entity.Property(e => e.Status)
                .IsRequired()
                .HasColumnName("status");

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message");

            entity.Property(e => e.RecordsProcessed)
                .HasColumnName("records_processed");

            entity.Property(e => e.RecordsFiltered)
                .HasColumnName("records_filtered");
        });
    }
}
