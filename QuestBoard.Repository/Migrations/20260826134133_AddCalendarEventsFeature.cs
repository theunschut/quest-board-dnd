using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEventsFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnchorDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IntervalWeeks = table.Column<int>(type: "int", nullable: false),
                    WeekDay = table.Column<int>(type: "int", nullable: false),
                    CycleMask = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventSeries_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    SeriesId = table.Column<int>(type: "int", nullable: true),
                    SeriesSlotIndex = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Events_EventSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "EventSeries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Events_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EventSignups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Availability = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSignups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventSignups_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EventSignups_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_GroupId_Date",
                table: "Events",
                columns: new[] { "GroupId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_SeriesId",
                table: "Events",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSeries_GroupId",
                table: "EventSeries",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSignups_EventId_UserId",
                table: "EventSignups",
                columns: new[] { "EventId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventSignups_UserId",
                table: "EventSignups",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventSignups");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "EventSeries");
        }
    }
}
