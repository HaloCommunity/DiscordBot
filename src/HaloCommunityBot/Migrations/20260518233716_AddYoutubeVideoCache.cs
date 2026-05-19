using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaloCommunityBot.Migrations
{
    /// <inheritdoc />
    public partial class AddYoutubeVideoCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedPostStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FeedType = table.Column<string>(type: "TEXT", nullable: false),
                    SourceId = table.Column<string>(type: "TEXT", nullable: false),
                    LastPostedItemId = table.Column<string>(type: "TEXT", nullable: true),
                    RecentVideoIds = table.Column<string>(type: "TEXT", nullable: true),
                    LastCheckedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedPostStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YoutubeMonitorSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ForumChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    PollIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 15),
                    DefaultPostTitleTemplate = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[{ChannelName}] {VideoTitle}"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoutubeMonitorSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YoutubeTrackedChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelId = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelName = table.Column<string>(type: "TEXT", nullable: false),
                    PostTitleTemplate = table.Column<string>(type: "TEXT", nullable: true),
                    KeywordFilters = table.Column<string>(type: "TEXT", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoutubeTrackedChannels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedPostStates_FeedType_SourceId",
                table: "FeedPostStates",
                columns: new[] { "FeedType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YoutubeTrackedChannels_ChannelId",
                table: "YoutubeTrackedChannels",
                column: "ChannelId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedPostStates");

            migrationBuilder.DropTable(
                name: "YoutubeMonitorSettings");

            migrationBuilder.DropTable(
                name: "YoutubeTrackedChannels");
        }
    }
}
