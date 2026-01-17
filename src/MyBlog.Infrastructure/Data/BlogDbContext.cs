using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Models;

namespace MyBlog.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for the blog.
/// </summary>
public sealed class BlogDbContext : DbContext
{
    /// <summary>Initializes a new instance of the BlogDbContext.</summary>
    public BlogDbContext(DbContextOptions<BlogDbContext> options) : base(options)
    {
    }

    /// <summary>Gets or sets the Users table.</summary>
    public DbSet<User> Users => Set<User>();
    /// <summary>Gets or sets the Posts table.</summary>
    public DbSet<Post> Posts => Set<Post>();
    /// <summary>Gets or sets the Images table.</summary>
    public DbSet<Image> Images => Set<Image>();
    /// <summary>Gets or sets the TelemetryLogs table.</summary>
    public DbSet<TelemetryLog> TelemetryLogs => Set<TelemetryLog>();
    /// <summary>Gets or sets the Image Dimension Cache table.</summary>
    public DbSet<ImageDimensionCache> ImageDimensionCache => Set<ImageDimensionCache>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
        });

        // Post configuration
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

        // Image configuration
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

        // TelemetryLog configuration
        modelBuilder.Entity<TelemetryLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Level).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.TimestampUtc);
        });

        // ImageDimensionCache configuration
        modelBuilder.Entity<ImageDimensionCache>(entity =>
        {
            entity.HasKey(e => e.Url); // URL is the primary key
            entity.Property(e => e.Url).HasMaxLength(2048); // Support long URLs
        });
    }
}
