using Leontes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leontes.Infrastructure.Data.Configurations;

public sealed class MetricsSummaryConfiguration : IEntityTypeConfiguration<MetricsSummary>
{
    public void Configure(EntityTypeBuilder<MetricsSummary> builder)
    {
        builder.ToTable("MetricsSummaries");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.PeriodStart).IsRequired();
        builder.Property(m => m.PeriodEnd).IsRequired();

        builder.HasIndex(m => m.PeriodStart).IsDescending();
        builder.HasIndex(m => new { m.PeriodStart, m.PeriodEnd }).IsUnique();
    }
}
