using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leontes.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddObservabilityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetricsSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalRequests = table.Column<int>(type: "integer", nullable: false),
                    SuccessfulRequests = table.Column<int>(type: "integer", nullable: false),
                    DegradedRequests = table.Column<int>(type: "integer", nullable: false),
                    FailedRequests = table.Column<int>(type: "integer", nullable: false),
                    MedianLatencyMs = table.Column<double>(type: "double precision", nullable: false),
                    P95LatencyMs = table.Column<double>(type: "double precision", nullable: false),
                    TotalInputTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    MemoryHitRate = table.Column<double>(type: "double precision", nullable: false),
                    ToolSuccessRate = table.Column<double>(type: "double precision", nullable: false),
                    SentinelEventsProcessed = table.Column<int>(type: "integer", nullable: false),
                    SentinelEventsDropped = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricsSummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineTraces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    TotalInputTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalOutputTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ConfidenceOverall = table.Column<double>(type: "double precision", nullable: true),
                    ConfidenceMemorySupport = table.Column<double>(type: "double precision", nullable: true),
                    ConfidenceGraphSupport = table.Column<double>(type: "double precision", nullable: true),
                    ConfidenceConversationClarity = table.Column<double>(type: "double precision", nullable: true),
                    ConfidenceToolReliability = table.Column<double>(type: "double precision", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineTraces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StageTraces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageTraces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageTraces_PipelineTraces_PipelineTraceId",
                        column: x => x.PipelineTraceId,
                        principalTable: "PipelineTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DecisionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StageTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Chosen = table.Column<string>(type: "text", nullable: false),
                    Rationale = table.Column<string>(type: "text", nullable: false),
                    Candidates = table.Column<string>(type: "jsonb", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DecisionRecords_StageTraces_StageTraceId",
                        column: x => x.StageTraceId,
                        principalTable: "StageTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionRecords_StageTraceId",
                table: "DecisionRecords",
                column: "StageTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_MetricsSummaries_PeriodStart",
                table: "MetricsSummaries",
                column: "PeriodStart",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_MetricsSummaries_PeriodStart_PeriodEnd",
                table: "MetricsSummaries",
                columns: new[] { "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PipelineTraces_ConversationId",
                table: "PipelineTraces",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineTraces_RequestId",
                table: "PipelineTraces",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineTraces_StartedAt",
                table: "PipelineTraces",
                column: "StartedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_StageTraces_PipelineTraceId",
                table: "StageTraces",
                column: "PipelineTraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecisionRecords");

            migrationBuilder.DropTable(
                name: "MetricsSummaries");

            migrationBuilder.DropTable(
                name: "StageTraces");

            migrationBuilder.DropTable(
                name: "PipelineTraces");
        }
    }
}
