using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardTypeToGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BoardType",
                table: "Groups",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoardType",
                table: "Groups");
        }
    }
}
