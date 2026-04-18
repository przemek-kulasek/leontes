using Leontes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leontes.Infrastructure.Data.Configurations;

public sealed class StageTraceConfiguration : IEntityTypeConfiguration<StageTrace>
{
    public void Configure(EntityTypeBuilder<StageTrace> builder)
    {
        builder.ToTable("StageTraces");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.PipelineTraceId).IsRequired();
        builder.Property(s => s.StageName).IsRequired().HasMaxLength(64);
        builder.Property(s => s.StartedAt).IsRequired();
        builder.Property(s => s.Outcome).IsRequired().HasConversion<string>();
        builder.Property(s => s.InputTokens).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.OutputTokens).IsRequired().HasDefaultValue(0);

        builder.HasIndex(s => s.PipelineTraceId);

        builder.HasMany(s => s.Decisions)
            .WithOne(d => d.StageTrace!)
            .HasForeignKey(d => d.StageTraceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
