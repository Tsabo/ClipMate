using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClipMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMonacoEditorState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonacoEditorStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClipDataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ViewState = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonacoEditorStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonacoEditorStates_ClipData_ClipDataId",
                        column: x => x.ClipDataId,
                        principalTable: "ClipData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonacoEditorStates_ClipDataId",
                table: "MonacoEditorStates",
                column: "ClipDataId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonacoEditorStates");
        }
    }
}
