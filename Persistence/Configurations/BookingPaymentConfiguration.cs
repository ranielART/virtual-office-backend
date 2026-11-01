using Domain.Entity.Coworking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations
{
    public class BookingPaymentConfiguration : IEntityTypeConfiguration<BookingPayment>
    {
        public void Configure(EntityTypeBuilder<BookingPayment> builder)
        {
            builder.ToTable("booking_payments");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Amount)
                .IsRequired()
                .HasPrecision(18, 2);

            builder.Property(p => p.PaymentMethod).IsRequired();
            builder.Property(p => p.PaymentType).IsRequired();
            builder.Property(p => p.PaymentValidationStatus).IsRequired();

            builder.Property(p => p.ProofOfPaymentUrl).HasMaxLength(1000);
            builder.Property(p => p.ReferenceNumber).HasMaxLength(200);
            builder.Property(p => p.ExternalPaymentRef).HasMaxLength(200);

            builder.HasOne(p => p.Booking)
                .WithMany(b => b.Payments)
                .HasForeignKey(p => p.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(p => p.PaymentValidationStatus);
            builder.HasIndex(p => p.ExternalPaymentRef);
        }
    }
}
