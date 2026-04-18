using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leontes.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCostControlTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyTokenBudget = table.Column<int>(type: "integer", nullable: false),
                    WarningThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    ThrottleThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    HardStopEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    HardStopThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    FeatureAllocations = table.Column<string>(type: "jsonb", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokenUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Feature = table.Column<string>(type: "text", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    ModelId = table.Column<string>(type: "text", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenUsageRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokenUsageRecords_Feature_Timestamp",
                table: "TokenUsageRecords",
                columns: new[] { "Feature", "Timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_TokenUsageRecords_Timestamp",
                table: "TokenUsageRecords",
                column: "Timestamp",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetPolicies");

            migrationBuilder.DropTable(
                name: "TokenUsageRecords");
        }
    }
}
