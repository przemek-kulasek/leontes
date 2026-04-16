using Leontes.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leontes.Infrastructure.Data.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.LastMessageAt)
            .IsRequired();

        builder.Property(c => c.InitiatedBy)
            .IsRequired()
            .HasConversion<string>()
            .HasDefaultValue(Domain.Enums.MessageInitiator.User);

        builder.Property(c => c.IsProactive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(c => c.LastMessageAt);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
