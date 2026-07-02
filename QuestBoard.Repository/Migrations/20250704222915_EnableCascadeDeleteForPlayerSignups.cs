using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class EnableCascadeDeleteForPlayerSignups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerSignups_AspNetUsers_PlayerId",
                table: "PlayerSignups");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerSignups_AspNetUsers_PlayerId",
                table: "PlayerSignups",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlayerSignups_AspNetUsers_PlayerId",
                table: "PlayerSignups");

            migrationBuilder.AddForeignKey(
                name: "FK_PlayerSignups_AspNetUsers_PlayerId",
                table: "PlayerSignups",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
