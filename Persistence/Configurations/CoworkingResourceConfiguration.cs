using Domain.Entity.Coworking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations
{
    public class CoworkingResourceConfiguration : IEntityTypeConfiguration<CoworkingResource>
    {
        public void Configure(EntityTypeBuilder<CoworkingResource> builder)
        {
            builder.ToTable("coworking_resources");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(r => r.ResourceType)
                .IsRequired();

            builder.Property(r => r.Capacity)
                .IsRequired();

            builder.Property(r => r.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(r => r.ResourceType);
            builder.HasIndex(r => r.IsActive);
        }
    }
}
