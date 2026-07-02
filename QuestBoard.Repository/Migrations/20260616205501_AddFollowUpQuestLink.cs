using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddFollowUpQuestLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalQuestId",
                table: "Quests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quests_OriginalQuestId",
                table: "Quests",
                column: "OriginalQuestId",
                unique: true,
                filter: "[OriginalQuestId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Quests_Quests_OriginalQuestId",
                table: "Quests",
                column: "OriginalQuestId",
                principalTable: "Quests",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quests_Quests_OriginalQuestId",
                table: "Quests");

            migrationBuilder.DropIndex(
                name: "IX_Quests_OriginalQuestId",
                table: "Quests");

            migrationBuilder.DropColumn(
                name: "OriginalQuestId",
                table: "Quests");
        }
    }
}
