using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddContactTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContactTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, collation: "SQL_Latin1_General_CP1_CI_AS"),
                    GroupId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactTags_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ContactContactTags",
                columns: table => new
                {
                    ContactsId = table.Column<int>(type: "int", nullable: false),
                    TagsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactContactTags", x => new { x.ContactsId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_ContactContactTags_ContactTags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "ContactTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContactContactTags_Contacts_ContactsId",
                        column: x => x.ContactsId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactContactTags_TagsId",
                table: "ContactContactTags",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactTags_GroupId_Name",
                table: "ContactTags",
                columns: new[] { "GroupId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactContactTags");

            migrationBuilder.DropTable(
                name: "ContactTags");
        }
    }
}
