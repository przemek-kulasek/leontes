using Leontes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leontes.Infrastructure.Data.Configurations;

public sealed class TokenUsageRecordConfiguration : IEntityTypeConfiguration<TokenUsageRecord>
{
    public void Configure(EntityTypeBuilder<TokenUsageRecord> builder)
    {
        builder.ToTable("TokenUsageRecords");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Feature).IsRequired();
        builder.Property(t => t.Operation).IsRequired();
        builder.Property(t => t.ModelId).IsRequired();
        builder.Property(t => t.InputTokens).IsRequired();
        builder.Property(t => t.OutputTokens).IsRequired();
        builder.Property(t => t.Timestamp).IsRequired();

        builder.HasIndex(t => t.Timestamp).IsDescending();
        builder.HasIndex(t => new { t.Feature, t.Timestamp }).IsDescending(false, true);
    }
}
