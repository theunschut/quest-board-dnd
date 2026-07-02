using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class EnableCascadeDeleteForQuestProposedDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProposedDates_Quests_QuestId",
                table: "ProposedDates");

            migrationBuilder.AddForeignKey(
                name: "FK_ProposedDates_Quests_QuestId",
                table: "ProposedDates",
                column: "QuestId",
                principalTable: "Quests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProposedDates_Quests_QuestId",
                table: "ProposedDates");

            migrationBuilder.AddForeignKey(
                name: "FK_ProposedDates_Quests_QuestId",
                table: "ProposedDates",
                column: "QuestId",
                principalTable: "Quests",
                principalColumn: "Id");
        }
    }
}
