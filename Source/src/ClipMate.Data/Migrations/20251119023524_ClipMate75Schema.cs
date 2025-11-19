using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClipMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClipMate75Schema : Migration
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
                    CollectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Creator = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SortKey = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    CustomTitle = table.Column<bool>(type: "INTEGER", nullable: false),
                    Locale = table.Column<int>(type: "INTEGER", nullable: false),
                    WrapCheck = table.Column<bool>(type: "INTEGER", nullable: false),
                    Encrypted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Icons = table.Column<int>(type: "INTEGER", nullable: false),
                    Del = table.Column<bool>(type: "INTEGER", nullable: false),
                    Size = table.Column<int>(type: "INTEGER", nullable: false),
                    DelDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Checksum = table.Column<int>(type: "INTEGER", nullable: false),
                    ViewTab = table.Column<int>(type: "INTEGER", nullable: false),
                    Macro = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceApplicationName = table.Column<string>(type: "TEXT", nullable: true),
                    SourceApplicationTitle = table.Column<string>(type: "TEXT", nullable: true),
                    PasteCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false)
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
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParentGuid = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    LmType = table.Column<int>(type: "INTEGER", nullable: false),
                    ListType = table.Column<int>(type: "INTEGER", nullable: false),
                    SortKey = table.Column<int>(type: "INTEGER", nullable: false),
                    IlIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    RetentionLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    NewClipsGo = table.Column<int>(type: "INTEGER", nullable: false),
                    AcceptNewClips = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReadOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    AcceptDuplicates = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortColumn = table.Column<int>(type: "INTEGER", nullable: false),
                    SortAscending = table.Column<bool>(type: "INTEGER", nullable: false),
                    Encrypted = table.Column<bool>(type: "INTEGER", nullable: false),
                    Favorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    LastUpdateTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastKnownCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Sql = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Icon = table.Column<string>(type: "TEXT", nullable: true),
                    VirtualCollectionQuery = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collections_Collections_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Collections",
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

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Workstation = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClipData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FormatName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Format = table.Column<int>(type: "INTEGER", nullable: false),
                    Size = table.Column<int>(type: "INTEGER", nullable: false),
                    StorageType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClipData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClipData_Clips_ClipId",
                        column: x => x.ClipId,
                        principalTable: "Clips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shortcuts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ClipGuid = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shortcuts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shortcuts_Clips_ClipId",
                        column: x => x.ClipId,
                        principalTable: "Clips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlobBlob",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipDataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlobBlob", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlobBlob_ClipData_ClipDataId",
                        column: x => x.ClipDataId,
                        principalTable: "ClipData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlobJpg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipDataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlobJpg", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlobJpg_ClipData_ClipDataId",
                        column: x => x.ClipDataId,
                        principalTable: "ClipData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlobPng",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipDataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlobPng", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlobPng_ClipData_ClipDataId",
                        column: x => x.ClipDataId,
                        principalTable: "ClipData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlobTxt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipDataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlobTxt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlobTxt_ClipData_ClipDataId",
                        column: x => x.ClipDataId,
                        principalTable: "ClipData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_BlobBlob_ClipDataId",
                table: "BlobBlob",
                column: "ClipDataId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobBlob_ClipId",
                table: "BlobBlob",
                column: "ClipId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobJpg_ClipDataId",
                table: "BlobJpg",
                column: "ClipDataId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobJpg_ClipId",
                table: "BlobJpg",
                column: "ClipId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobPng_ClipDataId",
                table: "BlobPng",
                column: "ClipDataId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobPng_ClipId",
                table: "BlobPng",
                column: "ClipId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobTxt_ClipDataId",
                table: "BlobTxt",
                column: "ClipDataId");

            migrationBuilder.CreateIndex(
                name: "IX_BlobTxt_ClipId",
                table: "BlobTxt",
                column: "ClipId");

            migrationBuilder.CreateIndex(
                name: "IX_ClipData_ClipId",
                table: "ClipData",
                column: "ClipId");

            migrationBuilder.CreateIndex(
                name: "IX_ClipData_Format",
                table: "ClipData",
                column: "Format");

            migrationBuilder.CreateIndex(
                name: "IX_ClipData_StorageType",
                table: "ClipData",
                column: "StorageType");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_CapturedAt",
                table: "Clips",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_Checksum",
                table: "Clips",
                column: "Checksum");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_CollectionId",
                table: "Clips",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_ContentHash",
                table: "Clips",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_Del",
                table: "Clips",
                column: "Del");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_FolderId",
                table: "Clips",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_IsFavorite",
                table: "Clips",
                column: "IsFavorite");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_SortKey",
                table: "Clips",
                column: "SortKey");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_SourceApplicationName",
                table: "Clips",
                column: "SourceApplicationName");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_Type",
                table: "Clips",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_Favorite",
                table: "Collections",
                column: "Favorite");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_LmType",
                table: "Collections",
                column: "LmType");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_NewClipsGo",
                table: "Collections",
                column: "NewClipsGo");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_ParentId",
                table: "Collections",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_SortKey",
                table: "Collections",
                column: "SortKey");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_Title",
                table: "Collections",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_SearchQueries_LastExecutedAt",
                table: "SearchQueries",
                column: "LastExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SearchQueries_QueryText",
                table: "SearchQueries",
                column: "QueryText");

            migrationBuilder.CreateIndex(
                name: "IX_Shortcuts_ClipId",
                table: "Shortcuts",
                column: "ClipId");

            migrationBuilder.CreateIndex(
                name: "IX_Shortcuts_Nickname",
                table: "Shortcuts",
                column: "Nickname",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Workstation",
                table: "Users",
                column: "Workstation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationFilters");

            migrationBuilder.DropTable(
                name: "BlobBlob");

            migrationBuilder.DropTable(
                name: "BlobJpg");

            migrationBuilder.DropTable(
                name: "BlobPng");

            migrationBuilder.DropTable(
                name: "BlobTxt");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropTable(
                name: "SearchQueries");

            migrationBuilder.DropTable(
                name: "Shortcuts");

            migrationBuilder.DropTable(
                name: "SoundEvents");

            migrationBuilder.DropTable(
                name: "Templates");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ClipData");

            migrationBuilder.DropTable(
                name: "Clips");
        }
    }
}
