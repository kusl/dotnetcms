using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;

namespace MyBlog.Infrastructure.Data;

public sealed class BlogDbContext : DbContext
{
    public BlogDbContext(DbContextOptions<BlogDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Image> Images => Set<Image>();
    public DbSet<TelemetryLog> TelemetryLogs => Set<TelemetryLog>();
    public DbSet<ImageDimensionCache> ImageDimensionCache => Set<ImageDimensionCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Summary).HasMaxLength(500).IsRequired();
            entity.HasOne(e => e.Author)
                .WithMany(u => u.Posts)
                .HasForeignKey(e => e.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Image>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.HasOne(e => e.Post)
                .WithMany(p => p.Images)
                .HasForeignKey(e => e.PostId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.UploadedBy)
                .WithMany(u => u.UploadedImages)
                .HasForeignKey(e => e.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TelemetryLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Level).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.TimestampUtc);
        });

        modelBuilder.Entity<ImageDimensionCache>(entity =>
        {
            entity.HasKey(e => e.Url);
            entity.Property(e => e.Url).HasMaxLength(2048);
        });
    }
}
