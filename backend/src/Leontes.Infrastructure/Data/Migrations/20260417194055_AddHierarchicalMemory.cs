using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Leontes.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHierarchicalMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "MemoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(384)", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    SourceMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Importance = table.Column<float>(type: "real", nullable: false, defaultValue: 0.5f),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SynapseEntities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    Properties = table.Column<string>(type: "jsonb", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SynapseEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SynapseRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Weight = table.Column<float>(type: "real", nullable: false, defaultValue: 1f),
                    Context = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SynapseRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SynapseRelationships_SynapseEntities_SourceEntityId",
                        column: x => x.SourceEntityId,
                        principalTable: "SynapseEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SynapseRelationships_SynapseEntities_TargetEntityId",
                        column: x => x.TargetEntityId,
                        principalTable: "SynapseEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Embedding",
                table: "MemoryEntries",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "ivfflat")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:lists", 100);

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_SourceConversationId",
                table: "MemoryEntries",
                column: "SourceConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryEntries_Type",
                table: "MemoryEntries",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_SynapseEntities_EntityType",
                table: "SynapseEntities",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_SynapseEntities_EntityType_Name",
                table: "SynapseEntities",
                columns: new[] { "EntityType", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SynapseEntities_Name",
                table: "SynapseEntities",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SynapseRelationships_SourceEntityId_TargetEntityId_Relation~",
                table: "SynapseRelationships",
                columns: new[] { "SourceEntityId", "TargetEntityId", "RelationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SynapseRelationships_TargetEntityId",
                table: "SynapseRelationships",
                column: "TargetEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemoryEntries");

            migrationBuilder.DropTable(
                name: "SynapseRelationships");

            migrationBuilder.DropTable(
                name: "SynapseEntities");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
