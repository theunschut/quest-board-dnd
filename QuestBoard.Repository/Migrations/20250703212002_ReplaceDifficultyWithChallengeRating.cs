using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDifficultyWithChallengeRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Quests");

            migrationBuilder.AddColumn<decimal>(
                name: "ChallengeRating",
                table: "Quests",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChallengeRating",
                table: "Quests");

            migrationBuilder.AddColumn<int>(
                name: "Difficulty",
                table: "Quests",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
