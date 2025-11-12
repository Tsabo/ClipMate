using ClipMate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data;

/// <summary>
/// Entity Framework Core database context for ClipMate.
/// </summary>
public class ClipMateDbContext : DbContext
{
    /// <summary>
    /// Gets or sets the clips collection.
    /// </summary>
    public DbSet<Clip> Clips => Set<Clip>();

    /// <summary>
    /// Gets or sets the collections.
    /// </summary>
    public DbSet<Collection> Collections => Set<Collection>();

    /// <summary>
    /// Gets or sets the folders.
    /// </summary>
    public DbSet<Folder> Folders => Set<Folder>();

    /// <summary>
    /// Gets or sets the templates.
    /// </summary>
    public DbSet<Template> Templates => Set<Template>();

    /// <summary>
    /// Gets or sets the search queries.
    /// </summary>
    public DbSet<SearchQuery> SearchQueries => Set<SearchQuery>();

    /// <summary>
    /// Gets or sets the application filters.
    /// </summary>
    public DbSet<ApplicationFilter> ApplicationFilters => Set<ApplicationFilter>();

    /// <summary>
    /// Gets or sets the sound events.
    /// </summary>
    public DbSet<SoundEvent> SoundEvents => Set<SoundEvent>();

    public ClipMateDbContext(DbContextOptions<ClipMateDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureClip(modelBuilder);
        ConfigureCollection(modelBuilder);
        ConfigureFolder(modelBuilder);
        ConfigureTemplate(modelBuilder);
        ConfigureSearchQuery(modelBuilder);
        ConfigureApplicationFilter(modelBuilder);
        ConfigureSoundEvent(modelBuilder);
    }

    private static void ConfigureClip(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Clip>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.ContentHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CapturedAt).IsRequired();
            
            // Indexes for performance
            entity.HasIndex(e => e.CapturedAt).HasDatabaseName("IX_Clips_CapturedAt");
            entity.HasIndex(e => e.Type).HasDatabaseName("IX_Clips_Type");
            entity.HasIndex(e => e.ContentHash).HasDatabaseName("IX_Clips_ContentHash");
            entity.HasIndex(e => e.SourceApplicationName).HasDatabaseName("IX_Clips_SourceApplicationName");
            entity.HasIndex(e => e.IsFavorite).HasDatabaseName("IX_Clips_IsFavorite");
            entity.HasIndex(e => e.CollectionId).HasDatabaseName("IX_Clips_CollectionId");
            entity.HasIndex(e => e.FolderId).HasDatabaseName("IX_Clips_FolderId");
        });
    }

    private static void ConfigureCollection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasIndex(e => e.Name).HasDatabaseName("IX_Collections_Name");
            entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_Collections_IsActive");
        });
    }

    private static void ConfigureFolder(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Folder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasIndex(e => e.Name).HasDatabaseName("IX_Folders_Name");
            entity.HasIndex(e => e.CollectionId).HasDatabaseName("IX_Folders_CollectionId");
            entity.HasIndex(e => e.ParentFolderId).HasDatabaseName("IX_Folders_ParentFolderId");
            entity.HasIndex(e => e.SortOrder).HasDatabaseName("IX_Folders_SortOrder");
            
            // Self-referencing relationship for folder hierarchy
            entity.HasOne<Folder>()
                .WithMany()
                .HasForeignKey(e => e.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTemplate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Template>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasIndex(e => e.Name).HasDatabaseName("IX_Templates_Name");
            entity.HasIndex(e => e.CollectionId).HasDatabaseName("IX_Templates_CollectionId");
        });
    }

    private static void ConfigureSearchQuery(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SearchQuery>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.QueryText).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasIndex(e => e.LastExecutedAt).HasDatabaseName("IX_SearchQueries_LastExecutedAt");
            entity.HasIndex(e => e.QueryText).HasDatabaseName("IX_SearchQueries_QueryText");
        });
    }

    private static void ConfigureApplicationFilter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationFilter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasIndex(e => e.Name).HasDatabaseName("IX_ApplicationFilters_Name");
            entity.HasIndex(e => e.ProcessName).HasDatabaseName("IX_ApplicationFilters_ProcessName");
            entity.HasIndex(e => e.IsEnabled).HasDatabaseName("IX_ApplicationFilters_IsEnabled");
        });
    }

    private static void ConfigureSoundEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SoundEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            
            entity.Property(e => e.EventType).IsRequired();
            entity.Property(e => e.SoundFilePath).IsRequired().HasMaxLength(500);
            
            entity.HasIndex(e => e.EventType).HasDatabaseName("IX_SoundEvents_EventType");
            entity.HasIndex(e => e.IsEnabled).HasDatabaseName("IX_SoundEvents_IsEnabled");
        });
    }
}
