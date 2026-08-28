using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSeriesRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "EventSeries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "EventSeries",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "EventSeries",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "EventSeries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Events",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_SeriesId_SeriesSlotIndex",
                table: "Events",
                columns: new[] { "SeriesId", "SeriesSlotIndex" },
                unique: true,
                filter: "[SeriesId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_SeriesId_SeriesSlotIndex",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "EventSeries");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "EventSeries");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "EventSeries");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "EventSeries");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Events");
        }
    }
}
