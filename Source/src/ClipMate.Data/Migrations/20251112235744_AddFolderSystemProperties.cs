using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClipMate.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderSystemProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IconName",
                table: "Folders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemFolder",
                table: "Folders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IconName",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "IsSystemFolder",
                table: "Folders");
        }
    }
}
