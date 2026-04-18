using System.Text.Json;
using Leontes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leontes.Infrastructure.Data.Configurations;

public sealed class DecisionRecordConfiguration : IEntityTypeConfiguration<DecisionRecord>
{
    public void Configure(EntityTypeBuilder<DecisionRecord> builder)
    {
        builder.ToTable("DecisionRecords");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.StageTraceId).IsRequired();
        builder.Property(d => d.DecisionType).IsRequired().HasMaxLength(64);
        builder.Property(d => d.Question).IsRequired();
        builder.Property(d => d.Chosen).IsRequired();
        builder.Property(d => d.Rationale).IsRequired();

        builder.Property(d => d.Candidates)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<IReadOnlyList<DecisionCandidate>>(v, (JsonSerializerOptions?)null) ?? new List<DecisionCandidate>())
            .IsRequired();

        builder.HasIndex(d => d.StageTraceId);
    }
}
