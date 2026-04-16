using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leontes.Infrastructure.Data.Configurations;

public sealed class StoredProactiveEventConfiguration : IEntityTypeConfiguration<StoredProactiveEvent>
{
    public void Configure(EntityTypeBuilder<StoredProactiveEvent> builder)
    {
        builder.ToTable("StoredProactiveEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.PayloadJson)
            .IsRequired();

        builder.Property(e => e.Urgency)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasDefaultValue(ProactiveEventStatus.Pending);

        builder.Property(e => e.RequestId)
            .HasMaxLength(200);

        builder.Property(e => e.Response)
            .HasMaxLength(2000);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.RequestId);
    }
}
