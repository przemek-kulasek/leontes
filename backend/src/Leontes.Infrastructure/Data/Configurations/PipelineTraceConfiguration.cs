using Leontes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leontes.Infrastructure.Data.Configurations;

public sealed class PipelineTraceConfiguration : IEntityTypeConfiguration<PipelineTrace>
{
    public void Configure(EntityTypeBuilder<PipelineTrace> builder)
    {
        builder.ToTable("PipelineTraces");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.RequestId).IsRequired();
        builder.Property(t => t.ConversationId).IsRequired();
        builder.Property(t => t.StartedAt).IsRequired();
        builder.Property(t => t.Outcome).IsRequired().HasConversion<string>();
        builder.Property(t => t.TotalInputTokens).IsRequired().HasDefaultValue(0);
        builder.Property(t => t.TotalOutputTokens).IsRequired().HasDefaultValue(0);

        builder.HasIndex(t => t.RequestId);
        builder.HasIndex(t => t.ConversationId);
        builder.HasIndex(t => t.StartedAt).IsDescending();

        builder.HasMany(t => t.Stages)
            .WithOne(s => s.PipelineTrace!)
            .HasForeignKey(s => s.PipelineTraceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
