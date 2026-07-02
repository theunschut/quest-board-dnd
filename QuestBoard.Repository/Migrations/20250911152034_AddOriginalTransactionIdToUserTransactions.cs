using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalTransactionIdToUserTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalTransactionId",
                table: "UserTransactions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTransactions_OriginalTransactionId",
                table: "UserTransactions",
                column: "OriginalTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserTransactions_UserTransactions_OriginalTransactionId",
                table: "UserTransactions",
                column: "OriginalTransactionId",
                principalTable: "UserTransactions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserTransactions_UserTransactions_OriginalTransactionId",
                table: "UserTransactions");

            migrationBuilder.DropIndex(
                name: "IX_UserTransactions_OriginalTransactionId",
                table: "UserTransactions");

            migrationBuilder.DropColumn(
                name: "OriginalTransactionId",
                table: "UserTransactions");
        }
    }
}
