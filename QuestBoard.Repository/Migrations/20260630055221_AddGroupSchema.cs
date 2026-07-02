using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    // DEPLOYMENT CONSTRAINT — this migration must co-deploy with the authorization-handler update:
    // This migration deletes Player, DungeonMaster, and Admin rows from AspNetUserRoles (Step 10).
    // The existing DungeonMasterHandler and AdminHandler still read Identity role claims from
    // AspNetUserRoles. After this migration runs, those claims are gone, so any user whose session
    // hits an authorization check WILL fail until the handlers are updated to read
    // UserGroups.GroupRole instead.
    //
    // RULE: Deploy this migration together with the authorization-handler update in a single release.
    //       OR deploy during a maintenance window where no user logs in until that update is also live.
    //       Do NOT deploy this migration alone to a production environment with active users.
    /// <inheritdoc />
    public partial class AddGroupSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create Groups table (parent table — must exist before FK references)
            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Name",
                table: "Groups",
                column: "Name",
                unique: true);

            // Step 2: Create UserGroups junction table (FKs to AspNetUsers and Groups — both must exist first)
            migrationBuilder.CreateTable(
                name: "UserGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    GroupRole = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGroups_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGroups_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserGroups_GroupId",
                table: "UserGroups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroups_UserId_GroupId",
                table: "UserGroups",
                columns: new[] { "UserId", "GroupId" },
                unique: true);

            // Step 3: Add GroupId to Quests with defaultValue:0 — fills existing rows with 0 temporarily
            // (rows will be updated to GroupId=1 in step 6 before the FK constraint is added in step 7)
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Quests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Quests_GroupId",
                table: "Quests",
                column: "GroupId");

            // Step 4: Add GroupId to ShopItems with defaultValue:0 — fills existing rows with 0 temporarily
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "ShopItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ShopItems_GroupId",
                table: "ShopItems",
                column: "GroupId");

            // Step 5: Seed EuphoriaInn group with explicit Id=1.
            // IDENTITY_INSERT is required to insert an explicit Id into an IDENTITY column (Pitfall 2).
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT Groups ON;
INSERT INTO Groups (Id, Name, CreatedAt) VALUES (1, 'EuphoriaInn', GETUTCDATE());
SET IDENTITY_INSERT Groups OFF;
");

            // Step 6: Update all existing Quests and ShopItems to belong to EuphoriaInn (GroupId=1).
            // This MUST happen before adding the FK constraint in steps 7-8 (Pitfall 1 — FK added before data populated).
            migrationBuilder.Sql("UPDATE Quests SET GroupId = 1");
            migrationBuilder.Sql("UPDATE ShopItems SET GroupId = 1");

            // Step 7: Add FK constraint Quests.GroupId → Groups(Id) — AFTER rows are populated (Pitfall 1).
            // NoAction delete behavior: groups must not cascade-delete quests.
            migrationBuilder.AddForeignKey(
                name: "FK_Quests_Groups_GroupId",
                table: "Quests",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            // Step 8: Add FK constraint ShopItems.GroupId → Groups(Id) — AFTER rows are populated (Pitfall 1).
            // NoAction delete behavior: groups must not cascade-delete shop items.
            migrationBuilder.AddForeignKey(
                name: "FK_ShopItems_Groups_GroupId",
                table: "ShopItems",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            // Step 9: Seed UserGroups for all users from AspNetUserRoles with highest-role logic.
            // LEFT JOIN from AspNetUsers ensures users with no role row are included (GroupRole=0/Player).
            // An INNER JOIN would silently drop users with no role.
            // MAX(CASE) logic for multi-role edge case: Admin=3 > DungeonMaster=2 > Player=1 source IDs.
            // GroupRole enum mapping: Admin→2, DungeonMaster→1, Player/None→0.
            // Role IDs in AspNetRoles: Player=1, DungeonMaster=2, Admin=3 (per ConvertIsDungeonMasterToRoles migration).
            migrationBuilder.Sql(@"
INSERT INTO UserGroups (UserId, GroupId, GroupRole)
SELECT
    u.Id,
    1,
    CASE
        WHEN MAX(CASE r.Name WHEN 'Admin' THEN 3 WHEN 'DungeonMaster' THEN 2 ELSE 1 END) = 3 THEN 2
        WHEN MAX(CASE r.Name WHEN 'Admin' THEN 3 WHEN 'DungeonMaster' THEN 2 ELSE 1 END) = 2 THEN 1
        ELSE 0
    END
FROM AspNetUsers u
LEFT JOIN AspNetUserRoles ur ON ur.UserId = u.Id
LEFT JOIN AspNetRoles r ON r.Id = ur.RoleId AND r.Name IN ('Player', 'DungeonMaster', 'Admin')
GROUP BY u.Id
");

            // Step 10: Delete Player/DungeonMaster/Admin rows from AspNetUserRoles.
            // UserGroups now holds per-group roles; AspNetUserRoles is reserved for SuperAdmin only.
            // SuperAdmin rows (none existed at migration time) are untouched by this DELETE.
            migrationBuilder.Sql(@"
DELETE ur FROM AspNetUserRoles ur
INNER JOIN AspNetRoles r ON ur.RoleId = r.Id
WHERE r.Name IN ('Player', 'DungeonMaster', 'Admin')
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down() reverses all 10 steps in reverse order.
            //
            // NOTE: Re-inserting AspNetUserRoles from UserGroups is a best-effort restoration.
            // The migration guarantees schema rollback; full data fidelity is restored on a best-effort basis.
            // If multi-role users existed prior to this migration (an edge case), Down() restores only one role
            // (the highest, consistent with the Up() logic). This is an acceptable trade-off for a
            // forward-only migration path.

            // Reverse step 10: Re-insert Player/DungeonMaster/Admin rows into AspNetUserRoles from UserGroups.
            // GroupRole enum: Player=0, DungeonMaster=1, Admin=2 → Role IDs: Player=1, DungeonMaster=2, Admin=3.
            migrationBuilder.Sql(@"
INSERT INTO AspNetUserRoles (UserId, RoleId)
SELECT
    ug.UserId,
    CASE ug.GroupRole
        WHEN 2 THEN 3
        WHEN 1 THEN 2
        ELSE 1
    END
FROM UserGroups ug
WHERE ug.GroupId = 1
");

            // Reverse steps 9 (drop UserGroups data — table dropped below)
            // Reverse steps 7-8: Drop FK constraints before dropping columns
            migrationBuilder.DropForeignKey(
                name: "FK_ShopItems_Groups_GroupId",
                table: "ShopItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Quests_Groups_GroupId",
                table: "Quests");

            // Reverse steps 3-4: Drop GroupId columns and their indexes
            migrationBuilder.DropIndex(
                name: "IX_ShopItems_GroupId",
                table: "ShopItems");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "ShopItems");

            migrationBuilder.DropIndex(
                name: "IX_Quests_GroupId",
                table: "Quests");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Quests");

            // Reverse step 2: Drop UserGroups table (FKs to Groups and AspNetUsers cascade)
            migrationBuilder.DropTable(
                name: "UserGroups");

            // Reverse step 1: Drop Groups table
            migrationBuilder.DropTable(
                name: "Groups");
        }
    }
}
