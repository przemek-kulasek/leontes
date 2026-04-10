using Leontes.Domain.Entities;
using Leontes.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leontes.Infrastructure.Data.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content)
            .IsRequired();

        builder.Property(m => m.Role)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(m => m.Channel)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(m => m.IsComplete)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(m => m.ConversationId);
    }
}
