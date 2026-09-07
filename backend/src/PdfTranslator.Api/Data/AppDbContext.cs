using Microsoft.EntityFrameworkCore;
using PdfTranslator.Api.Models;

namespace PdfTranslator.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TranslationJob> TranslationJobs => Set<TranslationJob>();
    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();
    public DbSet<JobStatusHistory> JobStatusHistories => Set<JobStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Lưu enum JobStatus dưới dạng string trong database
        modelBuilder.Entity<TranslationJob>()
            .Property(j => j.Status)
            .HasConversion<string>();

        // Thiết lập quan hệ 1-N giữa TranslationJob và ContentBlock
        modelBuilder.Entity<ContentBlock>()
            .HasOne(c => c.TranslationJob)
            .WithMany(j => j.ContentBlocks)
            .HasForeignKey(c => c.TranslationJobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cấu hình JobStatusHistory
        modelBuilder.Entity<JobStatusHistory>(entity =>
        {
            entity.Property(h => h.FromStatus)
                .HasConversion<string>();

            entity.Property(h => h.ToStatus)
                .HasConversion<string>();

            entity.HasOne(h => h.TranslationJob)
                .WithMany(j => j.StatusHistories)
                .HasForeignKey(h => h.TranslationJobId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
