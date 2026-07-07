using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class RenameImageColumnsAddCropped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RenameColumn (not DropColumn+AddColumn) preserves every existing photo's bytes
            // under the new column name instead of destroying them.
            migrationBuilder.RenameColumn(
                name: "ImageData",
                table: "DungeonMasterProfileImages",
                newName: "OriginalImageData");

            migrationBuilder.RenameColumn(
                name: "ImageData",
                table: "ContactImages",
                newName: "OriginalImageData");

            migrationBuilder.RenameColumn(
                name: "ImageData",
                table: "CharacterImages",
                newName: "OriginalImageData");

            migrationBuilder.AddColumn<byte[]>(
                name: "CroppedImageData",
                table: "DungeonMasterProfileImages",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "CroppedImageData",
                table: "ContactImages",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "CroppedImageData",
                table: "CharacterImages",
                type: "varbinary(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CroppedImageData",
                table: "DungeonMasterProfileImages");

            migrationBuilder.DropColumn(
                name: "CroppedImageData",
                table: "ContactImages");

            migrationBuilder.DropColumn(
                name: "CroppedImageData",
                table: "CharacterImages");

            migrationBuilder.RenameColumn(
                name: "OriginalImageData",
                table: "DungeonMasterProfileImages",
                newName: "ImageData");

            migrationBuilder.RenameColumn(
                name: "OriginalImageData",
                table: "ContactImages",
                newName: "ImageData");

            migrationBuilder.RenameColumn(
                name: "OriginalImageData",
                table: "CharacterImages",
                newName: "ImageData");
        }
    }
}
