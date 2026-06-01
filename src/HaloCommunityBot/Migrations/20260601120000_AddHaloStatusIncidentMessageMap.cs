using DiscordBot.Core.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace HaloCommunityBot.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(HaloCommunityBotContext))]
    [Migration("20260601120000_AddHaloStatusIncidentMessageMap")]
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
