using Domain.Entity.Coworking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations
{
    public class BookingExtensionConfiguration : IEntityTypeConfiguration<BookingExtension>
    {
        public void Configure(EntityTypeBuilder<BookingExtension> builder)
        {
            builder.ToTable("booking_extensions");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.AddedHours)
                .IsRequired()
                .HasPrecision(18, 2);

            builder.Property(e => e.SnapshotHourlyRate)
                .IsRequired()
                .HasPrecision(18, 2);

            builder.Property(e => e.Amount)
                .IsRequired()
                .HasPrecision(18, 2);

            builder.Property(e => e.Notes).HasMaxLength(500);

            builder.HasOne(e => e.Booking)
                .WithMany(b => b.Extensions)
                .HasForeignKey(e => e.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
