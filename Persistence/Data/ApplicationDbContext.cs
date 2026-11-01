using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Domain.Entity.Identity;
using Domain.Entity.Coworking;
using Microsoft.AspNetCore.Identity;

namespace Persistence.Data
{
    public class ApplicationDbContext : IdentityDbContext<
        User,
        Role,
        string,
        IdentityUserClaim<string>,
        UserRole,
        IdentityUserLogin<string>,
        IdentityRoleClaim<string>,
        IdentityUserToken<string>
    >
    {
        public DbSet<CoworkingResource> CoworkingResources => Set<CoworkingResource>();
        public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<BookingGuest> BookingGuests => Set<BookingGuest>();
        public DbSet<BookingPayment> BookingPayments => Set<BookingPayment>();
        public DbSet<BookingExtension> BookingExtensions => Set<BookingExtension>();
        public DbSet<Inbox> Inboxes => Set<Inbox>();
        public DbSet<VirtualOfficeApplication> VirtualOfficeApplications => Set<VirtualOfficeApplication>();

        public ApplicationDbContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Apply all configurations from the assembly
            builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // Rename AspNet tables
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                var table = entityType.GetTableName();
                if (table!.StartsWith("AspNet"))
                {
                    entityType.SetTableName(table.Substring(6).Replace(" ", "_"));
                }
            }


        }
    }
}
