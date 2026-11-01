using Domain.Entity.Coworking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations
{
    public class ServicePackageConfiguration : IEntityTypeConfiguration<ServicePackage>
    {
        public void Configure(EntityTypeBuilder<ServicePackage> builder)
        {
            builder.ToTable("service_packages");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(p => p.ResourceType).IsRequired();
            builder.Property(p => p.PackageType).IsRequired();

            builder.Property(p => p.PriceAmount)
                .IsRequired()
                .HasPrecision(18, 2);

            builder.Property(p => p.StudentDiscountPercent)
                .HasPrecision(5, 2)
                .HasDefaultValue(0);

            builder.Property(p => p.DownpaymentPercent)
                .HasPrecision(5, 2)
                .HasDefaultValue(50);

            builder.Property(p => p.IsStudentEligible)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(p => p.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(p => new { p.ResourceType, p.IsActive });
        }
    }
}
