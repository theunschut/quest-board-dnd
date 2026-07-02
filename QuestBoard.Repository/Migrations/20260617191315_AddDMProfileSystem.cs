using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddDMProfileSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DungeonMasterProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Bio = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DungeonMasterProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DungeonMasterProfiles_AspNetUsers_Id",
                        column: x => x.Id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DungeonMasterProfileImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ImageData = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DungeonMasterProfileImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DungeonMasterProfileImages_DungeonMasterProfiles_Id",
                        column: x => x.Id,
                        principalTable: "DungeonMasterProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DungeonMasterProfileImages");

            migrationBuilder.DropTable(
                name: "DungeonMasterProfiles");
        }
    }
}
