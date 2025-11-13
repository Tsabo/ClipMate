using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClipMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FolderType",
                table: "Folders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FolderType",
                table: "Folders");
        }
    }
}
