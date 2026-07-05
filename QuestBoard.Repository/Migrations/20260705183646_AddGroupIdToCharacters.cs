using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupIdToCharacters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add GroupId to Characters with defaultValue:0 — fills existing rows with 0
            // temporarily (rows are updated to GroupId=1 in step 2, before the FK constraint is
            // added in step 3, to avoid a constraint violation on rows without a real group yet).
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Characters",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Step 2: Backfill all existing Characters rows to group 1 — the only group that
            // existed before multi-tenancy. This MUST run before the FK constraint is added below.
            migrationBuilder.Sql("UPDATE Characters SET GroupId = 1");

            // Step 3: Add FK constraint Characters.GroupId → Groups(Id) — AFTER rows are populated.
            // NoAction delete behavior: groups must not cascade-delete characters.
            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Groups_GroupId",
                table: "Characters",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            // Step 4: Index for group-scoped lookups.
            migrationBuilder.CreateIndex(
                name: "IX_Characters_GroupId",
                table: "Characters",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Characters_GroupId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Groups_GroupId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Characters");
        }
    }
}
