using System.Text.Json;
using Leontes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leontes.Infrastructure.Data.Configurations;

public sealed class BudgetPolicyConfiguration : IEntityTypeConfiguration<BudgetPolicy>
{
    public void Configure(EntityTypeBuilder<BudgetPolicy> builder)
    {
        builder.ToTable("BudgetPolicies");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.DailyTokenBudget).IsRequired();
        builder.Property(p => p.WarningThresholdPercent).IsRequired();
        builder.Property(p => p.ThrottleThresholdPercent).IsRequired();
        builder.Property(p => p.HardStopEnabled).IsRequired();
        builder.Property(p => p.HardStopThresholdPercent).IsRequired();

        builder.Property(p => p.FeatureAllocations)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, int>>(v, (JsonSerializerOptions?)null) ?? new())
            .IsRequired();
    }
}
