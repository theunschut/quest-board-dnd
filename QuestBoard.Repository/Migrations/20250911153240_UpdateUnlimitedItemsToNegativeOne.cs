using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUnlimitedItemsToNegativeOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update existing items with quantity 0 to -1 (unlimited)
            // This fixes the bug where sold out items became unlimited
            migrationBuilder.Sql("UPDATE ShopItems SET Quantity = -1 WHERE Quantity = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert unlimited items back to 0 (old system)
            migrationBuilder.Sql("UPDATE ShopItems SET Quantity = 0 WHERE Quantity = -1");
        }
    }
}
