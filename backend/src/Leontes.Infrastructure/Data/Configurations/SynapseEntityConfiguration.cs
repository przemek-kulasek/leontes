using System.Globalization;
using System.Text.Json;
using Leontes.Application.Configuration;
using Leontes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leontes.Infrastructure.Data.Configurations;

public sealed class SynapseEntityConfiguration : IEntityTypeConfiguration<SynapseEntity>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<SynapseEntity> builder)
    {
        builder.ToTable("SynapseEntities");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.EntityType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.Embedding)
            .HasColumnType(FormattableString.Invariant($"vector({MemoryOptions.EmbeddingDimensions})"));

        builder.Property(e => e.Properties)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions)
                     ?? new Dictionary<string, string>(),
                new ValueComparer<Dictionary<string, string>>(
                    (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                    d => d.Aggregate(0, (hash, pair) => hash ^ HashCode.Combine(pair.Key, pair.Value)),
                    d => d.ToDictionary(p => p.Key, p => p.Value)))
            .IsRequired();

        builder.HasIndex(e => e.Name);
        builder.HasIndex(e => e.EntityType);
        builder.HasIndex(e => new { e.EntityType, e.Name }).IsUnique();
    }
}
