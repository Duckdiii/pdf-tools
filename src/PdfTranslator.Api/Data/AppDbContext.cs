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
    }
}
