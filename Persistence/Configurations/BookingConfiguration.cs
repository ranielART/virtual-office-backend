using Domain.Entity.Coworking;
using Domain.Enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.ToTable("bookings");
            builder.HasKey(b => b.Id);

            builder.Property(b => b.ReferenceNumber)
                .IsRequired()
                .HasMaxLength(20);

            builder.HasIndex(b => b.ReferenceNumber).IsUnique();

            builder.Property(b => b.BookingType).IsRequired();
            builder.Property(b => b.BookingStatus).IsRequired();
            builder.Property(b => b.PaymentStatus)
                .IsRequired()
                .HasDefaultValue(PaymentStatus.Unpaid);

            builder.Property(b => b.SnapshotPackageName)
                .IsRequired()
                .HasMaxLength(200);

            // All money fields: precision 18,2
            builder.Property(b => b.SnapshotPricePerUnit).HasPrecision(18, 2);
            builder.Property(b => b.SnapshotHourlyRate).HasPrecision(18, 2);
            builder.Property(b => b.BaseAmount).HasPrecision(18, 2);
            builder.Property(b => b.DiscountAmount).HasPrecision(18, 2).HasDefaultValue(0);
            builder.Property(b => b.DownpaymentAmount).HasPrecision(18, 2).HasDefaultValue(0);
            builder.Property(b => b.BalanceAmount).HasPrecision(18, 2);
            builder.Property(b => b.OverstayAmount).HasPrecision(18, 2).HasDefaultValue(0);
            builder.Property(b => b.TotalAmount).HasPrecision(18, 2);

            builder.Property(b => b.PaxCount).IsRequired().HasDefaultValue(1);
            builder.Property(b => b.IsStudent).IsRequired().HasDefaultValue(false);

            // FK: CoworkingResource — restrict delete so resources can't be deleted if bookings exist
            builder.HasOne(b => b.Resource)
                .WithMany(r => r.Bookings)
                .HasForeignKey(b => b.ResourceId)
                .OnDelete(DeleteBehavior.Restrict);

            // FK: ServicePackage — restrict delete
            builder.HasOne(b => b.ServicePackage)
                .WithMany()
                .HasForeignKey(b => b.ServicePackageId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for common query patterns
            builder.HasIndex(b => b.BookingStatus);
            builder.HasIndex(b => new { b.ResourceId, b.ScheduledStart, b.ScheduledEnd });
            builder.HasIndex(b => b.ScheduledStart);
        }
    }
}
