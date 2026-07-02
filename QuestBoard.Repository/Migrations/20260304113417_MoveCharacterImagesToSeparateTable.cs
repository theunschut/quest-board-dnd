using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class MoveCharacterImagesToSeparateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CharacterImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ImageData = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterImages_Characters_Id",
                        column: x => x.Id,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"
                INSERT INTO CharacterImages (Id, ImageData)
                SELECT Id, ProfilePicture
                FROM Characters
                WHERE ProfilePicture IS NOT NULL
            ");

            migrationBuilder.DropColumn(
                name: "ProfilePicture",
                table: "Characters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ProfilePicture",
                table: "Characters",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE c
                SET c.ProfilePicture = ci.ImageData
                FROM Characters c
                INNER JOIN CharacterImages ci ON ci.Id = c.Id
            ");

            migrationBuilder.DropTable(
                name: "CharacterImages");
        }
    }
}
