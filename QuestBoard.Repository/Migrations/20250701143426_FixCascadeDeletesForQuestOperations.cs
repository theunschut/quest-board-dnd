using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class FixCascadeDeletesForQuestOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerDateVotes_PlayerSignups_PlayerSignupId",
                table: "PlayerDateVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerDateVotes_ProposedDates_ProposedDateId",
                table: "PlayerDateVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProposedDates_Quests_QuestId",
                table: "ProposedDates");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerDateVotes_PlayerSignups_PlayerSignupId",
                table: "PlayerDateVotes",
                column: "PlayerSignupId",
                principalTable: "PlayerSignups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerDateVotes_ProposedDates_ProposedDateId",
                table: "PlayerDateVotes",
                column: "ProposedDateId",
                principalTable: "ProposedDates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_PlayerDateVotes_PlayerSignups_PlayerSignupId",
                table: "PlayerDateVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_PlayerDateVotes_ProposedDates_ProposedDateId",
                table: "PlayerDateVotes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProposedDates_Quests_QuestId",
                table: "ProposedDates");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerDateVotes_PlayerSignups_PlayerSignupId",
                table: "PlayerDateVotes",
                column: "PlayerSignupId",
                principalTable: "PlayerSignups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerDateVotes_ProposedDates_ProposedDateId",
                table: "PlayerDateVotes",
                column: "ProposedDateId",
                principalTable: "ProposedDates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProposedDates_Quests_QuestId",
                table: "ProposedDates",
                column: "QuestId",
                principalTable: "Quests",
                principalColumn: "Id");
        }
    }
}
