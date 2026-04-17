using Leontes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leontes.Infrastructure.Data.Configurations;

public sealed class SynapseRelationshipConfiguration : IEntityTypeConfiguration<SynapseRelationship>
{
    public void Configure(EntityTypeBuilder<SynapseRelationship> builder)
    {
        builder.ToTable("SynapseRelationships");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.RelationType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Weight)
            .IsRequired()
            .HasDefaultValue(1.0f);

        builder.Property(r => r.Context)
            .HasMaxLength(500);

        builder.HasOne(r => r.SourceEntity)
            .WithMany()
            .HasForeignKey(r => r.SourceEntityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.TargetEntity)
            .WithMany()
            .HasForeignKey(r => r.TargetEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.SourceEntityId, r.TargetEntityId, r.RelationType })
            .IsUnique();

        builder.HasIndex(r => r.TargetEntityId);
    }
}
