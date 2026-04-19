using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Leontes.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class IncreaseEmbeddingDimensionsTo768 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing vectors are 384-dim and cannot be cast to 768-dim — clear before altering.
            migrationBuilder.Sql("""UPDATE "SynapseEntities" SET "Embedding" = NULL""");
            migrationBuilder.Sql("DELETE FROM \"MemoryEntries\"");

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "SynapseEntities",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(384)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "MemoryEntries",
                type: "vector(768)",
                nullable: false,
                oldClrType: typeof(Vector),
                oldType: "vector(384)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "SynapseEntities",
                type: "vector(384)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(768)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "MemoryEntries",
                type: "vector(384)",
                nullable: false,
                oldClrType: typeof(Vector),
                oldType: "vector(768)");
        }
    }
}
