using System.Data;
using System.Text.RegularExpressions;
using ClipMate.Core.Models;
using Microsoft.Data.Sqlite;
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
        // Register TextSearch function when connection is opened
        if (Database.GetDbConnection() is SqliteConnection sqliteConnection)
        {
            sqliteConnection.StateChange += (_, e) =>
            {
                if (e.CurrentState == ConnectionState.Open)
                    RegisterTextSearchFunction(sqliteConnection);
            };
        }
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
    /// Gets or sets the Monaco Editor states.
    /// </summary>
    public DbSet<MonacoEditorState> MonacoEditorStates => Set<MonacoEditorState>();

    /// <summary>
    /// Registers the TextSearch custom SQLite function that handles ClipMate search syntax.
    /// Supports: whole keywords, comma-separated OR logic, * wildcard, % wildcard
    /// </summary>
    private static void RegisterTextSearchFunction(SqliteConnection connection)
    {
        connection.CreateFunction(
            "TextSearch",
            (string fieldValue, string query) =>
            {
                if (string.IsNullOrEmpty(fieldValue))
                    return false;

                // Split by comma for OR logic
                var keywords = query.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var keyword in keywords)
                {
                    var pattern = keyword;

                    // Convert * wildcard to % for LIKE matching
                    if (pattern.EndsWith('*'))
                        pattern = pattern[..^1] + "%";

                    // If no wildcards, add % at both ends for substring search
                    if (!pattern.Contains('%'))
                        pattern = $"%{pattern}%";

                    // Case-insensitive LIKE matching
                    if (fieldValue.Contains(pattern.Replace("%", ""), StringComparison.OrdinalIgnoreCase))
                        return true;

                    // Try proper wildcard matching if pattern has %
                    if (!pattern.Contains('%'))
                        continue;

                    var regex = "^" + Regex.Escape(pattern)
                        .Replace("%", ".*") + "$";

                    if (Regex.IsMatch(fieldValue, regex, RegexOptions.IgnoreCase))
                        return true;
                }

                return false;
            });
    }

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
        ConfigureMonacoEditorState(modelBuilder);
    }

    private static void ConfigureClip(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Clip>(entity =>
        {
            entity.Property(p => p.Title).HasMaxLength(60);
            entity.Property(p => p.Creator).HasMaxLength(60);
            entity.Property(p => p.SourceUrl).HasMaxLength(250);
            entity.Property(p => p.CapturedAt).IsRequired();
            entity.Property(p => p.Type).IsRequired();
            entity.Property(p => p.ContentHash).IsRequired().HasMaxLength(64);

            // Transient properties - NOT stored in Clips table (loaded from BLOB tables)
            entity.Ignore(p => p.TextContent);
            entity.Ignore(p => p.RtfContent);
            entity.Ignore(p => p.HtmlContent);
            entity.Ignore(p => p.ImageData);
            entity.Ignore(p => p.FilePathsJson);

            // Format flag properties - NOT stored in Clips table
            entity.Ignore(p => p.IconGlyph);
            entity.Ignore(p => p.HasText);
            entity.Ignore(p => p.HasRtf);
            entity.Ignore(p => p.HasHtml);
            entity.Ignore(p => p.HasBitmap);
            entity.Ignore(p => p.HasFiles);

            // Navigation properties
            entity.Ignore(p => p.ClipDataFormats); // For now, explicit queries

            // Indexes for performance - ClipMate 7.5 compatibility
            entity.HasIndex(p => p.CapturedAt).HasDatabaseName("IX_Clips_CapturedAt");
            entity.HasIndex(p => p.Type).HasDatabaseName("IX_Clips_Type");
            entity.HasIndex(p => p.ContentHash).HasDatabaseName("IX_Clips_ContentHash");
            entity.HasIndex(p => p.SourceApplicationName).HasDatabaseName("IX_Clips_SourceApplicationName");
            entity.HasIndex(p => p.IsFavorite).HasDatabaseName("IX_Clips_IsFavorite");
            entity.HasIndex(p => p.CollectionId).HasDatabaseName("IX_Clips_CollectionId");
            entity.HasIndex(p => p.FolderId).HasDatabaseName("IX_Clips_FolderId");
            entity.HasIndex(p => p.Del).HasDatabaseName("IX_Clips_Del"); // Soft delete index
            entity.HasIndex(p => p.SortKey).HasDatabaseName("IX_Clips_SortKey");
            entity.HasIndex(p => p.Checksum).HasDatabaseName("IX_Clips_Checksum");
        });
    }

    private static void ConfigureCollection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Collection>(entity =>
        {
            entity.Property(p => p.Title).IsRequired().HasMaxLength(60);
            entity.Property(p => p.Sql).HasMaxLength(256);

            // Retention properties (new fields)
            entity.Property(p => p.MaxBytes).HasDefaultValueSql("0");
            entity.Property(p => p.MaxAgeDays).HasDefaultValueSql("0");
            entity.Property(p => p.Role).IsRequired();

            // Indexes for performance
            entity.HasIndex(p => p.Title).HasDatabaseName("IX_Collections_Title");
            entity.HasIndex(p => p.ParentId).HasDatabaseName("IX_Collections_ParentId");
            entity.HasIndex(p => p.SortKey).HasDatabaseName("IX_Collections_SortKey");
            entity.HasIndex(p => p.LmType).HasDatabaseName("IX_Collections_LmType");
            entity.HasIndex(p => p.NewClipsGo).HasDatabaseName("IX_Collections_NewClipsGo");
            entity.HasIndex(p => p.Favorite).HasDatabaseName("IX_Collections_Favorite");
            entity.HasIndex(p => p.Role).HasDatabaseName("IX_Collections_Role");

            // Self-referencing relationship for hierarchy
            entity.HasOne(p => p.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(p => p.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureClipData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClipData>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedOnAdd();

            entity.Property(p => p.FormatName).IsRequired().HasMaxLength(60);
            entity.Property(p => p.ClipId).IsRequired();
            entity.Property(p => p.Format).IsRequired();

            // Indexes for performance
            entity.HasIndex(p => p.ClipId).HasDatabaseName("IX_ClipData_ClipId");
            entity.HasIndex(p => p.Format).HasDatabaseName("IX_ClipData_Format");
            entity.HasIndex(p => p.StorageType).HasDatabaseName("IX_ClipData_StorageType");

            // Relationship to Clip
            entity.HasOne(p => p.Clip)
                .WithMany()
                .HasForeignKey(p => p.ClipId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureBlobTxt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlobTxt>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedOnAdd();

            entity.Property(p => p.ClipDataId).IsRequired();
            entity.Property(p => p.ClipId).IsRequired(); // Denormalized
            entity.Property(p => p.Data).IsRequired();

            // Indexes for performance
            entity.HasIndex(p => p.ClipId).HasDatabaseName("IX_BlobTxt_ClipId");
            entity.HasIndex(p => p.ClipDataId).HasDatabaseName("IX_BlobTxt_ClipDataId");

            // Relationship to ClipData
            entity.HasOne(p => p.ClipData)
                .WithMany()
                .HasForeignKey(p => p.ClipDataId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureBlobJpg(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlobJpg>(entity =>
        {
            entity.Property(p => p.ClipId).IsRequired(); // Denormalized
            entity.Property(p => p.Data).IsRequired();

            // Indexes for performance
            entity.HasIndex(p => p.ClipId).HasDatabaseName("IX_BlobJpg_ClipId");
            entity.HasIndex(p => p.ClipDataId).HasDatabaseName("IX_BlobJpg_ClipDataId");

            // Relationship to ClipData
            entity.HasOne(p => p.ClipData)
                .WithMany()
                .HasForeignKey(p => p.ClipDataId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureBlobPng(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlobPng>(entity =>
        {
            entity.Property(p => p.ClipId).IsRequired(); // Denormalized
            entity.Property(p => p.Data).IsRequired();

            // Indexes for performance
            entity.HasIndex(p => p.ClipId).HasDatabaseName("IX_BlobPng_ClipId");
            entity.HasIndex(p => p.ClipDataId).HasDatabaseName("IX_BlobPng_ClipDataId");

            // Relationship to ClipData
            entity.HasOne(p => p.ClipData)
                .WithMany()
                .HasForeignKey(p => p.ClipDataId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureBlobBlob(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlobBlob>(entity =>
        {
            entity.Property(p => p.ClipId).IsRequired(); // Denormalized
            entity.Property(p => p.Data).IsRequired();

            // Indexes for performance
            entity.HasIndex(p => p.ClipId).HasDatabaseName("IX_BlobBlob_ClipId");
            entity.HasIndex(p => p.ClipDataId).HasDatabaseName("IX_BlobBlob_ClipDataId");

            // Relationship to ClipData
            entity.HasOne(p => p.ClipData)
                .WithMany()
                .HasForeignKey(p => p.ClipDataId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureShortcut(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shortcut>(entity =>
        {
            // Use ClipMate 7.5 table name
            entity.ToTable("ShortCut");

            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedOnAdd();

            entity.Property(p => p.Nickname).IsRequired().HasMaxLength(64);
            entity.Property(p => p.ClipId).IsRequired();

            // Unique index on Nickname - PowerPaste requires unique shortcuts
            entity.HasIndex(p => p.Nickname).IsUnique().HasDatabaseName("IX_Shortcuts_Nickname");
            entity.HasIndex(p => p.ClipId).HasDatabaseName("IX_Shortcuts_ClipId");

            // Relationship to Clip
            entity.HasOne(p => p.Clip)
                .WithMany()
                .HasForeignKey(p => p.ClipId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(p => p.Workstation).IsRequired().HasMaxLength(50);
            entity.Property(p => p.LastDate).IsRequired();

            // Indexes for performance
            entity.HasIndex(p => p.Username).HasDatabaseName("IX_Users_Username");
            entity.HasIndex(p => p.Workstation).HasDatabaseName("IX_Users_Workstation");
        });
    }

    private static void ConfigureTemplate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Template>(entity =>
        {
            entity.Property(p => p.Content).IsRequired();
            entity.Property(p => p.CreatedAt).IsRequired();

            entity.HasIndex(p => p.Name).HasDatabaseName("IX_Templates_Name");
            entity.HasIndex(p => p.CollectionId).HasDatabaseName("IX_Templates_CollectionId");
        });
    }

    private static void ConfigureSearchQuery(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SearchQuery>(entity =>
        {
            entity.Property(p => p.QueryText).IsRequired().HasMaxLength(500);
            entity.Property(p => p.CreatedAt).IsRequired();

            entity.HasIndex(p => p.LastExecutedAt).HasDatabaseName("IX_SearchQueries_LastExecutedAt");
            entity.HasIndex(p => p.QueryText).HasDatabaseName("IX_SearchQueries_QueryText");
        });
    }

    private static void ConfigureApplicationFilter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationFilter>(entity =>
        {
            entity.Property(p => p.CreatedAt).IsRequired();

            entity.HasIndex(p => p.Name).HasDatabaseName("IX_ApplicationFilters_Name");
            entity.HasIndex(p => p.ProcessName).HasDatabaseName("IX_ApplicationFilters_ProcessName");
            entity.HasIndex(p => p.IsEnabled).HasDatabaseName("IX_ApplicationFilters_IsEnabled");
        });
    }

    private static void ConfigureMonacoEditorState(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonacoEditorState>(entity =>
        {
            // Use ClipMate 7.5 table name
            entity.ToTable("MonacoEditorState");

            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).ValueGeneratedOnAdd();

            entity.Property(p => p.ClipDataId).IsRequired();
            entity.Property(p => p.Language).IsRequired().HasMaxLength(50);
            entity.Property(p => p.ViewState);
            entity.Property(p => p.LastModified).IsRequired();

            // One-to-one relationship with ClipData
            entity.HasOne(p => p.ClipData)
                .WithMany()
                .HasForeignKey(p => p.ClipDataId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint on ClipDataId (one-to-one)
            entity.HasIndex(p => p.ClipDataId)
                .IsUnique()
                .HasDatabaseName("IX_MonacoEditorStates_ClipDataId");
        });
    }
}
