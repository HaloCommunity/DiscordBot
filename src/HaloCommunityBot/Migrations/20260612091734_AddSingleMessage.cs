using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HaloCommunityBot.Migrations
{
    /// <inheritdoc />
    public partial class AddSingleMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SingleMessageChannelStates",
                columns: table => new
                {
                    ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SingleMessageChannelStates", x => x.ChannelId);
                });

            migrationBuilder.CreateTable(
                name: "SingleMessageRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SingleMessageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SingleMessageRecords_SingleMessageChannelStates_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "SingleMessageChannelStates",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SingleMessageRecords_ChannelId_UserId",
                table: "SingleMessageRecords",
                columns: new[] { "ChannelId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SingleMessageRecords");

            migrationBuilder.DropTable(
                name: "SingleMessageChannelStates");
        }
    }
}
