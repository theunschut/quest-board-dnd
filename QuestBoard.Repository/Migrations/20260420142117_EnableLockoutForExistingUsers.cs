using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuestBoard.Repository.Migrations
{
    /// <inheritdoc />
    public partial class EnableLockoutForExistingUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill LockoutEnabled = 1 for all existing users so the
            // Identity lockout policy configured in Program.cs applies
            // to every account, not just newly-registered ones.
            migrationBuilder.Sql("UPDATE AspNetUsers SET LockoutEnabled = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE AspNetUsers SET LockoutEnabled = 0");
        }
    }
}
