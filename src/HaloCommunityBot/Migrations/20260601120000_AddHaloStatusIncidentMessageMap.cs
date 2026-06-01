using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaloCommunityBot.Migrations
{
    /// <inheritdoc />
    public partial class AddHaloStatusIncidentMessageMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecentIncidentMessageIds",
                table: "FeedPostStates",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecentIncidentMessageIds",
                table: "FeedPostStates");
        }
    }
}
