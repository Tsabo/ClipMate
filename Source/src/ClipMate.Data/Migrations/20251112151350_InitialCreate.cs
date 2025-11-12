using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClipMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", nullable: true),
                    WindowTitlePattern = table.Column<string>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationFilters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    TextContent = table.Column<string>(type: "TEXT", nullable: true),
                    RtfContent = table.Column<string>(type: "TEXT", nullable: true),
                    HtmlContent = table.Column<string>(type: "TEXT", nullable: true),
                    ImageData = table.Column<byte[]>(type: "BLOB", nullable: true),
                    FilePathsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceApplicationName = table.Column<string>(type: "TEXT", nullable: true),
                    SourceApplicationTitle = table.Column<string>(type: "TEXT", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PasteCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: true),
                    CollectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CollectionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentFolderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Folders_Folders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SearchQueries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    QueryText = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsCaseSensitive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRegex = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExecutionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchQueries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SoundEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    SoundFilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Volume = table.Column<float>(type: "REAL", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoundEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CollectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UseCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationFilters_IsEnabled",
                table: "ApplicationFilters",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationFilters_Name",
                table: "ApplicationFilters",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationFilters_ProcessName",
                table: "ApplicationFilters",
                column: "ProcessName");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_CapturedAt",
                table: "Clips",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_CollectionId",
                table: "Clips",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_ContentHash",
                table: "Clips",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_FolderId",
                table: "Clips",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_IsFavorite",
                table: "Clips",
                column: "IsFavorite");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_SourceApplicationName",
                table: "Clips",
                column: "SourceApplicationName");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_Type",
                table: "Clips",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_IsActive",
                table: "Collections",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_Name",
                table: "Collections",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_CollectionId",
                table: "Folders",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_Name",
                table: "Folders",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_ParentFolderId",
                table: "Folders",
                column: "ParentFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Folders_SortOrder",
                table: "Folders",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_SearchQueries_LastExecutedAt",
                table: "SearchQueries",
                column: "LastExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SearchQueries_QueryText",
                table: "SearchQueries",
                column: "QueryText");

            migrationBuilder.CreateIndex(
                name: "IX_SoundEvents_EventType",
                table: "SoundEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SoundEvents_IsEnabled",
                table: "SoundEvents",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_CollectionId",
                table: "Templates",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_Name",
                table: "Templates",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationFilters");

            migrationBuilder.DropTable(
                name: "Clips");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropTable(
                name: "SearchQueries");

            migrationBuilder.DropTable(
                name: "SoundEvents");

            migrationBuilder.DropTable(
                name: "Templates");
        }
    }
}
