using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedEntryRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FeedRevisedAt",
                table: "Quests",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "FeedRevision",
                table: "Quests",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "FeedRevisedAt",
                table: "Events",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "FeedRevision",
                table: "Events",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Every entry already published goes out once with a higher revision and a fresh
            // stamp, so calendars holding a stale copy replace it.
            migrationBuilder.Sql(
                "UPDATE [Events] SET [FeedRevision] = [FeedRevision] + 1, [FeedRevisedAt] = SYSUTCDATETIME()");

            migrationBuilder.Sql(
                "UPDATE [Quests] SET [FeedRevision] = [FeedRevision] + 1, [FeedRevisedAt] = SYSUTCDATETIME()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedRevisedAt",
                table: "Quests");

            migrationBuilder.DropColumn(
                name: "FeedRevision",
                table: "Quests");

            migrationBuilder.DropColumn(
                name: "FeedRevisedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "FeedRevision",
                table: "Events");
        }
    }
}
