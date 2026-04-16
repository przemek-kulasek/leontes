using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leontes.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProactiveCommunication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Initiator",
                table: "Messages",
                type: "text",
                nullable: false,
                defaultValue: "User");

            migrationBuilder.AddColumn<string>(
                name: "InitiatedBy",
                table: "Conversations",
                type: "text",
                nullable: false,
                defaultValue: "User");

            migrationBuilder.AddColumn<bool>(
                name: "IsProactive",
                table: "Conversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "StoredProactiveEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Urgency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    RequestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Response = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredProactiveEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoredProactiveEvents_RequestId",
                table: "StoredProactiveEvents",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_StoredProactiveEvents_Status",
                table: "StoredProactiveEvents",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoredProactiveEvents");

            migrationBuilder.DropColumn(
                name: "Initiator",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "InitiatedBy",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "IsProactive",
                table: "Conversations");
        }
    }
}
