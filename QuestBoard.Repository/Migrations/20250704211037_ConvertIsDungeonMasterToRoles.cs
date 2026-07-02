using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class ConvertIsDungeonMasterToRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create roles
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
                values: new object[,]
                {
                    { 1, "Player", "PLAYER", Guid.NewGuid().ToString() },
                    { 2, "DungeonMaster", "DUNGEONMASTER", Guid.NewGuid().ToString() },
                    { 3, "Admin", "ADMIN", Guid.NewGuid().ToString() }
                });

            // Assign roles based on IsDungeonMaster boolean
            migrationBuilder.Sql(@"
                INSERT INTO AspNetUserRoles (UserId, RoleId)
                SELECT Id, CASE WHEN IsDungeonMaster = 1 THEN 2 ELSE 1 END
                FROM AspNetUsers
            ");

            // Drop the IsDungeonMaster column
            migrationBuilder.DropColumn(
                name: "IsDungeonMaster",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add back the IsDungeonMaster column
            migrationBuilder.AddColumn<bool>(
                name: "IsDungeonMaster",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Set IsDungeonMaster based on roles
            migrationBuilder.Sql(@"
                UPDATE AspNetUsers 
                SET IsDungeonMaster = CASE WHEN EXISTS (
                    SELECT 1 FROM AspNetUserRoles ur 
                    INNER JOIN AspNetRoles r ON ur.RoleId = r.Id 
                    WHERE ur.UserId = AspNetUsers.Id AND r.Name IN ('DungeonMaster', 'Admin')
                ) THEN 1 ELSE 0 END
            ");

            // Remove user-role assignments
            migrationBuilder.Sql("DELETE FROM AspNetUserRoles");

            // Remove roles
            migrationBuilder.Sql("DELETE FROM AspNetRoles WHERE Name IN ('Player', 'DungeonMaster', 'Admin')");
        }
    }
}
