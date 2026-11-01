using Domain.Entity.Coworking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations
{
    public class BookingGuestConfiguration : IEntityTypeConfiguration<BookingGuest>
    {
        public void Configure(EntityTypeBuilder<BookingGuest> builder)
        {
            builder.ToTable("booking_guests");
            builder.HasKey(g => g.Id);

            builder.Property(g => g.FirstName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(g => g.LastName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(g => g.Email)
                .HasMaxLength(256);

            builder.Property(g => g.Phone)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(g => g.StudentIdImageUrl)
                .HasMaxLength(1000);

            builder.Property(g => g.IsStudentVerified)
                .IsRequired()
                .HasDefaultValue(false);

            // 1-to-1 with Booking — BookingGuest is the dependent side
            builder.HasOne(g => g.Booking)
                .WithOne(b => b.Guest)
                .HasForeignKey<BookingGuest>(g => g.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
