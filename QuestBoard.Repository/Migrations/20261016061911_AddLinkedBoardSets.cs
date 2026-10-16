using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedBoardSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinkedBoardSetId",
                table: "Groups",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LinkedBoardSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BoardType = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkedBoardSets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Groups_LinkedBoardSetId",
                table: "Groups",
                column: "LinkedBoardSetId");

            migrationBuilder.CreateIndex(
                name: "IX_LinkedBoardSets_Name",
                table: "LinkedBoardSets",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_LinkedBoardSets_LinkedBoardSetId",
                table: "Groups",
                column: "LinkedBoardSetId",
                principalTable: "LinkedBoardSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Groups_LinkedBoardSets_LinkedBoardSetId",
                table: "Groups");

            migrationBuilder.DropTable(
                name: "LinkedBoardSets");

            migrationBuilder.DropIndex(
                name: "IX_Groups_LinkedBoardSetId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "LinkedBoardSetId",
                table: "Groups");
        }
    }
}
