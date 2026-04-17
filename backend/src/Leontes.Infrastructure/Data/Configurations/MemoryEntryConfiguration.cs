using System.Globalization;
using Leontes.Application.Configuration;
using Leontes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leontes.Infrastructure.Data.Configurations;

public sealed class MemoryEntryConfiguration : IEntityTypeConfiguration<MemoryEntry>
{
    public void Configure(EntityTypeBuilder<MemoryEntry> builder)
    {
        builder.ToTable("MemoryEntries");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content)
            .IsRequired();

        builder.Property(m => m.Type)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(m => m.Embedding)
            .HasColumnType(FormattableString.Invariant($"vector({MemoryOptions.EmbeddingDimensions})"))
            .IsRequired();

        builder.Property(m => m.Importance)
            .IsRequired()
            .HasDefaultValue(0.5f);

        builder.Property(m => m.AccessCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.HasIndex(m => m.Type);
        builder.HasIndex(m => m.SourceConversationId);

        builder.HasIndex(m => m.Embedding)
            .HasMethod("ivfflat")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("lists", 100);
    }
}
