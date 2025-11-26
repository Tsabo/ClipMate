using ClipMate.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data;

/// <summary>
/// Entity Framework Core database context for ClipMate.
/// Schema matches ClipMate 7.5 for compatibility.
/// </summary>
public class ClipMateDbContext : DbContext
{
    public ClipMateDbContext(DbContextOptions<ClipMateDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the clips collection.
    /// </summary>
    public DbSet<Clip> Clips => Set<Clip>();

    /// <summary>
    /// Gets or sets the collections (includes folders - merged table matching ClipMate 7.5).
    /// </summary>
    public DbSet<Collection> Collections => Set<Collection>();

    /// <summary>
    /// Gets or sets the clipboard format metadata.
    /// </summary>
    public DbSet<ClipData> ClipData => Set<ClipData>();

    /// <summary>
    /// Gets or sets the text BLOB storage.
    /// </summary>
    public DbSet<BlobTxt> BlobTxt => Set<BlobTxt>();

    /// <summary>
    /// Gets or sets the JPEG image BLOB storage.
    /// </summary>
    public DbSet<BlobJpg> BlobJpg => Set<BlobJpg>();

    /// <summary>
    /// Gets or sets the PNG image BLOB storage.
    /// </summary>
    public DbSet<BlobPng> BlobPng => Set<BlobPng>();

    /// <summary>
    /// Gets or sets the generic binary BLOB storage.
    /// </summary>
    public DbSet<BlobBlob> BlobBlob => Set<BlobBlob>();

    /// <summary>
    /// Gets or sets the PowerPaste shortcuts.
    /// </summary>
    public DbSet<Shortcut> Shortcuts => Set<Shortcut>();

    /// <summary>
    /// Gets or sets the users.
    /// </summary>
    public DbSet<User> Users => Set<User>();

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

    /// <summary>
    /// Gets or sets the Monaco Editor states.
    /// </summary>
    public DbSet<MonacoEditorState> MonacoEditorStates => Set<MonacoEditorState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureClip(modelBuilder);
        ConfigureCollection(modelBuilder);
        ConfigureClipData(modelBuilder);
        ConfigureBlobTxt(modelBuilder);
        ConfigureBlobJpg(modelBuilder);
        ConfigureBlobPng(modelBuilder);
        ConfigureBlobBlob(modelBuilder);
        ConfigureShortcut(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigureTemplate(modelBuilder);
        ConfigureSearchQuery(modelBuilder);
        ConfigureApplicationFilter(modelBuilder);
        ConfigureSoundEvent(modelBuilder);
        ConfigureMonacoEditorState(modelBuilder);
    }

    private static void ConfigureClip(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Clip>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            // ClipMate 7.5 fields with max lengths
            entity.Property(e => e.Title).HasMaxLength(60);
            entity.Property(e => e.Creator).HasMaxLength(60);
            entity.Property(e => e.SourceUrl).HasMaxLength(250);
            entity.Property(e => e.CapturedAt).IsRequired();
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.ContentHash).IsRequired().HasMaxLength(64);

            // Transient properties - NOT stored in Clips table (loaded from BLOB tables)
            entity.Ignore(e => e.TextContent);
            entity.Ignore(e => e.RtfContent);
            entity.Ignore(e => e.HtmlContent);
            entity.Ignore(e => e.ImageData);
            entity.Ignore(e => e.FilePathsJson);

            // Format flag properties - NOT stored in Clips table
            entity.Ignore(e => e.IconGlyph);
            entity.Ignore(e => e.HasText);
            entity.Ignore(e => e.HasRtf);
            entity.Ignore(e => e.HasHtml);
            entity.Ignore(e => e.HasBitmap);
            entity.Ignore(e => e.HasFiles);

            // Navigation properties
            entity.Ignore(e => e.ClipDataFormats); // For now, explicit queries

            // Indexes for performance - ClipMate 7.5 compatibility
            entity.HasIndex(e => e.CapturedAt).HasDatabaseName("IX_Clips_CapturedAt");
            entity.HasIndex(e => e.Type).HasDatabaseName("IX_Clips_Type");
            entity.HasIndex(e => e.ContentHash).HasDatabaseName("IX_Clips_ContentHash");
            entity.HasIndex(e => e.SourceApplicationName).HasDatabaseName("IX_Clips_SourceApplicationName");
            entity.HasIndex(e => e.IsFavorite).HasDatabaseName("IX_Clips_IsFavorite");
            entity.HasIndex(e => e.CollectionId).HasDatabaseName("IX_Clips_CollectionId");
            entity.HasIndex(e => e.FolderId).HasDatabaseName("IX_Clips_FolderId");
            entity.HasIndex(e => e.Del).HasDatabaseName("IX_Clips_Del"); // Soft delete index
            entity.HasIndex(e => e.SortKey).HasDatabaseName("IX_Clips_SortKey");
            entity.HasIndex(e => e.Checksum).HasDatabaseName("IX_Clips_Checksum");
        });
    }

    private static void ConfigureCollection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            // ClipMate 7.5 fields with max lengths
            entity.Property(e => e.Title).IsRequired().HasMaxLength(60);
            entity.Property(e => e.Sql).HasMaxLength(256);

            // Indexes for performance
            entity.HasIndex(e => e.Title).HasDatabaseName("IX_Collections_Title");
            entity.HasIndex(e => e.ParentId).HasDatabaseName("IX_Collections_ParentId");
            entity.HasIndex(e => e.SortKey).HasDatabaseName("IX_Collections_SortKey");
            entity.HasIndex(e => e.LmType).HasDatabaseName("IX_Collections_LmType");
            entity.HasIndex(e => e.NewClipsGo).HasDatabaseName("IX_Collections_NewClipsGo");
            entity.HasIndex(e => e.Favorite).HasDatabaseName("IX_Collections_Favorite");

            // Self-referencing relationship for hierarchy
            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureClipData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClipData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.FormatName).IsRequired().HasMaxLength(60);
            entity.Property(e => e.ClipId).IsRequired();
            entity.Property(e => e.Format).IsRequired();

            // Indexes for performance
            entity.HasIndex(e => e.ClipId).HasDatabaseName("IX_ClipData_ClipId");
            entity.HasIndex(e => e.Format).HasDatabaseName("IX_ClipData_Format");
            entity.HasIndex(e => e.StorageType).HasDatabaseName("IX_ClipData_StorageType");

            // Relationship to Clip
            entity.HasOne(e => e.Clip)
                .WithMany()
                .HasForeignKey(e => e.ClipId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureBlobTxt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlobTxt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.ClipDataId).IsRequired();
            entity.Property(e => e.ClipId).IsRequired(); // Denormalized
            entity.Property(e => e.Data).IsRequired();

            // Indexes for performance
            entity.HasIndex(e => e.ClipId).HasDatabaseName("IX_BlobTxt_ClipId");
            entity.HasIndex(e => e.ClipDataId).HasDatabaseName("IX_BlobTxt_ClipDataId");

            // Relationship to ClipData
            entity.HasOne(e => e.ClipData)
                .WithMany()
                .HasForeignKey(e => e.ClipDataId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureBlobJpg(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlobJpg>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.ClipDataId).IsRequired();
            entity.Property(e => e.ClipId).IsRequired(); // Denormalized
            entity.Property(e => e.Data).IsRequired();

            // Indexes for performance
            entity.HasIndex(e => e.ClipId).HasDatabaseName("IX_BlobJpg_ClipId");
            entity.HasIndex(e => e.ClipDataId).HasDatabaseName("IX_BlobJpg_ClipDataId");

            // Relationship to ClipData
            entity.HasOne(e => e.ClipData)
                .WithMany()
                .HasForeignKey(e => e.ClipDataId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureBlobPng(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlobPng>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.ClipDataId).IsRequired();
            entity.Property(e => e.ClipId).IsRequired(); // Denormalized
            entity.Property(e => e.Data).IsRequired();

            // Indexes for performance
            entity.HasIndex(e => e.ClipId).HasDatabaseName("IX_BlobPng_ClipId");
            entity.HasIndex(e => e.ClipDataId).HasDatabaseName("IX_BlobPng_ClipDataId");

            // Relationship to ClipData
            entity.HasOne(e => e.ClipData)
                .WithMany()
                .HasForeignKey(e => e.ClipDataId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureBlobBlob(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlobBlob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.ClipDataId).IsRequired();
            entity.Property(e => e.ClipId).IsRequired(); // Denormalized
            entity.Property(e => e.Data).IsRequired();

            // Indexes for performance
            entity.HasIndex(e => e.ClipId).HasDatabaseName("IX_BlobBlob_ClipId");
            entity.HasIndex(e => e.ClipDataId).HasDatabaseName("IX_BlobBlob_ClipDataId");

            // Relationship to ClipData
            entity.HasOne(e => e.ClipData)
                .WithMany()
                .HasForeignKey(e => e.ClipDataId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureShortcut(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shortcut>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.Nickname).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ClipId).IsRequired();

            // Unique index on Nickname - PowerPaste requires unique shortcuts
            entity.HasIndex(e => e.Nickname).IsUnique().HasDatabaseName("IX_Shortcuts_Nickname");
            entity.HasIndex(e => e.ClipId).HasDatabaseName("IX_Shortcuts_ClipId");

            // Relationship to Clip
            entity.HasOne(e => e.Clip)
                .WithMany()
                .HasForeignKey(e => e.ClipId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Workstation).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LastDate).IsRequired();

            // Indexes for performance
            entity.HasIndex(e => e.Username).HasDatabaseName("IX_Users_Username");
            entity.HasIndex(e => e.Workstation).HasDatabaseName("IX_Users_Workstation");
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

    private static void ConfigureMonacoEditorState(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonacoEditorState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.ClipDataId).IsRequired();
            entity.Property(e => e.Language).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ViewState);
            entity.Property(e => e.LastModified).IsRequired();

            // One-to-one relationship with ClipData
            entity.HasOne(e => e.ClipData)
                .WithMany()
                .HasForeignKey(e => e.ClipDataId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint on ClipDataId (one-to-one)
            entity.HasIndex(e => e.ClipDataId)
                .IsUnique()
                .HasDatabaseName("IX_MonacoEditorStates_ClipDataId");
        });
    }
}
