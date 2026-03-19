using Domain.Entity.Coworking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations
{
    public class InboxConfiguration : IEntityTypeConfiguration<Inbox>
    {
        public void Configure(EntityTypeBuilder<Inbox> builder)
        {
            builder.ToTable("inboxes");
            builder.HasKey(i => i.Id);

            builder.Property(i => i.FullName)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(i => i.Email)
                .IsRequired()
                .HasMaxLength(256);

            builder.Property(i => i.Subject)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(i => i.Message)
                .IsRequired()
                .HasMaxLength(4000);

            builder.Property(i => i.Phone)
                .HasMaxLength(20);

            builder.Property(i => i.IsRead)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(i => i.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("New");

            builder.Property(i => i.AdminNotes)
                .HasMaxLength(2000);

            builder.HasIndex(i => i.Email);
            builder.HasIndex(i => i.CreatedAt);
            builder.HasIndex(i => i.IsRead);
            builder.HasIndex(i => i.Status);
        }
    }
}