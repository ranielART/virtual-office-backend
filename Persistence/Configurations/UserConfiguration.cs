using Domain.Entity.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);

            // 3. Property Constraints
            builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            builder.Property(u => u.MiddleName).HasMaxLength(100);
            builder.Property(u => u.Address).HasMaxLength(250);

            // 4. Enum Configuration (Store as String: "Pending", "Active")
            builder.Property(u => u.Status)
                .HasConversion<string>()
                .IsRequired();
            builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);

            // 5. Default Value for Password Change Flag
            builder.Property(u => u.RequiresPasswordChange)
                .HasDefaultValue(false);

            // 6. Relationships

            // One-to-Many: User -> RefreshTokens (Cascade Delete)
            builder.HasMany(u => u.RefreshTokens)
                .WithOne(rt => rt.User)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(u => u.RefreshTokens)
                .WithOne(rt => rt.User)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Many-to-Many: User -> Roles (Via UserRole)
            builder.HasMany(u => u.Roles)
                .WithOne(ur => ur.User)
                .HasForeignKey(ur => ur.UserId)
                .IsRequired();
        }
    }
}
